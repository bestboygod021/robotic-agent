using Agent.Core.Localization;
using Agent.Core.Tools;
using Agent.Core.Workflows;

namespace Agent.Core.UX;

public enum UserRolePersona
{
    Accountant,
    LegalCompliance,
    HumanResources,
    ContentCreator,
    CustomerSupport,
    SoftwareDeveloper
}

public sealed record OnboardingRecommendationPack(
    UserRolePersona Persona,
    string LanguageCode,
    string WelcomeHeadline,
    IReadOnlyList<WorkflowDefinition> RecommendedPlaybooks,
    IReadOnlyList<AiToolEntry> RecommendedAiTools);

/// <summary>
/// 60-Second Onboarding Wizard Service (#85): tailors the initial workspace by
/// selecting the user's language and job role and immediately surfacing 3 matching
/// SMB Playbooks and 3 recommended AI tools.
/// </summary>
public static class OnboardingWizardService
{
    public static OnboardingRecommendationPack BuildStarterPack(
        UserRolePersona persona,
        string languageCode = CultureCatalog.DefaultLanguageCode)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        var category = persona switch
        {
            UserRolePersona.Accountant => "Accounting",
            UserRolePersona.LegalCompliance => "Legal",
            UserRolePersona.HumanResources => "HR",
            UserRolePersona.ContentCreator => "Content",
            UserRolePersona.CustomerSupport => "Support",
            _ => "IT"
        };

        var playbooks = PlaybookCatalog.All
            .Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToArray();

        var queryHint = persona switch
        {
            UserRolePersona.Accountant => "spreadsheet invoice automation",
            UserRolePersona.LegalCompliance => "document research pdf",
            UserRolePersona.HumanResources => "meeting notes workflow",
            UserRolePersona.ContentCreator => "image video writing",
            UserRolePersona.CustomerSupport => "chatbot translation voice",
            _ => "code debug ide"
        };

        var tools = AiToolMatchmaker.RecommendTools(queryHint, lang, maxResults: 3)
            .Select(r => r.Tool)
            .ToArray();

        var headline = lang switch
        {
            "fa" => $"بستهٔ شروع سریع ویژهٔ نقش «{persona}» آماده شد.",
            "ar" => $"تم تجهيز حزمة البدء السريع لدور «{persona}».",
            "zh" => $"已为您准备好“{persona}”专属快速入门套件。",
            "es" => $"Paquete de inicio rápido listo para el perfil «{persona}».",
            "fr" => $"Pack de démarrage rapide prêt pour le profil «{persona}».",
            _ => $"Quick-start pack ready for '{persona}'."
        };

        return new OnboardingRecommendationPack(persona, lang, headline, playbooks, tools);
    }
}
