using System;
using System.IO;
using System.Media;

namespace ZzzBattery.Windows;

internal sealed class ReminderSoundPlayer : IDisposable
{
    private SoundPlayer? _player;

    public static SoundPlayer Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) ||
            (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsUnc) ||
            !string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请选择本地 WAV 音频文件。");
        if (new FileInfo(path).Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("请选择不超过 10 MB 的 WAV 提示音。");
        var player = new SoundPlayer(path);
        try { player.Load(); return player; }
        catch { player.Dispose(); throw; }
    }

    public bool TryPlay(string path)
    {
        Stop();
        try { _player = Load(path); _player.Play(); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or TimeoutException)
        { Stop(); return false; }
    }

    public void Stop() { _player?.Stop(); _player?.Dispose(); _player = null; }
    public void Dispose() => Stop();
}
