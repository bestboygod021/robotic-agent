using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Diagnostics;

public sealed record UpdatePackageManifest(
    string Version,
    string PackageFileName,
    long PackageSizeBytes,
    string ExpectedSha256Hex,
    string SignatureHex);

public sealed record UpdateVerificationResult(
    bool IsValid,
    bool IsNewerVersion,
    string? ErrorReason);

/// <summary>
/// Offline Update Package Verifier (#96): verifies semantic version progression
/// (preventing downgrade attacks), file size, SHA-256 package hash, and HMAC signature.
/// </summary>
public static class UpdatePackageVerifier
{
    public static UpdateVerificationResult VerifyUpdatePackage(
        string currentVersionText,
        UpdatePackageManifest manifest,
        ReadOnlySpan<byte> packageBytes,
        byte[] signingHmacKey)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(signingHmacKey);

        if (!Version.TryParse(currentVersionText, out var currentVersion) ||
            !Version.TryParse(manifest.Version, out var targetVersion))
        {
            return new UpdateVerificationResult(false, false, "قالب شمارهٔ نسخه معتبر نیست.");
        }

        if (targetVersion <= currentVersion)
        {
            return new UpdateVerificationResult(false, false, $"حملهٔ تنزل نسخه (Downgrade) یا نسخهٔ تکراری مسدود شد: نسخهٔ فعلی {currentVersion}، بستهٔ دریافتی {targetVersion}.");
        }

        if (packageBytes.Length != manifest.PackageSizeBytes)
        {
            return new UpdateVerificationResult(false, true, "حجم بستهٔ به‌روزرسانی با مانیفست مطابقت ندارد.");
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
        if (!string.Equals(actualHash, manifest.ExpectedSha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateVerificationResult(false, true, "هش SHA-256 بستهٔ به‌روزرسانی با مانیفست هم‌خوانی ندارد.");
        }

        var expectedSig = ComputeManifestSignatureHex(manifest, signingHmacKey);
        if (!string.Equals(expectedSig, manifest.SignatureHex, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateVerificationResult(false, true, "امضای دیجیتال مانیفست به‌روزرسانی نامعتبر است.");
        }

        return new UpdateVerificationResult(true, true, null);
    }

    public static string ComputeManifestSignatureHex(UpdatePackageManifest manifest, byte[] signingHmacKey)
    {
        var canonical = $"{manifest.Version}|{manifest.PackageFileName}|{manifest.PackageSizeBytes}|{manifest.ExpectedSha256Hex.ToLowerInvariant()}";
        using var hmac = new HMACSHA256(signingHmacKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
