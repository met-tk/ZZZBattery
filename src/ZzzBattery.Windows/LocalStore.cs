using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public sealed class LocalStore
{
    public string Root { get; }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public LocalStore(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); }
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
        if (!File.Exists(FilePath("credentials.dpapi"))) return null;
        byte[]? plaintext = null;
        try
        {
            plaintext = WindowsProtection.Unprotect(File.ReadAllBytes(FilePath("credentials.dpapi")));
            var credential = JsonSerializer.Deserialize<Credential>(plaintext);
            if (credential?.Role is null) return null;
            var parsed = Cookies.Create(credential.Cookie);
            if (parsed.AccountKey != credential.AccountKey) return null;
            credential.Cookie = parsed.Cookie;
            return credential;
        }
        catch (Exception e) when (e is IOException or JsonException or CryptographicException or ArgumentException) { return null; }
        finally { if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext); }
    }
    public void SaveCredential(Credential credential)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(credential);
        try { AtomicWrite("credentials.dpapi", WindowsProtection.Protect(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public void ClearAccount()
    {
        foreach (var name in new[] { "credentials.dpapi", "snapshot.json", "reminders.json", "sync-state.json" })
            if (File.Exists(FilePath(name))) File.Delete(FilePath(name));
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
