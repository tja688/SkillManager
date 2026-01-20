using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SkillManager.Services;

public sealed class TranslationTermProtector
{
    private readonly List<string> _protectedTerms;
    private readonly Dictionary<string, string> _mappedPhrases;

    public TranslationTermProtector(IEnumerable<string> protectedTerms, IDictionary<string, string> mappedPhrases)
    {
        _protectedTerms = protectedTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(term => term.Length)
            .ToList();

        _mappedPhrases = mappedPhrases
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .OrderByDescending(kv => kv.Key.Length)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    public ProtectedText Protect(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ProtectedText(input, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = input;
        var counter = 1;

        foreach (var phrase in _mappedPhrases)
        {
            var placeholder = $"__MAP_{counter:000}__";
            if (ReplaceAll(output, phrase.Key, placeholder, out var replaced))
            {
                output = replaced;
                replacements[placeholder] = phrase.Value;
                counter++;
            }
        }

        foreach (var term in _protectedTerms)
        {
            var placeholder = $"__TERM_{counter:000}__";
            if (ReplaceAll(output, term, placeholder, out var replaced))
            {
                output = replaced;
                replacements[placeholder] = term;
                counter++;
            }
        }

        return new ProtectedText(output, replacements);
    }

    public string Restore(string text, ProtectedText protectedText)
    {
        if (string.IsNullOrWhiteSpace(text) || protectedText.Replacements.Count == 0)
        {
            return text;
        }

        var output = text;
        foreach (var kv in protectedText.Replacements)
        {
            output = output.Replace(kv.Key, kv.Value, StringComparison.Ordinal);
        }

        return output;
    }

    private static bool ReplaceAll(string input, string token, string placeholder, out string output)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            output = input;
            return false;
        }

        var regex = BuildRegex(token);
        if (!regex.IsMatch(input))
        {
            output = input;
            return false;
        }

        output = regex.Replace(input, placeholder);
        return true;
    }

    private static Regex BuildRegex(string token)
    {
        var escaped = Regex.Escape(token);
        if (Regex.IsMatch(token, @"^[A-Za-z0-9]+$"))
        {
            return new Regex($@"\b{escaped}\b", RegexOptions.IgnoreCase);
        }

        return new Regex(escaped, RegexOptions.IgnoreCase);
    }
}

public sealed class ProtectedText
{
    public ProtectedText(string text, Dictionary<string, string> replacements)
    {
        Text = text;
        Replacements = replacements;
    }

    public string Text { get; }
    public Dictionary<string, string> Replacements { get; }
}
