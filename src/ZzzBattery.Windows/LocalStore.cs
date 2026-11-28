using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public enum CredentialLoadIssue
{
    None,
    Missing,
    StorageUnavailable,
    DecryptionFailed,
    InvalidData,
    AccountMismatch,
    InvalidCookie
}

public sealed class LocalStore
{
    private const string PendingProfileCleanupFile = "pending-profile-cleanup";
    private const string ActiveProfileSlotFile = "webview-profile-slot";
    public string Root { get; }
    public string WebViewProfilePath { get; private set; }
    public CredentialLoadIssue LastCredentialLoadIssue { get; private set; }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public LocalStore(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
        CleanupPendingProfiles();
        WebViewProfilePath = ResolveWebViewProfilePath();
    }
    private string FilePath(string name) => Path.Combine(Root, name);
    public T? Read<T>(string name)
    {
        try { return File.Exists(FilePath(name)) ? JsonSerializer.Deserialize<T>(File.ReadAllText(FilePath(name)), Options) : default; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return default; }
    }
    public void Write<T>(string name, T value) => AtomicWrite(name, JsonSerializer.SerializeToUtf8Bytes(value, Options));
    private void AtomicWrite(string name, byte[] bytes)
    {
        var target = FilePath(name);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, target, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public Credential? LoadCredential()
    {
        LastCredentialLoadIssue = CredentialLoadIssue.None;
        if (!File.Exists(FilePath("credentials.dpapi")))
        {
            LastCredentialLoadIssue = CredentialLoadIssue.Missing;
            return null;
        }
        byte[]? plaintext = null;
        try
        {
            plaintext = WindowsProtection.Unprotect(File.ReadAllBytes(FilePath("credentials.dpapi")));
            var credential = JsonSerializer.Deserialize<Credential>(plaintext);
            if (credential?.Role is null)
            {
                LastCredentialLoadIssue = CredentialLoadIssue.InvalidData;
                return null;
            }
            var parsed = Cookies.Create(credential.Cookie);
            if (parsed.AccountKey != credential.AccountKey)
            {
                LastCredentialLoadIssue = CredentialLoadIssue.AccountMismatch;
                return null;
            }
            credential.Cookie = parsed.Cookie;
            return credential;
        }
        catch (CryptographicException)
        {
            LastCredentialLoadIssue = CredentialLoadIssue.DecryptionFailed;
            return null;
        }
        catch (ArgumentException)
        {
            LastCredentialLoadIssue = CredentialLoadIssue.InvalidCookie;
            return null;
        }
        catch (JsonException)
        {
            LastCredentialLoadIssue = CredentialLoadIssue.InvalidData;
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            LastCredentialLoadIssue = CredentialLoadIssue.StorageUnavailable;
            return null;
        }
        finally { if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext); }
    }
    public void SaveCredential(Credential credential)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(credential);
        try { AtomicWrite("credentials.dpapi", WindowsProtection.Protect(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal T? ReadProtected<T>(string name)
    {
        byte[]? plaintext = null;
        try
        {
            if (!File.Exists(FilePath(name))) return default;
            plaintext = WindowsProtection.Unprotect(File.ReadAllBytes(FilePath(name)));
            return JsonSerializer.Deserialize<T>(plaintext);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        { return default; }
        finally { if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext); }
    }
    internal void WriteProtected<T>(string name, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try { AtomicWrite(name, WindowsProtection.Protect(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public VerificationProof? LoadVerificationProof()
    {
        if (!File.Exists(FilePath("verification.dpapi"))) return null;
        byte[]? plaintext = null;
        try
        {
            plaintext = WindowsProtection.Unprotect(File.ReadAllBytes(FilePath("verification.dpapi")));
            var proof = JsonSerializer.Deserialize<VerificationProof>(plaintext);
            if (proof is null || !MiyousheClient.IsSafeVerificationValue(proof.Value) ||
                proof.ExpiresAt <= DateTimeOffset.UtcNow || string.IsNullOrWhiteSpace(proof.AccountKey) ||
                string.IsNullOrWhiteSpace(proof.RoleKey))
            {
                ClearVerificationProof();
                return null;
            }
            return proof;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or CryptographicException or ArgumentException)
        {
            return null;
        }
        finally { if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext); }
    }
    public void SaveVerificationProof(VerificationProof proof)
    {
        if (!MiyousheClient.IsSafeVerificationValue(proof.Value) || proof.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            ClearVerificationProof();
            return;
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(proof);
        try { AtomicWrite("verification.dpapi", WindowsProtection.Protect(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public void ClearVerificationProof()
    {
        try { if (File.Exists(FilePath("verification.dpapi"))) File.Delete(FilePath("verification.dpapi")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    public void ClearAccount()
    {
        foreach (var name in new[] { "credentials.dpapi", "browser-session.dpapi", "verification.dpapi", "snapshot.json", "reminders.json", "sync-state.json", "daily-sign.json" })
            if (File.Exists(FilePath(name))) File.Delete(FilePath(name));
        var targets = new HashSet<string>(ReadPendingCleanupNames(), StringComparer.OrdinalIgnoreCase) { "WebViewProfile" };
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(Root, "WebViewProfile-*"))
            {
                var name = Path.GetFileName(dir);
                if (IsValidProfileDirName(name)) targets.Add(name);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }

        var remaining = DeleteProfileDirectories(targets);
        if (remaining.Count == 0)
        {
            TryDeleteFile(FilePath(PendingProfileCleanupFile));
            TryDeleteFile(FilePath(ActiveProfileSlotFile));
            WebViewProfilePath = FilePath("WebViewProfile");
        }
        else
        {
            WritePendingCleanupNames(remaining);
            string nextSlot;
            do { nextSlot = "WebViewProfile-" + Guid.NewGuid().ToString("N")[..8]; }
            while (Directory.Exists(FilePath(nextSlot)) || remaining.Contains(nextSlot, StringComparer.OrdinalIgnoreCase));
            try { AtomicWrite(ActiveProfileSlotFile, System.Text.Encoding.UTF8.GetBytes(nextSlot)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            WebViewProfilePath = FilePath(nextSlot);
        }
    }

    public void CleanupPendingProfiles()
    {
        var pending = ReadPendingCleanupNames();
        if (pending.Count == 0) return;
        var remaining = DeleteProfileDirectories(pending);
        if (remaining.Count == 0)
        {
            TryDeleteFile(FilePath(PendingProfileCleanupFile));
            var active = ResolveWebViewProfilePath();
            if (!Directory.Exists(active) && !Directory.Exists(FilePath("WebViewProfile")))
            {
                TryDeleteFile(FilePath(ActiveProfileSlotFile));
                WebViewProfilePath = FilePath("WebViewProfile");
            }
        }
        else
        {
            WritePendingCleanupNames(remaining);
        }
    }

    private string ResolveWebViewProfilePath()
    {
        try
        {
            if (File.Exists(FilePath(ActiveProfileSlotFile)))
            {
                var slot = File.ReadAllText(FilePath(ActiveProfileSlotFile)).Trim();
                if (IsValidProfileDirName(slot)) return FilePath(slot);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return FilePath("WebViewProfile");
    }

    private static bool IsValidProfileDirName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name == "WebViewProfile" || name.StartsWith("WebViewProfile-", StringComparison.Ordinal)) &&
        name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0 &&
        !name.Contains("..", StringComparison.Ordinal);

    private List<string> ReadPendingCleanupNames()
    {
        try
        {
            var marker = FilePath(PendingProfileCleanupFile);
            if (!File.Exists(marker)) return [];
            return File.ReadAllLines(marker)
                .Select(line => line.Trim())
                .Where(IsValidProfileDirName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    private void WritePendingCleanupNames(List<string> names)
    {
        try { AtomicWrite(PendingProfileCleanupFile, System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, names))); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private List<string> DeleteProfileDirectories(IEnumerable<string> names)
    {
        var remaining = new List<string>();
        foreach (var name in names)
        {
            if (!IsValidProfileDirName(name)) continue;
            var full = Path.GetFullPath(FilePath(name));
            var relative = Path.GetRelativePath(Root, full);
            if (string.IsNullOrEmpty(relative) || relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                continue;
            try
            {
                if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                remaining.Add(name);
            }
        }
        return remaining;
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

internal static class WindowsProtection
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    public static byte[] Protect(byte[] data) => Transform(data, true);
    public static byte[] Unprotect(byte[] data) => Transform(data, false);
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var ok = protect
                ? CryptProtectData(ref input, "ZZZBattery", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new CryptographicException("Windows 无法解密当前用户的凭据。");
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            {
                for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                LocalFree(output.Data);
            }
        }
    }
}
