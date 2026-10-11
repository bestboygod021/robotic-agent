using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Localization;

/// <summary>
/// Normalizes multilingual user input across Persian, Arabic, English, Spanish,
/// French, and Simplified Chinese so intent detection and search work reliably
/// regardless of script variants, diacritics, or digit styles.
/// </summary>
public static class TextNormalizer
{
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeMultilingual(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // First apply FormKC to fold full-width Latin/digits (e.g., ＰＤＦ -> PDF, １２３ -> 123).
        var compatibilityNormalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(compatibilityNormalized.Length);

        foreach (var character in compatibilityNormalized)
        {
            // Strip Arabic/Persian harakat and vowel marks.
            if ((character >= '\u064b' && character <= '\u065f') || character == '\u0670')
            {
                continue;
            }

            var mapped = character switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                'ؤ' => 'و',
                'إ' or 'أ' => 'ا',
                '\u0640' => ' ', // tatweel
                '\u200c' or '\u200e' or '\u200f' or '\u3000' => ' ',
                '،' or '؛' or '؟' or '。' or '，' or '、' or '！' or '？' => ' ',
                '۰' => '0', '۱' => '1', '۲' => '2', '۳' => '3', '۴' => '4',
                '۵' => '5', '۶' => '6', '۷' => '7', '۸' => '8', '۹' => '9',
                '٠' => '0', '١' => '1', '٢' => '2', '٣' => '3', '٤' => '4',
                '٥' => '5', '٦' => '6', '٧' => '7', '٨' => '8', '٩' => '9',
                _ => FoldLatinAccent(character)
            };

            builder.Append(mapped);
        }

        return MultiWhitespace.Replace(builder.ToString(), " ").Trim().ToLowerInvariant();
    }

    private static char FoldLatinAccent(char character)
    {
        // Only fold Latin-1 / Latin Extended accented letters (e.g., é -> e, ñ -> n, ç -> c)
        // so Persian/Arabic letters with hamza/madda (like آ) are not altered.
        if (character < '\u00c0' || character > '\u024f')
        {
            return character;
        }

        var decomposed = character.ToString().Normalize(NormalizationForm.FormD);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                return ch;
            }
        }

        return character;
    }
}
