using System.Globalization;
using System.Text;

namespace Kofflard.Core.Rules;

/// <summary>Comparing musician names the way a person would: case, accents and spacing ignored.</summary>
public static class Names
{
    /// <summary>The longest name the search accepts (design §11).</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// "Björk", "bjork " and "BJÖRK" all become "bjork"; "The Beatles" and "Beatles, The" stay apart.
    /// </summary>
    public static string Normalise(string name)
    {
        var decomposed = name.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Whether two names are the same musician's name.</summary>
    public static bool Same(string a, string b) => Normalise(a) == Normalise(b);

    /// <summary>
    /// The name as the search sends it: control characters removed, whitespace collapsed, trimmed,
    /// at most <see cref="MaxLength"/> characters. Empty when nothing usable was typed.
    /// </summary>
    public static string Clean(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return string.Empty;
        }

        var spaced = new string([.. typed.Select(c => char.IsControl(c) ? ' ' : c)]);
        var collapsed = string.Join(' ', spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaxLength ? collapsed : collapsed[..MaxLength].TrimEnd();
    }
}
