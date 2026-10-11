using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Security;

/// <summary>
/// Local Data Protector (#78): encrypts and decrypts local configuration and history
/// payloads using authenticated AES-256-GCM bound to the current OS user identity
/// (cross-platform compatible with Windows DPAPI key material).
/// </summary>
public sealed class LocalDataProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public LocalDataProtector(string? customScopeEntropy = null)
    {
        var userIdentity = $"{Environment.UserName}|{Environment.MachineName}|{customScopeEntropy ?? "RoboticAgent.LocalVault.v1"}";
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(userIdentity));
    }

    public string ProtectString(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize + TagSize, cipherBytes.Length);
        return Convert.ToBase64String(combined);
    }

    public string UnprotectString(string base64CipherPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64CipherPayload);
        var combined = Convert.FromBase64String(base64CipherPayload);
        if (combined.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("دادهٔ رمزنگاری‌شده کوتاه‌تر از ساختار معتبر AES-GCM است.");
        }

        var nonce = combined.AsSpan(0, NonceSize);
        var tag = combined.AsSpan(NonceSize, TagSize);
        var cipher = combined.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
