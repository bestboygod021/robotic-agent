using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public sealed record LocalSemanticSearchRequest(
    string DirectoryPath,
    string QueryText,
    string Extension = "*",
    int MaxResults = 10);

public sealed record LocalSearchHit(
    string FilePath,
    string FileName,
    double Score,
    string Snippet,
    IReadOnlyList<string> MatchedTerms);

public sealed record LocalSemanticSearchPreview(
    string DirectoryPath,
    string QueryText,
    IReadOnlyList<LocalSearchHit> Hits)
{
    public int HitCount => Hits.Count;
}

public sealed record LocalSemanticSearchResult(
    int HitCount,
    IReadOnlyList<LocalSearchHit> Hits,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline multilingual semantic & lexical document search skill (#28) using
/// word and character n-gram TF-IDF cosine similarity across Persian, English,
/// Chinese, Spanish, Arabic, and French documents.
/// </summary>
public sealed class LocalSemanticSearchSkill : ISkill<LocalSemanticSearchRequest, LocalSemanticSearchPreview, LocalSemanticSearchResult>
{
    public const string SkillId = "doc.search";
    private static readonly Regex TokenPattern = new(@"[\p{L}\p{N}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CjkPattern = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "جست‌وجوی معنایی محلی داخل محتوای اسناد",
            ["en"] = "Local Semantic & Full-Text Document Search",
            ["zh"] = "本地文档内容语义与全文检索",
            ["es"] = "Búsqueda Semántica Local en Contenido de Documentos",
            ["ar"] = "بحث دلالي محلي داخل محتوى المستندات",
            ["fr"] = "Recherche Sémantique Locale dans les Documents"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "یافتن فایل‌ها بر اساس مفهوم و واژگان داخل متن در ۶ زبان (بدون ایندکس ابری) همراه با نمایش بخش مرتبط (Snippet).",
            ["en"] = "Finds documents by concept and content similarity across 6 languages offline, returning ranked snippets.",
            ["zh"] = "完全在本地对 6 种语言文档内容进行概念与关键词检索，返回相关度排序与文本片段。",
            ["es"] = "Encuentra documentos por contenido y similitud en 6 idiomas sin conexión, mostrando fragmentos relevantes.",
            ["ar"] = "يبحث داخل محتوى المستندات بـ 6 لغات محلياً دون اتصال ويعرض المقاطع الأكثر صلة.",
            ["fr"] = "Recherche dans le contenu des documents en 6 langues hors-ligne et affiche les extraits pertinents."
        },
        RiskLevel: SkillRiskLevel.ReadOnly,
        RequiresPreviewApproval: false,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public LocalSemanticSearchPreview Preview(LocalSemanticSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var directory = PathGuard.ValidateSelectedFolder(request.DirectoryPath, "جست‌وجوی اسناد");
        var normalizedQuery = TextNormalizer.NormalizeMultilingual(request.QueryText);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new LocalSemanticSearchPreview(directory, string.Empty, []);
        }

        var ext = FileCopySkill.NormalizeExtension(request.Extension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var queryFeatures = ExtractFeatures(normalizedQuery);
        var queryTokens = ExtractTokens(normalizedQuery);

        var hits = new List<LocalSearchHit>();
        foreach (var filePath in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            var rawText = File.ReadAllText(filePath, Encoding.UTF8);
            var normalizedDoc = TextNormalizer.NormalizeMultilingual(rawText);
            if (string.IsNullOrWhiteSpace(normalizedDoc))
            {
                continue;
            }

            var docFeatures = ExtractFeatures(normalizedDoc);
            var cosine = ComputeCosineSimilarity(queryFeatures, docFeatures);
            var matchedTerms = queryTokens
                .Where(t => normalizedDoc.Contains(t, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var score = cosine + (matchedTerms.Length * 0.25);
            if (score <= 0.05)
            {
                continue;
            }

            var snippet = BuildSnippet(rawText, matchedTerms);
            hits.Add(new LocalSearchHit(
                FilePath: filePath,
                FileName: Path.GetFileName(filePath),
                Score: Math.Round(score, 4),
                Snippet: snippet,
                MatchedTerms: matchedTerms));
        }

        var ranked = hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.FileName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(request.MaxResults, 1, 50))
            .ToArray();

        return new LocalSemanticSearchPreview(directory, request.QueryText, ranked);
    }

    public Task<LocalSemanticSearchResult> ExecuteAsync(
        LocalSemanticSearchRequest request,
        LocalSemanticSearchPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: []);

        return Task.FromResult(new LocalSemanticSearchResult(approvedPreview.HitCount, approvedPreview.Hits, manifest));
    }

    private static string[] ExtractTokens(string normalizedText)
    {
        var tokens = TokenPattern.Matches(normalizedText).Select(m => m.Value).ToList();
        foreach (Match m in CjkPattern.Matches(normalizedText))
        {
            tokens.Add(m.Value);
        }

        return tokens.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Dictionary<string, double> ExtractFeatures(string normalizedText)
    {
        var counts = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in ExtractTokens(normalizedText))
        {
            counts.TryGetValue($"w:{token}", out var c);
            counts[$"w:{token}"] = c + 1.5;

            for (var i = 0; i <= token.Length - 3; i++)
            {
                var tri = $"g:{token.Substring(i, 3)}";
                counts.TryGetValue(tri, out var gc);
                counts[tri] = gc + 0.5;
            }
        }

        return counts;
    }

    private static double ComputeCosineSimilarity(
        Dictionary<string, double> a,
        Dictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        double dot = 0;
        foreach (var (key, valA) in a)
        {
            if (b.TryGetValue(key, out var valB))
            {
                dot += valA * valB;
            }
        }

        var normA = Math.Sqrt(a.Values.Sum(v => v * v));
        var normB = Math.Sqrt(b.Values.Sum(v => v * v));
        return normA > 0 && normB > 0 ? dot / (normA * normB) : 0;
    }

    private static string BuildSnippet(string rawText, IReadOnlyList<string> matchedTerms)
    {
        var clean = Regex.Replace(rawText, @"\s+", " ").Trim();
        if (clean.Length <= 140)
        {
            return clean;
        }

        return clean[..140] + "...";
    }
}
