using System.Threading;
using System.Threading.Tasks;

namespace SkillManager.Services;

public interface ITranslationProvider
{
    Task<string> TranslateAsync(string text, string sourceLang, string targetLang, TranslationOptions options, CancellationToken ct);
}

public sealed class TranslationOptions
{
    public int MaxLength { get; init; } = 96;
}
