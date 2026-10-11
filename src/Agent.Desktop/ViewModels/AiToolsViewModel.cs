using Agent.Core.Localization;
using Agent.Core.Storage;
using Agent.Core.Tools;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Agent.Desktop.ViewModels;

public sealed record LocalizedCategoryOption(
    string Id,
    string DisplayName,
    string Description,
    string IconGeometry,
    int ToolCount);

public sealed class AiToolCardItem : INotifyPropertyChanged
{
    private bool _isFavorite;
    private bool _isSelectedForComparison;
    private string? _matchmakerReason;

    public AiToolCardItem(
        AiTool tool,
        string languageCode,
        bool isFavorite,
        bool isSelectedForComparison,
        string? matchmakerReason = null)
    {
        Tool = tool;
        LanguageCode = CultureCatalog.NormalizeLanguageCode(languageCode);
        _isFavorite = isFavorite;
        _isSelectedForComparison = isSelectedForComparison;
        _matchmakerReason = matchmakerReason;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AiTool Tool { get; }

    public string LanguageCode { get; private set; }

    public string Id => Tool.Id;

    public string CategoryId => Tool.CategoryId;

    public string Name => Tool.Name;

    public string IconText => Tool.IconText;

    public string CategoryIconGeometry => Tool.CategoryIconGeometry;

    public string LoginUrl => Tool.LoginUrl;

    public string CategoryName => Tool.GetLocalizedCategoryName(LanguageCode);

    public string DescriptionFa => Tool.GetLocalizedDescription(LanguageCode);

    public string PrivacyBadgeLabel => Tool.GetPrivacyBadgeLabel(LanguageCode);

    public string LaunchButtonLabel => Tool.GetLaunchButtonLabel(LanguageCode);

    public string FreeTierBadge => Tool.HasFreeTier
        ? (LanguageCode == "fa" ? "پلن رایگان دارد" : LanguageCode == "ar" ? "خطة مجانية" : LanguageCode == "zh" ? "含免费版" : LanguageCode == "es" ? "Plan gratis" : LanguageCode == "fr" ? "Offre gratuite" : "Free Tier")
        : (LanguageCode == "fa" ? "حرفه‌ای / پولی" : LanguageCode == "ar" ? "مدفوع" : LanguageCode == "zh" ? "付费专业版" : LanguageCode == "es" ? "De pago" : LanguageCode == "fr" ? "Payant" : "Paid Plan");

    public string StarterPrompt => Tool.GetPromptTemplate(LanguageCode);

    public string FavoriteSymbol => _isFavorite ? "★" : "☆";

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value)
            {
                return;
            }

            _isFavorite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoriteSymbol));
        }
    }

    public bool IsSelectedForComparison
    {
        get => _isSelectedForComparison;
        set
        {
            if (_isSelectedForComparison == value)
            {
                return;
            }

            _isSelectedForComparison = value;
            OnPropertyChanged();
        }
    }

    public string? MatchmakerReason
    {
        get => _matchmakerReason;
        set
        {
            if (_matchmakerReason == value)
            {
                return;
            }

            _matchmakerReason = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMatchmakerReason));
        }
    }

    public bool HasMatchmakerReason => !string.IsNullOrWhiteSpace(_matchmakerReason);

    public void UpdateLanguage(string languageCode)
    {
        LanguageCode = CultureCatalog.NormalizeLanguageCode(languageCode);
        OnPropertyChanged(nameof(LanguageCode));
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(DescriptionFa));
        OnPropertyChanged(nameof(PrivacyBadgeLabel));
        OnPropertyChanged(nameof(LaunchButtonLabel));
        OnPropertyChanged(nameof(FreeTierBadge));
        OnPropertyChanged(nameof(StarterPrompt));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// MVVM ViewModel for the 200-tool AI Hub window. Coordinates multilingual
/// filtering, Smart Matchmaker recommendations, 2-to-4 tool side-by-side
/// comparison, favorites/recents persistence, and safe prompt handoff.
/// </summary>
public sealed class AiToolsViewModel : INotifyPropertyChanged
{
    private readonly Localizer _localizer;
    private readonly AiToolMatchmaker _matchmaker;
    private readonly AiToolComparisonService _comparisonService;
    private readonly WebToolHandoffService _handoffService;
    private readonly ToolPreferenceStore _preferenceStore;
    private readonly CustomAiToolStore _customToolStore;
    private readonly HashSet<string> _favoriteIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedComparisonIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _recentIds = [];
    private readonly List<AiToolCardItem> _allCards = [];

    private string _languageCode;
    private string _selectedCategoryId = "all";
    private string _searchQuery = string.Empty;
    private string _problemQuery = string.Empty;
    private bool _onlyFavorites;
    private bool _onlyFreeTier;
    private bool _preferOpenWeights;
    private bool _isMatchmakerActive;

    public AiToolsViewModel(
        Localizer? localizer = null,
        AiToolMatchmaker? matchmaker = null,
        AiToolComparisonService? comparisonService = null,
        WebToolHandoffService? handoffService = null,
        ToolPreferenceStore? preferenceStore = null,
        CustomAiToolStore? customToolStore = null)
    {
        _localizer = localizer ?? Localizer.Shared;
        _matchmaker = matchmaker ?? new AiToolMatchmaker();
        _comparisonService = comparisonService ?? new AiToolComparisonService();
        _handoffService = handoffService ?? new WebToolHandoffService();
        _preferenceStore = preferenceStore ?? new ToolPreferenceStore();
        _customToolStore = customToolStore ?? new CustomAiToolStore();
        _languageCode = _localizer.CurrentLanguageCode;

        RebuildCategories();
        RebuildAllCards([]);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LocalizedCategoryOption> Categories { get; } = [];

    public ObservableCollection<AiToolCardItem> VisibleTools { get; } = [];

    public IReadOnlyList<SupportedLanguage> Languages => CultureCatalog.Languages;

    public SupportedLanguage SelectedLanguage
    {
        get => CultureCatalog.GetLanguage(_languageCode);
        set
        {
            if (value is null)
            {
                return;
            }

            SetLanguage(value.Code);
        }
    }

    public string LanguageCode => _languageCode;

    public bool IsRightToLeft => CultureCatalog.GetLanguage(_languageCode).IsRightToLeft;

    public string SelectedCategoryId
    {
        get => _selectedCategoryId;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "all" : value;
            if (string.Equals(_selectedCategoryId, next, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedCategoryId = next;
            OnPropertyChanged();
            RefreshVisibleTools();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
            {
                return;
            }

            _searchQuery = value ?? string.Empty;
            OnPropertyChanged();
            RefreshVisibleTools();
        }
    }

    public string ProblemQuery
    {
        get => _problemQuery;
        set
        {
            if (_problemQuery == value)
            {
                return;
            }

            _problemQuery = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool OnlyFavorites
    {
        get => _onlyFavorites;
        set
        {
            if (_onlyFavorites == value)
            {
                return;
            }

            _onlyFavorites = value;
            OnPropertyChanged();
            RefreshVisibleTools();
        }
    }

    public bool OnlyFreeTier
    {
        get => _onlyFreeTier;
        set
        {
            if (_onlyFreeTier == value)
            {
                return;
            }

            _onlyFreeTier = value;
            OnPropertyChanged();
            RefreshVisibleTools();
        }
    }

    public bool PreferOpenWeights
    {
        get => _preferOpenWeights;
        set
        {
            if (_preferOpenWeights == value)
            {
                return;
            }

            _preferOpenWeights = value;
            OnPropertyChanged();
            if (_isMatchmakerActive)
            {
                RunMatchmaker();
            }
            else
            {
                RefreshVisibleTools();
            }
        }
    }

    public int SelectedComparisonCount => _selectedComparisonIds.Count;

    public int TotalToolCount => _allCards.Count;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var prefs = await _preferenceStore.LoadAsync(cancellationToken);
        var customTools = await _customToolStore.LoadAsync(cancellationToken);

        _favoriteIds.Clear();
        foreach (var id in prefs.FavoriteToolIds)
        {
            _favoriteIds.Add(id);
        }

        _recentIds.Clear();
        _recentIds.AddRange(prefs.RecentToolIds);

        SetLanguage(prefs.LanguageCode);
        RebuildAllCards(customTools);
        RefreshVisibleTools();
    }

    public void SetLanguage(string? languageCode)
    {
        var normalized = CultureCatalog.NormalizeLanguageCode(languageCode);
        _languageCode = normalized;
        _localizer.SetLanguage(normalized);

        RebuildCategories();
        foreach (var card in _allCards)
        {
            card.UpdateLanguage(normalized);
        }

        if (_isMatchmakerActive)
        {
            RunMatchmaker();
        }
        else
        {
            RefreshVisibleTools();
        }

        OnPropertyChanged(nameof(LanguageCode));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(IsRightToLeft));
    }

    public void RunMatchmaker()
    {
        var queryText = _problemQuery.Trim();
        if (string.IsNullOrWhiteSpace(queryText))
        {
            ClearMatchmaker();
            return;
        }

        _isMatchmakerActive = true;
        var recommendations = _matchmaker.Recommend(
            new AiToolMatchmakingQuery(
                ProblemDescription: queryText,
                LanguageCode: _languageCode,
                CategoryId: _selectedCategoryId,
                RequireFreeTier: _onlyFreeTier,
                PreferOpenWeightsOrEnterprisePrivacy: _preferOpenWeights,
                MaxResults: 12),
            _allCards.Select(card => card.Tool));

        var cardMap = _allCards.ToDictionary(card => card.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var card in _allCards)
        {
            card.MatchmakerReason = null;
        }

        VisibleTools.Clear();
        foreach (var rec in recommendations)
        {
            if (cardMap.TryGetValue(rec.Tool.Id, out var card))
            {
                if (_onlyFavorites && !card.IsFavorite)
                {
                    continue;
                }

                card.MatchmakerReason = rec.WhyRecommended;
                VisibleTools.Add(card);
            }
        }
    }

    public void ClearMatchmaker()
    {
        _isMatchmakerActive = false;
        _problemQuery = string.Empty;
        OnPropertyChanged(nameof(ProblemQuery));
        foreach (var card in _allCards)
        {
            card.MatchmakerReason = null;
        }

        RefreshVisibleTools();
    }

    public async Task ToggleFavoriteAsync(AiToolCardItem card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        var updated = await _preferenceStore.ToggleFavoriteAsync(card.Id, cancellationToken);
        _favoriteIds.Clear();
        foreach (var id in updated.FavoriteToolIds)
        {
            _favoriteIds.Add(id);
        }

        card.IsFavorite = _favoriteIds.Contains(card.Id);
        if (_onlyFavorites)
        {
            RefreshVisibleTools();
        }
    }

    public async Task RecordToolOpenedAsync(AiToolCardItem card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        var updated = await _preferenceStore.RecordRecentAsync(card.Id, cancellationToken);
        _recentIds.Clear();
        _recentIds.AddRange(updated.RecentToolIds);
    }

    public bool ToggleComparisonSelection(AiToolCardItem card, out string? limitMessage)
    {
        ArgumentNullException.ThrowIfNull(card);
        limitMessage = null;

        if (_selectedComparisonIds.Contains(card.Id))
        {
            _selectedComparisonIds.Remove(card.Id);
            card.IsSelectedForComparison = false;
            OnPropertyChanged(nameof(SelectedComparisonCount));
            return true;
        }

        if (_selectedComparisonIds.Count >= AiToolComparisonService.MaximumComparisonCount)
        {
            card.IsSelectedForComparison = false;
            limitMessage = _languageCode == "fa"
                ? $"حداکثر {AiToolComparisonService.MaximumComparisonCount} ابزار را می‌توان هم‌زمان مقایسه کرد."
                : $"You can compare up to {AiToolComparisonService.MaximumComparisonCount} tools at a time.";
            return false;
        }

        _selectedComparisonIds.Add(card.Id);
        card.IsSelectedForComparison = true;
        OnPropertyChanged(nameof(SelectedComparisonCount));
        return true;
    }

    public AiToolComparisonMatrix BuildComparisonMatrix()
    {
        var selectedTools = _allCards
            .Where(card => _selectedComparisonIds.Contains(card.Id))
            .Select(card => card.Tool)
            .ToArray();

        return _comparisonService.Compare(selectedTools, _languageCode);
    }

    public WebToolHandoffPackage PrepareSafeHandoff(AiToolCardItem card, string? userDraft = null) =>
        _handoffService.PrepareSafeHandoffPackage(card.Tool, userDraft ?? _problemQuery, _languageCode);

    public void RefreshVisibleTools()
    {
        if (_isMatchmakerActive && !string.IsNullOrWhiteSpace(_problemQuery))
        {
            RunMatchmaker();
            return;
        }

        var normalizedQuery = TextNormalizer.NormalizeMultilingual(_searchQuery);
        VisibleTools.Clear();

        foreach (var card in _allCards)
        {
            if (!string.Equals(_selectedCategoryId, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_selectedCategoryId, card.CategoryId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_onlyFavorites && !card.IsFavorite)
            {
                continue;
            }

            if (_onlyFreeTier && !card.Tool.HasFreeTier)
            {
                continue;
            }

            if (_preferOpenWeights && !card.Tool.SupportsOfflineOrOpenWeights)
            {
                continue;
            }

            if (normalizedQuery.Length > 0)
            {
                var searchable = TextNormalizer.NormalizeMultilingual(
                    $"{card.Name} {card.DescriptionFa} {card.Tool.DescriptionFa} {card.Tool.GetLocalizedDescription("en")} {card.CategoryName} {card.LoginUrl} {string.Join(' ', card.Tool.Tags)}");

                if (!searchable.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            VisibleTools.Add(card);
        }
    }

    private void RebuildCategories()
    {
        var previousCategory = _selectedCategoryId;
        Categories.Clear();
        foreach (var category in AiToolCatalog.Categories)
        {
            Categories.Add(new LocalizedCategoryOption(
                category.Id,
                category.GetLocalizedName(_languageCode),
                category.GetLocalizedDescription(_languageCode),
                category.IconGeometry,
                category.ToolCount));
        }

        _selectedCategoryId = Categories.Any(c => string.Equals(c.Id, previousCategory, StringComparison.OrdinalIgnoreCase))
            ? previousCategory
            : "all";
    }

    private void RebuildAllCards(IReadOnlyList<AiTool> customTools)
    {
        _allCards.Clear();
        foreach (var tool in customTools.Concat(AiToolCatalog.Tools))
        {
            _allCards.Add(new AiToolCardItem(
                tool,
                _languageCode,
                isFavorite: _favoriteIds.Contains(tool.Id),
                isSelectedForComparison: _selectedComparisonIds.Contains(tool.Id)));
        }

        OnPropertyChanged(nameof(TotalToolCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
