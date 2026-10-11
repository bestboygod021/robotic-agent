using Agent.Core.Localization;
using Agent.Core.Skills;
using Agent.Core.Skills.Documents;

namespace Agent.Core.Automation;

public sealed record ScreenRegion(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width is > 4 and <= 7680 && Height is > 4 and <= 4320 && X >= 0 && Y >= 0;
}

public sealed record ScreenSnipActionResult(
    ScreenRegion Region,
    string ExtractedRawText,
    bool IsTabularData,
    string FormattedOutput,
    IReadOnlyList<string> DetectedActionItems);

/// <summary>
/// Screen Snip to Action Service (#37): takes a captured screen region, runs it
/// through the offline OCR engine, and automatically formats tabular snips into
/// Markdown tables or extracts action items from prose snips.
/// </summary>
public sealed class ScreenSnipToActionService
{
    private readonly IOcrEngine _ocrEngine;

    public ScreenSnipToActionService(IOcrEngine? ocrEngine = null)
    {
        _ocrEngine = ocrEngine ?? new BuiltInOfflineOcrEngine();
    }

    public async Task<ScreenSnipActionResult> ProcessSnipFileAsync(
        ScreenRegion region,
        string snipFilePath,
        string languageCode = CultureCatalog.DefaultLanguageCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (!region.IsValid)
        {
            throw new ArgumentException("ابعاد ناحیهٔ انتخابی تصویر صفحه نامعتبر است.", nameof(region));
        }

        if (!File.Exists(snipFilePath))
        {
            throw new FileNotFoundException("فایل برش صفحه پیدا نشد.", snipFilePath);
        }

        var rawText = await _ocrEngine.ExtractTextAsync(snipFilePath, languageCode, cancellationToken).ConfigureAwait(false);
        var isTable = rawText.Contains('\t', StringComparison.Ordinal) ||
                      (rawText.Contains(',', StringComparison.Ordinal) && rawText.Split('\n').Length >= 2);

        if (isTable)
        {
            var (mdTable, _) = SmartClipboardSkill.Transform(rawText, ClipboardTransformMode.TsvToMarkdownTable);
            return new ScreenSnipActionResult(region, rawText, IsTabularData: true, mdTable, []);
        }

        var summary = BatchSummarizeSkill.SummarizeDocument("screen_snip", rawText, maxSentences: 3);
        var (cleaned, _) = SmartClipboardSkill.Transform(rawText, ClipboardTransformMode.CleanAndNormalizeText);
        return new ScreenSnipActionResult(region, rawText, IsTabularData: false, cleaned, summary.ActionItems);
    }
}
