using Agent.Core.AI;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Agent.Desktop;

/// <summary>
/// Stores provider secrets in the current Windows user's Credential Manager.
/// Secrets are never written to app settings or history.
/// </summary>
internal sealed class WindowsAiCredentialStore : IAiCredentialStore
{
    private const uint GenericCredentialType = 1;
    private const uint PersistForCurrentMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBytes = 5 * 1024;
    private const string TargetPrefix = "RoboticAgent/AiProvider/";

    public Task SaveAsync(string providerId, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        if (secretBytes.Length > MaximumCredentialBytes)
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            throw new ArgumentException("The provider credential exceeds the Windows Credential Manager size limit.", nameof(secret));
        }

        var targetNamePointer = IntPtr.Zero;
        var userNamePointer = IntPtr.Zero;
        var credentialBlobPointer = IntPtr.Zero;
        try
        {
            targetNamePointer = Marshal.StringToHGlobalUni(GetTargetName(providerId));
            userNamePointer = Marshal.StringToHGlobalUni("RoboticAgent");
            credentialBlobPointer = Marshal.AllocHGlobal(secretBytes.Length);
            Marshal.Copy(secretBytes, 0, credentialBlobPointer, secretBytes.Length);

            var credential = new NativeCredential
            {
                Type = GenericCredentialType,
                TargetName = targetNamePointer,
                CredentialBlobSize = checked((uint)secretBytes.Length),
                CredentialBlob = credentialBlobPointer,
                Persist = PersistForCurrentMachine,
                UserName = userNamePointer
            };

            if (!CredWriteW(ref credential, flags: 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ذخیرهٔ امن کلید provider در Windows Credential Manager ناموفق بود.");
            }

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            ZeroUnmanagedMemory(credentialBlobPointer, secretBytes.Length);
            FreeHGlobal(credentialBlobPointer);
            FreeHGlobal(userNamePointer);
            FreeHGlobal(targetNamePointer);
        }
    }

    public Task<string?> ReadAsync(string providerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        var targetName = GetTargetName(providerId);

        if (!CredReadW(targetName, GenericCredentialType, flags: 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return Task.FromResult<string?>(null);
            }

            throw new Win32Exception(error, "خواندن امن کلید provider از Windows Credential Manager ناموفق بود.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlobSize > MaximumCredentialBytes || credential.CredentialBlob == IntPtr.Zero)
            {
                throw new InvalidDataException("مقدار credential ذخیره‌شده معتبر نیست.");
            }

            var secretLength = checked((int)credential.CredentialBlobSize);
            var secretBytes = new byte[secretLength];
            try
            {
                Marshal.Copy(credential.CredentialBlob, secretBytes, 0, secretBytes.Length);
                return Task.FromResult<string?>(Encoding.UTF8.GetString(secretBytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secretBytes);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public Task DeleteAsync(string providerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        if (!CredDeleteW(GetTargetName(providerId), GenericCredentialType, flags: 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error, "حذف کلید provider از Windows Credential Manager ناموفق بود.");
            }
        }

        return Task.CompletedTask;
    }

    private static string GetTargetName(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        if (providerId.Length > 128 || providerId.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("Provider ID may contain only ASCII letters, digits, dot, underscore, and hyphen.", nameof(providerId));
        }

        return TargetPrefix + providerId.ToLowerInvariant();
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ذخیرهٔ امن کلید provider فقط در ویندوز پشتیبانی می‌شود.");
        }
    }

    private static void ZeroUnmanagedMemory(IntPtr pointer, int byteCount)
    {
        if (pointer == IntPtr.Zero || byteCount <= 0)
        {
            return;
        }

        Marshal.Copy(new byte[byteCount], 0, pointer, byteCount);
    }

    private static void FreeHGlobal(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string targetName, uint type, uint flags, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string targetName, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
