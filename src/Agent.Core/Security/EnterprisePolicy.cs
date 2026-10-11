using Agent.Core.AI;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Agent.Core.Security;

public sealed record EnterprisePolicyDocument(
    string PolicyId,
    bool ForceLocalOnlyMode,
    bool AllowExternalWebToolLaunch,
    int MaxBatchFilesLimit,
    IReadOnlyList<string> AllowedSkillIds,
    string? SignatureHex = null);

/// <summary>
/// Enterprise Policy Engine (#77): allows IT administrators to lock down execution
/// modes (e.g., ForceLocalOnlyMode), restrict skills to an approved subset, cap batch
/// sizes, and verify policy integrity via HMAC-SHA256.
/// </summary>
public sealed class EnterprisePolicy
{
    public EnterprisePolicy(EnterprisePolicyDocument? document = null)
    {
        Document = document ?? new EnterprisePolicyDocument(
            PolicyId: "default.unrestricted",
            ForceLocalOnlyMode: false,
            AllowExternalWebToolLaunch: true,
            MaxBatchFilesLimit: 1000,
            AllowedSkillIds: []);
    }

    public EnterprisePolicyDocument Document { get; }

    public void EnsureSkillAllowedByPolicy(string skillId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        if (Document.AllowedSkillIds.Count > 0 &&
            !Document.AllowedSkillIds.Contains(skillId, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"مهارت «{skillId}» توسط سیاست سازمانی (EnterprisePolicy) مسدود شده است.");
        }
    }

    public void EnsureExecutionModeAllowed(AiExecutionMode requestedMode)
    {
        if (Document.ForceLocalOnlyMode && requestedMode != AiExecutionMode.LocalOnly)
        {
            throw new UnauthorizedAccessException("سیاست سازمانی فقط اجرای ۱۰۰٪ محلی (ForceLocalOnlyMode) را مجاز می‌داند.");
        }
    }

    public static string SignPolicyDocument(EnterprisePolicyDocument doc, byte[] hmacKey)
    {
        var canonical = $"{doc.PolicyId}|{doc.ForceLocalOnlyMode}|{doc.AllowExternalWebToolLaunch}|{doc.MaxBatchFilesLimit}|{string.Join(",", doc.AllowedSkillIds)}";
        using var hmac = new HMACSHA256(hmacKey);
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    public static bool VerifyPolicySignature(EnterprisePolicyDocument doc, byte[] hmacKey)
    {
        if (string.IsNullOrWhiteSpace(doc.SignatureHex))
        {
            return false;
        }

        var expected = SignPolicyDocument(doc, hmacKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(doc.SignatureHex.ToLowerInvariant()));
    }

    public static EnterprisePolicy LoadFromJson(string json)
    {
        var doc = JsonSerializer.Deserialize<EnterprisePolicyDocument>(json)
            ?? throw new InvalidOperationException("فایل سیاست سازمانی نامعتبر است.");
        return new EnterprisePolicy(doc);
    }
}
