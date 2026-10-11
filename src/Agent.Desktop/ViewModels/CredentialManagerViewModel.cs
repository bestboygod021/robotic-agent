using Agent.Core.AI;
using System.Collections.ObjectModel;

namespace Agent.Desktop.ViewModels;

/// <summary>
/// ViewModel for managing BYOK API credentials (#76) and inspecting outbound
/// Hybrid payloads (#55).
/// </summary>
public sealed class CredentialManagerViewModel : ObservableObject
{
    private readonly CredentialLifecycleManager _lifecycle;
    private readonly HybridConsentGate _consentGate;
    private string _selectedProviderId = "openai";
    private string _statusMessage = string.Empty;
    private OutboundPayloadInspection? _lastInspection;

    public CredentialManagerViewModel(
        IAiCredentialStore credentialStore,
        HybridConsentGate? consentGate = null)
    {
        _lifecycle = new CredentialLifecycleManager(credentialStore);
        _consentGate = consentGate ?? new HybridConsentGate();
    }

    public ObservableCollection<ProviderCredentialStatus> ProviderStatuses { get; } = [];

    public string SelectedProviderId
    {
        get => _selectedProviderId;
        set => SetProperty(ref _selectedProviderId, value);
    }

    public bool StrictLocalOnlyLock
    {
        get => _consentGate.StrictLocalOnlyLock;
        set
        {
            if (_consentGate.StrictLocalOnlyLock != value)
            {
                _consentGate.StrictLocalOnlyLock = value;
                RaisePropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public OutboundPayloadInspection? LastInspection
    {
        get => _lastInspection;
        private set => SetProperty(ref _lastInspection, value);
    }

    public async Task RefreshStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _lifecycle.GetAllStatusesAsync(cancellationToken).ConfigureAwait(false);
        ProviderStatuses.Clear();
        foreach (var status in statuses)
        {
            ProviderStatuses.Add(status);
        }
    }

    public async Task SaveKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken = default)
    {
        await _lifecycle.SaveOrRotateKeyAsync(providerId, apiKey, cancellationToken).ConfigureAwait(false);
        await RefreshStatusesAsync(cancellationToken).ConfigureAwait(false);
        StatusMessage = $"کلید API برای «{providerId}» به صورت امن ذخیره شد.";
    }

    public async Task DeleteKeyAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await _lifecycle.RevokeKeyAsync(providerId, cancellationToken).ConfigureAwait(false);
        await RefreshStatusesAsync(cancellationToken).ConfigureAwait(false);
        StatusMessage = $"کلید API برای «{providerId}» حذف شد.";
    }
}
