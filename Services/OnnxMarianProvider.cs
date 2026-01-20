using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SkillManager.Services;

public sealed class OnnxMarianProvider : ITranslationProvider, IDisposable
{
    private readonly TranslationModelConfig _config;
    private readonly object _initLock = new();
    private InferenceSession? _encoderSession;
    private InferenceSession? _decoderSession;
    private Tokenizer? _tokenizer;
    private EncoderIo? _encoderIo;
    private DecoderIo? _decoderIo;

    public OnnxMarianProvider(TranslationModelConfig config)
    {
        _config = config;
    }

    public Task<string> TranslateAsync(string text, string sourceLang, string targetLang, TranslationOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(string.Empty);
        }

        EnsureInitialized();

        var inputText = PrepareInput(text);
        var inputIds = Encode(inputText);
        var attentionMask = CreateMask(inputIds.Length);

        var hiddenStates = RunEncoder(inputIds, attentionMask);
        var decodedIds = RunGreedyDecode(hiddenStates, attentionMask, options.MaxLength, ct);

        var decoded = _tokenizer!.Decode(decodedIds.Select(id => (int)id).ToArray());
        return Task.FromResult(decoded.Trim());
    }

    public void Dispose()
    {
        _encoderSession?.Dispose();
        _decoderSession?.Dispose();
    }

    private void EnsureInitialized()
    {
        if (_encoderSession != null && _decoderSession != null && _tokenizer != null)
        {
            return;
        }

        lock (_initLock)
        {
            if (_encoderSession != null && _decoderSession != null && _tokenizer != null)
            {
                return;
            }

            if (!File.Exists(_config.EncoderModelPath))
            {
                throw new FileNotFoundException("Encoder model not found", _config.EncoderModelPath);
            }

            if (!File.Exists(_config.DecoderModelPath))
            {
                throw new FileNotFoundException("Decoder model not found", _config.DecoderModelPath);
            }

            if (!File.Exists(_config.TokenizerPath))
            {
                throw new FileNotFoundException("Tokenizer file not found", _config.TokenizerPath);
            }

            _encoderSession = new InferenceSession(_config.EncoderModelPath);
            _decoderSession = new InferenceSession(_config.DecoderModelPath);
            _tokenizer = LoadTokenizer(_config.TokenizerPath);
            _encoderIo = ResolveEncoderIo(_encoderSession);
            _decoderIo = ResolveDecoderIo(_decoderSession);
        }
    }

    private string PrepareInput(string text)
    {
        if (string.IsNullOrWhiteSpace(_config.SourcePrefix))
        {
            return text;
        }

        return $"{_config.SourcePrefix.Trim()} {text}";
    }

    private long[] Encode(string text)
    {
        var ids = _tokenizer!.EncodeToIds(text);
        return ids.Select(id => (long)id).ToArray();
    }

    private long[] CreateMask(int length)
    {
        var mask = new long[length];
        Array.Fill(mask, 1);
        return mask;
    }

    private DenseTensor<float> RunEncoder(long[] inputIds, long[] attentionMask)
    {
        var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
        var maskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_encoderIo!.InputIds, inputTensor),
            NamedOnnxValue.CreateFromTensor(_encoderIo.AttentionMask, maskTensor)
        };

        using var results = _encoderSession!.Run(inputs);

        var hidden = results.First(r => r.Name == _encoderIo.HiddenStates).AsTensor<float>();
        return CloneTensor(hidden);
    }

    private List<long> RunGreedyDecode(DenseTensor<float> hiddenStates, long[] attentionMask, int maxLength, CancellationToken ct)
    {
        var decoded = new List<long>();
        var startToken = ResolveStartToken();
        var eosToken = _config.EosTokenId ?? -1;

        decoded.Add(startToken);

        var encoderMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

        for (var step = 0; step < maxLength; step++)
        {
            ct.ThrowIfCancellationRequested();

            var decoderInput = new DenseTensor<long>(decoded.ToArray(), new[] { 1, decoded.Count });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_decoderIo!.InputIds, decoderInput),
                NamedOnnxValue.CreateFromTensor(_decoderIo.EncoderHiddenStates, hiddenStates)
            };

            if (!string.IsNullOrWhiteSpace(_decoderIo.EncoderAttentionMask))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(_decoderIo.EncoderAttentionMask, encoderMaskTensor));
            }

            using var results = _decoderSession!.Run(inputs);

            var logits = results.First(r => r.Name == _decoderIo.Logits).AsTensor<float>();
            var nextToken = ArgMaxLastToken(logits);
            decoded.Add(nextToken);

            if (eosToken >= 0 && nextToken == eosToken)
            {
                break;
            }
        }

        if (decoded.Count > 0)
        {
            decoded.RemoveAt(0);
        }

        if (eosToken >= 0)
        {
            var eosIndex = decoded.IndexOf(eosToken);
            if (eosIndex >= 0)
            {
                decoded = decoded.Take(eosIndex).ToList();
            }
        }

        return decoded;
    }

    private int ResolveStartToken()
    {
        if (_config.DecoderStartTokenId.HasValue) return _config.DecoderStartTokenId.Value;
        if (_config.BosTokenId.HasValue) return _config.BosTokenId.Value;
        if (_config.PadTokenId.HasValue) return _config.PadTokenId.Value;
        return 0;
    }

    private static long ArgMaxLastToken(Tensor<float> logits)
    {
        var dims = logits.Dimensions.ToArray();
        var seqLength = dims.Length >= 2 ? dims[1] : 1;
        var vocabSize = dims.Length >= 3 ? dims[2] : dims.Last();
        var data = logits.ToArray();
        var offset = (seqLength - 1) * vocabSize;

        var maxIndex = 0;
        var maxValue = float.NegativeInfinity;

        for (var i = 0; i < vocabSize; i++)
        {
            var value = data[offset + i];
            if (value > maxValue)
            {
                maxValue = value;
                maxIndex = i;
            }
        }

        return maxIndex;
    }

    private static DenseTensor<float> CloneTensor(Tensor<float> source)
    {
        var data = source.ToArray();
        return new DenseTensor<float>(data, source.Dimensions.ToArray());
    }

    private static Tokenizer LoadTokenizer(string tokenizerPath)
    {
        var extension = Path.GetExtension(tokenizerPath).ToLowerInvariant();
        if (extension is ".model" or ".spm")
        {
            var modelProtoType = Type.GetType("Sentencepiece.ModelProto, Microsoft.ML.Tokenizers", throwOnError: true);
            var parserProperty = modelProtoType!.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static);
            var parser = parserProperty!.GetValue(null);
            var parseFrom = parser!.GetType().GetMethod("ParseFrom", new[] { typeof(byte[]) });
            var model = parseFrom!.Invoke(parser, new object[] { File.ReadAllBytes(tokenizerPath) });

            var ctor = typeof(SentencePieceTokenizer).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(c => c.GetParameters().Length == 2);

            if (ctor == null)
            {
                throw new InvalidOperationException("SentencePiece tokenizer constructor not found.");
            }

            var tokenizer = ctor.Invoke(new object[] { model!, new Dictionary<string, int>() });
            return (Tokenizer)tokenizer;
        }

        throw new NotSupportedException("Tokenizer file must be a SentencePiece model (.model/.spm).");
    }

    private static EncoderIo ResolveEncoderIo(InferenceSession session)
    {
        var inputs = session.InputMetadata.Keys.ToList();
        var outputs = session.OutputMetadata.Keys.ToList();

        var inputIds = FindName(inputs, "input_ids") ?? inputs.First();
        var attentionMask = FindName(inputs, "attention_mask") ?? inputs.FirstOrDefault(n => !string.Equals(n, inputIds, StringComparison.Ordinal)) ?? inputIds;
        var hidden = FindName(outputs, "last_hidden_state") ?? outputs.First();

        return new EncoderIo(inputIds, attentionMask, hidden);
    }

    private static DecoderIo ResolveDecoderIo(InferenceSession session)
    {
        var inputs = session.InputMetadata.Keys.ToList();
        var outputs = session.OutputMetadata.Keys.ToList();

        var inputIds = FindName(inputs, "decoder_input_ids") ?? FindName(inputs, "input_ids") ?? inputs.First();
        var encoderHiddenStates = FindName(inputs, "encoder_hidden_states") ?? inputs.FirstOrDefault(n => !string.Equals(n, inputIds, StringComparison.Ordinal)) ?? inputs.First();
        var encoderAttentionMask = FindName(inputs, "encoder_attention_mask");
        var logits = FindName(outputs, "logits") ?? outputs.First();

        return new DecoderIo(inputIds, encoderHiddenStates, encoderAttentionMask, logits);
    }

    private static string? FindName(IEnumerable<string> names, string token)
    {
        return names.FirstOrDefault(name => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class EncoderIo
    {
        public EncoderIo(string inputIds, string attentionMask, string hiddenStates)
        {
            InputIds = inputIds;
            AttentionMask = attentionMask;
            HiddenStates = hiddenStates;
        }

        public string InputIds { get; }
        public string AttentionMask { get; }
        public string HiddenStates { get; }
    }

    private sealed class DecoderIo
    {
        public DecoderIo(string inputIds, string encoderHiddenStates, string? encoderAttentionMask, string logits)
        {
            InputIds = inputIds;
            EncoderHiddenStates = encoderHiddenStates;
            EncoderAttentionMask = encoderAttentionMask ?? string.Empty;
            Logits = logits;
        }

        public string InputIds { get; }
        public string EncoderHiddenStates { get; }
        public string EncoderAttentionMask { get; }
        public string Logits { get; }
    }
}
