using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Skills;
using Agent.Core.Skills.Rollback;
using Agent.Core.Storage;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Agent.Desktop.ViewModels;

/// <summary>
/// MVVM coordinator for main window localization, layout direction (RTL/LTR),
/// font adaptation, registered skill discovery, and transactional Undo (#20).
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Localizer _localizer;
    private readonly SkillRegistry _skillRegistry;
    private readonly ToolPreferenceStore _toolPreferenceStore;
    private readonly RollbackManager _rollbackManager = new();
    private SupportedLanguage _selectedLanguage;
    private OperationManifest? _lastRollbackManifest;

    public MainViewModel(
        Localizer? localizer = null,
        SkillRegistry? skillRegistry = null,
        ToolPreferenceStore? toolPreferenceStore = null)
    {
        _localizer = localizer ?? Localizer.Shared;
        _skillRegistry = skillRegistry ?? SkillRegistry.CreateDefault();
        _toolPreferenceStore = toolPreferenceStore ?? new ToolPreferenceStore();
        _selectedLanguage = _localizer.CurrentLanguage;
        _localizer.LanguageChanged += OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SupportedLanguage> Languages => CultureCatalog.Languages;

    public IReadOnlyList<SkillDescriptor> RegisteredSkills => _skillRegistry.Descriptors;

    public SupportedLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || string.Equals(_selectedLanguage.Code, value.Code, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedLanguage = value;
            _localizer.SetLanguage(value.Code);
            OnPropertyChanged();
            NotifyLocalizedPropertiesChanged();
        }
    }

    public string CurrentLanguageCode => _selectedLanguage.Code;

    public bool IsRightToLeft => _selectedLanguage.IsRightToLeft;

    public string RecommendedFontFamily => _selectedLanguage.RecommendedFontFamily;

    public string WindowTitle => _localizer.Get("app.title", CurrentLanguageCode);

    public string HeaderSubtitle => _localizer.Get("app.subtitle", CurrentLanguageCode);

    public string LanguagePickerLabel => _localizer.Get("app.languageLabel", CurrentLanguageCode);

    public string OpenAiCatalogButtonText => _localizer.Get("app.openAiCatalogButton", CurrentLanguageCode);

    public OperationManifest? LastRollbackManifest
    {
        get => _lastRollbackManifest;
        set
        {
            _lastRollbackManifest = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanUndoLastOperation));
        }
    }

    public bool CanUndoLastOperation => _lastRollbackManifest is { CanRollback: true };

    public async Task<RollbackExecutionResult> UndoLastOperationAsync(CancellationToken cancellationToken = default)
    {
        if (_lastRollbackManifest is null || !_lastRollbackManifest.CanRollback)
        {
            throw new InvalidOperationException("عملیات قابل بازگردانی در حافظه ثبت نشده است.");
        }

        var manifest = _lastRollbackManifest;
        _lastRollbackManifest = null;
        OnPropertyChanged(nameof(LastRollbackManifest));
        OnPropertyChanged(nameof(CanUndoLastOperation));

        return await _rollbackManager.UndoAsync(manifest, cancellationToken);
    }

    public async Task InitializeLanguagePreferenceAsync(CancellationToken cancellationToken = default)
    {
        var prefs = await _toolPreferenceStore.LoadAsync(cancellationToken);
        var language = CultureCatalog.GetLanguage(prefs.LanguageCode);
        SelectedLanguage = language;
    }

    public async Task SaveLanguagePreferenceAsync(CancellationToken cancellationToken = default)
    {
        var current = await _toolPreferenceStore.LoadAsync(cancellationToken);
        await _toolPreferenceStore.SaveAsync(current with { LanguageCode = CurrentLanguageCode }, cancellationToken);
    }

    public void CycleNextLanguage()
    {
        var list = Languages;
        var currentIndex = 0;
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Code, CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        SelectedLanguage = list[(currentIndex + 1) % list.Count];
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        _selectedLanguage = CultureCatalog.GetLanguage(languageCode);
        OnPropertyChanged(nameof(SelectedLanguage));
        NotifyLocalizedPropertiesChanged();
    }

    private void NotifyLocalizedPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentLanguageCode));
        OnPropertyChanged(nameof(IsRightToLeft));
        OnPropertyChanged(nameof(RecommendedFontFamily));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(LanguagePickerLabel));
        OnPropertyChanged(nameof(OpenAiCatalogButtonText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
