using Agent.Core.Localization;
using System.Text.RegularExpressions;

namespace Agent.Core.Tools;

public sealed record AiToolMatchmakingQuery(
    string ProblemDescription,
    string LanguageCode = CultureCatalog.DefaultLanguageCode,
    string? CategoryId = null,
    bool RequireFreeTier = false,
    bool PreferOpenWeightsOrEnterprisePrivacy = false,
    int MaxResults = 6);

public sealed record AiToolRecommendation(
    AiTool Tool,
    double Score,
    IReadOnlyList<string> MatchedSignals,
    string WhyRecommended);

/// <summary>
/// Offline recommendation engine that maps a user's problem statement in any of
/// the 6 supported languages (fa, en, zh, es, ar, fr) to the most relevant tools
/// in the 200-tool catalog.
/// </summary>
public sealed class AiToolMatchmaker
{
    private static readonly Regex TokenPattern = new(@"[\p{L}\p{N}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, string[]> CategoryIntentKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["chat"] =
            [
                "chat", "assistant", "conversation", "question", "brainstorm", "general",
                "چت", "گفتگو", "دستیار", "سوال", "پرسش", "مشاوره", "ایده",
                "محادثه", "مساعد", "سؤال", "دردشه",
                "asistente", "conversar", "preguntas", "ideas",
                "assistant", "discuter", "questions",
                "对话", "聊天", "助手", "问答"
            ],
            ["research"] =
            [
                "research", "paper", "academic", "citation", "literature", "scholar", "pdf", "study", "source",
                "پژوهش", "تحقیق", "مقاله", "منبع", "استناد", "علمی", "دانشگاهی", "رفرنس",
                "بحث", "مقالات", "مصادر", "علمي", "دراسه",
                "investigacion", "articulo", "cientifico", "fuentes", "citas",
                "recherche", "scientifique", "article", "sources", "bibliographie",
                "研究", "论文", "学术", "文献", "引用", "检索"
            ],
            ["writing"] =
            [
                "write", "writing", "grammar", "paraphrase", "rewrite", "essay", "copywriting", "blog", "email", "edit",
                "نویسندگی", "نوشتن", "ویرایش", "بازنویسی", "گرامر", "نگارش", "متن", "ایمیل", "محتوا",
                "كتابه", "تحرير", "صياغه", "تدقيق", "مقال", "بريد",
                "redactar", "redaccion", "gramatica", "reescribir", "correo", "texto",
                "rediger", "redaction", "grammaire", "reformuler", "texte", "courriel",
                "写作", "润色", "改写", "语法", "文章", "邮件"
            ],
            ["image"] =
            [
                "image", "photo", "picture", "design", "logo", "illustration", "background", "upscale", "vector", "poster",
                "تصویر", "عکس", "طراحی", "لوگو", "پوستر", "پس زمینه", "کیفیت عکس", "گرافیک", "وکتور",
                "صوره", "صور", "تصميم", "شعار", "خلفيه", "جرافيك",
                "imagen", "foto", "diseno", "logotipo", "fondo", "ilustracion",
                "image", "photo", "design", "logo", "fond", "illustration",
                "图像", "图片", "照片", "设计", "标志", "抠图", "背景"
            ],
            ["video"] =
            [
                "video", "clip", "movie", "subtitle", "caption", "avatar", "animation", "shorts", "reel", "footage",
                "ویدئو", "ویدیو", "فیلم", "زیرنویس", "کلیپ", "تیزر", "آواتار", "تدوین", "انیمیشن",
                "فيديو", "مقطع", "ترجمه", "مونتاج", "افاتار", "تحريك",
                "video", "subtitulos", "edicion", "avatar", "animacion", "cortos",
                "video", "montage", "sous-titres", "avatar", "animation",
                "视频", "剪辑", "字幕", "数字人", "动画", "短片"
            ],
            ["audio"] =
            [
                "audio", "voice", "speech", "tts", "music", "song", "podcast", "noise", "dubbing", "narration", "vocal",
                "صوت", "صدا", "گویندگی", "پادکست", "موسیقی", "آهنگ", "نویز", "دوبله", "نریشن", "گفتار",
                "صوت", "كلام", "موسيقى", "بودكاست", "ضوضاء", "تعليق صوتي", "دبلجه",
                "audio", "voz", "musica", "podcast", "ruido", "locucion", "doblaje",
                "audio", "voix", "musique", "podcast", "bruit", "doublage",
                "音频", "语音", "配音", "音乐", "播客", "降噪", "人声"
            ],
            ["coding"] =
            [
                "code", "coding", "programming", "developer", "debug", "bug", "refactor", "software", "app", "website", "git", "test",
                "کد", "برنامه نویسی", "کدنویسی", "دیباگ", "نرم افزار", "اپلیکیشن", "وبسایت", "تست", "باگ",
                "برمجه", "كود", "شفره", "تصحيح", "تطبيق", "برمجيات",
                "codigo", "programacion", "depurar", "desarrollo", "software", "aplicacion",
                "code", "programmation", "debogage", "developpement", "logiciel",
                "编程", "代码", "开发", "调试", "软件", "前端"
            ],
            ["productivity"] =
            [
                "meeting", "transcribe", "notes", "slide", "presentation", "schedule", "calendar", "workflow", "automate", "project", "task",
                "جلسه", "خلاصه جلسه", "اسلاید", "پرزنتیشن", "ارائه", "تقویم", "اتوماسیون", "گردش کار", "پروژه", "یادداشت",
                "اجتماع", "اجتماعات", "عرض تقديمي", "شرائح", "تقويم", "اتمته", "مشاريع",
                "reunion", "reuniones", "diapositivas", "presentacion", "calendario", "automatizar", "proyecto",
                "reunion", "diapositives", "presentation", "calendrier", "automatiser", "projet",
                "会议", "纪要", "转写", "幻灯片", "演示", "日程", "工作流", "自动化"
            ]
        };

    public IReadOnlyList<AiToolRecommendation> Recommend(
        AiToolMatchmakingQuery query,
        IEnumerable<AiTool>? sourceTools = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalizedProblem = TextNormalizer.NormalizeMultilingual(query.ProblemDescription);
        if (string.IsNullOrWhiteSpace(normalizedProblem))
        {
            return [];
        }

        var lang = CultureCatalog.NormalizeLanguageCode(query.LanguageCode);
        var tools = sourceTools ?? AiToolCatalog.Tools;
        var tokens = TokenPattern.Matches(normalizedProblem)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Detect category affinities from the problem statement
        var categoryAffinities = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (categoryId, keywords) in CategoryIntentKeywords)
        {
            var matched = keywords
                .Where(keyword => normalizedProblem.Contains(TextNormalizer.NormalizeMultilingual(keyword), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matched.Count > 0)
            {
                categoryAffinities[categoryId] = matched;
            }
        }

        var recommendations = new List<AiToolRecommendation>();
        foreach (var tool in tools)
        {
            if (!string.IsNullOrWhiteSpace(query.CategoryId) &&
                !string.Equals(query.CategoryId, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(query.CategoryId, tool.CategoryId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (query.RequireFreeTier && !tool.HasFreeTier)
            {
                continue;
            }

            double score = 0;
            var signals = new List<string>();

            if (categoryAffinities.TryGetValue(tool.CategoryId, out var catSignals))
            {
                score += 4.5 + Math.Min(3.0, catSignals.Count * 1.2);
                signals.AddRange(catSignals.Take(2));
            }

            var normalizedName = TextNormalizer.NormalizeMultilingual(tool.Name);
            if (normalizedProblem.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                score += 6.0;
                signals.Add(tool.Name);
            }

            var searchableToolText = TextNormalizer.NormalizeMultilingual(
                $"{tool.Name} {tool.DescriptionFa} {tool.GetLocalizedDescription(lang)} {tool.GetLocalizedDescription("en")} {string.Join(' ', tool.Tags)}");

            foreach (var token in tokens)
            {
                if (token.Length < 2)
                {
                    continue;
                }

                if (tool.Tags.Any(tag => TextNormalizer.NormalizeMultilingual(tag).Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 2.2;
                    if (!signals.Contains(token, StringComparer.OrdinalIgnoreCase))
                    {
                        signals.Add(token);
                    }
                }
                else if (searchableToolText.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1.4;
                    if (!signals.Contains(token, StringComparer.OrdinalIgnoreCase))
                    {
                        signals.Add(token);
                    }
                }
            }

            if (score <= 0)
            {
                continue;
            }

            if (tool.HasFreeTier)
            {
                score += 0.35;
            }

            if (query.PreferOpenWeightsOrEnterprisePrivacy)
            {
                if (tool.SupportsOfflineOrOpenWeights)
                {
                    score += 2.0;
                }
                else if (string.Equals(tool.PrivacyBadge, "EnterpriseReady", StringComparison.OrdinalIgnoreCase))
                {
                    score += 1.2;
                }
            }

            var distinctSignals = signals.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
            var reason = BuildReason(tool, distinctSignals, lang);
            recommendations.Add(new AiToolRecommendation(tool, Math.Round(score, 2), distinctSignals, reason));
        }

        var maxResults = Math.Clamp(query.MaxResults, 1, 25);
        return recommendations
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Tool.SupportsOfflineOrOpenWeights)
            .ThenByDescending(item => item.Tool.HasFreeTier)
            .ThenBy(item => item.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToArray();
    }

    private static string BuildReason(AiTool tool, IReadOnlyList<string> signals, string languageCode)
    {
        var categoryName = tool.GetLocalizedCategoryName(languageCode);
        var signalText = signals.Count > 0 ? string.Join("، ", signals) : categoryName;
        var privacyLabel = tool.GetPrivacyBadgeLabel(languageCode);

        return languageCode switch
        {
            "fa" => $"مناسب برای «{categoryName}» (کلیدواژه‌ها: {signalText}) • {privacyLabel}",
            "ar" => $"مناسب لفئة «{categoryName}» (الكلمات المطابقة: {signalText}) • {privacyLabel}",
            "zh" => $"匹配「{categoryName}」需求（关键词：{string.Join("、", signals.DefaultIfEmpty(categoryName))}）• {privacyLabel}",
            "es" => $"Recomendado en «{categoryName}» (coincidencias: {string.Join(", ", signals.DefaultIfEmpty(categoryName))}) • {privacyLabel}",
            "fr" => $"Recommandé dans « {categoryName} » (mots-clés : {string.Join(", ", signals.DefaultIfEmpty(categoryName))}) • {privacyLabel}",
            _ => $"Best fit in \"{categoryName}\" (matched: {string.Join(", ", signals.DefaultIfEmpty(categoryName))}) • {privacyLabel}"
        };
    }
}
