using Agent.Core.Localization;
using System.Text.Json;

namespace Agent.Core.Tools;

public sealed record AiToolCategory(
    string Id,
    string Name,
    string DescriptionFa,
    int ToolCount)
{
    public IReadOnlyDictionary<string, string>? Names { get; init; }

    public IReadOnlyDictionary<string, string>? Descriptions { get; init; }

    public string IconGeometry { get; init; } = "M4,4 H20 V20 H4 Z";

    public string GetLocalizedName(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (Names is not null && Names.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return Name;
    }

    public string GetLocalizedDescription(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (Descriptions is not null && Descriptions.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return DescriptionFa;
    }
}

public sealed record AiTool(
    string Id,
    string CategoryId,
    string Name,
    string DescriptionFa,
    string LoginUrl,
    string IconText)
{
    public string CategoryName { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string>? CategoryNames { get; init; }

    public IReadOnlyDictionary<string, string>? Descriptions { get; init; }

    public IReadOnlyDictionary<string, string>? PromptTemplates { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> SupportedLanguages { get; init; } = ["fa", "en", "zh", "es", "ar", "fr"];

    public bool HasFreeTier { get; init; } = true;

    public bool SupportsOfflineOrOpenWeights { get; init; }

    public string PrivacyBadge { get; init; } = "StandardCloud";

    public string UrlKind { get; init; } = "ProductPage";

    public string CategoryIconGeometry { get; init; } = "M4,4 H20 V20 H4 Z";

    public bool IsCustomTool { get; init; }

    public string GetLocalizedDescription(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (Descriptions is not null && Descriptions.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return DescriptionFa;
    }

    public string GetLocalizedCategoryName(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (CategoryNames is not null && CategoryNames.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return CategoryName;
    }

    public string GetPromptTemplate(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (PromptTemplates is not null && PromptTemplates.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return $"Use {Name} to help with:";
    }

    public string GetPrivacyBadgeLabel(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        return (PrivacyBadge, lang) switch
        {
            ("OpenWeights", "fa") => "مدل باز / قابل میزبانی",
            ("OpenWeights", "ar") => "أوزان مفتوحة / استضافة ذاتية",
            ("OpenWeights", "zh") => "开源权重 / 可自托管",
            ("OpenWeights", "es") => "Pesos abiertos / Autoalojable",
            ("OpenWeights", "fr") => "Poids ouverts / Auto-hébergeable",
            ("OpenWeights", _) => "Open-Weights / Self-Hostable",

            ("EnterpriseReady", "fa") => "سیاست سازمانی شفاف",
            ("EnterpriseReady", "ar") => "جاهز للمؤسسات",
            ("EnterpriseReady", "zh") => "企业级隐私合规",
            ("EnterpriseReady", "es") => "Privacidad empresarial",
            ("EnterpriseReady", "fr") => "Confidentialité Entreprise",
            ("EnterpriseReady", _) => "Enterprise Privacy Options",

            (_, "fa") => "سرویس ابری عمومی",
            (_, "ar") => "سحابة قياسية",
            (_, "zh") => "标准云服务",
            (_, "es") => "Nube estándar",
            (_, "fr") => "Cloud standard",
            _ => "Standard Cloud"
        };
    }

    public string GetLaunchButtonLabel(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        var isDirect = string.Equals(UrlKind, "DirectWorkspace", StringComparison.OrdinalIgnoreCase);

        return (isDirect, lang) switch
        {
            (true, "fa") => "ورود مستقیم  ↗",
            (false, "fa") => "صفحهٔ ابزار  ↗",
            (true, "ar") => "دخول مباشر  ↗",
            (false, "ar") => "صفحة الأداة  ↗",
            (true, "zh") => "直达工作区  ↗",
            (false, "zh") => "访问官网  ↗",
            (true, "es") => "Entrar directo  ↗",
            (false, "es") => "Ver herramienta  ↗",
            (true, "fr") => "Accès direct  ↗",
            (false, "fr") => "Page de l’outil  ↗",
            (true, _) => "Launch Workspace  ↗",
            (false, _) => "Open Tool Page  ↗"
        };
    }
}

public sealed class AiToolCatalogDocument
{
    public List<AiToolCategory>? Categories { get; set; }

    public List<AiTool>? Tools { get; set; }
}

/// <summary>
/// A curated, offline catalogue of 200 public AI tools across 8 categories with
/// multilingual metadata, starter prompts, and privacy badges. Links are only
/// opened when the user explicitly clicks a tool; the application does not call
/// these sites automatically.
/// </summary>
public static class AiToolCatalog
{
    public const int ExpectedCategoryCount = 8;
    public const int ToolsPerCategory = 25;
    private const string CatalogResource = "Agent.Core.Tools.Data.ai-tools-fa.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, string> AllCategoriesLocalizedNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "همهٔ دسته‌ها",
            ["en"] = "All Categories",
            ["zh"] = "所有类别",
            ["es"] = "Todas las categorías",
            ["ar"] = "جميع الفئات",
            ["fr"] = "Toutes les catégories"
        };

    private static readonly IReadOnlyDictionary<string, string> AllCategoriesLocalizedDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "نمایش همهٔ ابزارهای فهرست‌شده.",
            ["en"] = "Show all 200 curated AI tools.",
            ["zh"] = "显示全部 200 款精选 AI 工具。",
            ["es"] = "Mostrar las 200 herramientas de IA.",
            ["ar"] = "عرض جميع أدوات الذكاء الاصطناعي (200 أداة).",
            ["fr"] = "Afficher les 200 outils IA sélectionnés."
        };

    static AiToolCatalog()
    {
        var document = LoadDocument();
        var categories = document.Categories ?? throw new InvalidOperationException("دسته‌های کاتالوگ ابزارهای هوش مصنوعی موجود نیستند.");
        var tools = document.Tools ?? throw new InvalidOperationException("فهرست ابزارهای هوش مصنوعی موجود نیست.");

        if (categories.Count != ExpectedCategoryCount ||
            categories.Any(category => string.IsNullOrWhiteSpace(category.Id) ||
                string.IsNullOrWhiteSpace(category.Name) ||
                string.IsNullOrWhiteSpace(category.DescriptionFa) ||
                category.ToolCount != ToolsPerCategory) ||
            categories.Select(category => category.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != categories.Count)
        {
            throw new InvalidOperationException($"کاتالوگ باید دقیقاً {ExpectedCategoryCount} دستهٔ معتبر و یکتا داشته باشد.");
        }

        var categoryIds = categories.Select(category => category.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tools.Count != categories.Count * ToolsPerCategory ||
            tools.Any(tool => !categoryIds.Contains(tool.CategoryId)) ||
            tools.Select(tool => tool.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != tools.Count)
        {
            throw new InvalidOperationException("کاتالوگ باید دقیقاً ۲۵ ابزار یکتا در هر دسته داشته باشد.");
        }

        foreach (var category in categories)
        {
            var categoryTools = tools.Where(tool => string.Equals(tool.CategoryId, category.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (categoryTools.Length != ToolsPerCategory ||
                categoryTools.Select(tool => tool.IconText).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ToolsPerCategory)
            {
                throw new InvalidOperationException($"دستهٔ «{category.Name}» باید دقیقاً {ToolsPerCategory} ابزار و نشان حروفی یکتا داشته باشد.");
            }

            foreach (var tool in categoryTools)
            {
                if (string.IsNullOrWhiteSpace(tool.Name) ||
                    string.IsNullOrWhiteSpace(tool.DescriptionFa) ||
                    string.IsNullOrWhiteSpace(tool.IconText) ||
                    !TryGetSafeWebUri(tool.LoginUrl, out _))
                {
                    throw new InvalidOperationException($"اطلاعات ابزار «{tool.Id}» کامل یا معتبر نیست.");
                }
            }
        }

        Categories =
        [
            new AiToolCategory("all", "همهٔ دسته‌ها", "نمایش همهٔ ابزارهای فهرست‌شده.", tools.Count)
            {
                Names = AllCategoriesLocalizedNames,
                Descriptions = AllCategoriesLocalizedDescriptions,
                IconGeometry = "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z"
            },
            .. categories.Select(category => category with
            {
                ToolCount = tools.Count(tool => string.Equals(tool.CategoryId, category.Id, StringComparison.OrdinalIgnoreCase))
            })
        ];

        var categoryById = categories.ToDictionary(category => category.Id, StringComparer.OrdinalIgnoreCase);
        var categoryOrder = categories.Select((category, index) => (Id: category.Id, Index: index))
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.OrdinalIgnoreCase);

        Tools = tools
            .Select(tool =>
            {
                var cat = categoryById[tool.CategoryId];
                return tool with
                {
                    CategoryName = cat.Name,
                    CategoryNames = cat.Names,
                    CategoryIconGeometry = cat.IconGeometry
                };
            })
            .OrderBy(tool => categoryOrder[tool.CategoryId])
            .ThenBy(tool => tool.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    public static IReadOnlyList<AiToolCategory> Categories { get; }

    public static IReadOnlyList<AiTool> Tools { get; }

    public static bool TryGetSafeWebUri(string? value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(parsed.Host) ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static AiToolCatalogDocument LoadDocument()
    {
        using var stream = typeof(AiToolCatalog).Assembly.GetManifestResourceStream(CatalogResource)
            ?? throw new InvalidOperationException("دادهٔ کاتالوگ ابزارهای هوش مصنوعی در برنامه پیدا نشد.");

        return JsonSerializer.Deserialize<AiToolCatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidOperationException("دادهٔ کاتالوگ ابزارهای هوش مصنوعی خالی است.");
    }
}
