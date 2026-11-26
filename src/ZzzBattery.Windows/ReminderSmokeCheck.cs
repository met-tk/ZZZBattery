using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZzzBattery.Windows;

internal static class ReminderSmokeCheck
{
    public static void Run(App app)
    {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception("Reminders: " + message); }
        var path = Path.Combine(app.Store.Root, "提示音播放检查.wav");
        using (var writer = new BinaryWriter(File.Create(path), Encoding.ASCII))
        {
            // 0.1 second of silent, mono 16-bit PCM exercises playback without audible test noise.
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(1636);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(1600); writer.Write(new byte[1600]);
        }
        using var sound = new ReminderSoundPlayer();
        Assert(sound.TryPlay(path), "PCM WAV with a Unicode path did not play.");
        sound.Stop();
        Assert(!sound.TryPlay(Path.Combine(app.Store.Root, "missing.wav")), "Missing sound was accepted.");
        Assert(!sound.TryPlay(@"\\127.0.0.1\share\alert.wav"), "UNC sound path was accepted.");
        var bad = Path.Combine(app.Store.Root, "invalid.wav"); File.WriteAllText(bad, "invalid wave");
        Assert(!sound.TryPlay(bad), "Invalid sound was accepted.");
        app.Settings.ReminderSoundPath = path; app.SaveSettings();
        var saved = app.Store.Read<ZzzBattery.Core.WidgetSettings>("settings.json");
        Assert(saved?.ReminderSoundPath == path, "Sound path did not persist.");

        using var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/battery.ico")).Stream;
        var played = new List<string>();
        using var tray = new TrayIcon(new System.Drawing.Icon(iconStream), selected => { played.Add(selected); return true; });
        var clicks = 0; var balloonClicks = 0;
        tray.Click += () => clicks++; tray.BalloonClick += () => balloonClicks++;
        SendMessage(tray.Handle, 0x8001, new IntPtr(1), new IntPtr(0x202));
        SendMessage(tray.Handle, 0x8001, new IntPtr(1), new IntPtr(0x405));
        Assert(clicks == 1 && balloonClicks == 1, "Native tray callbacks did not dispatch.");
        tray.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
        tray.ContextMenuStrip.Items.Add("提醒功能检查");
        SendMessage(tray.Handle, 0x8001, new IntPtr(1), new IntPtr(0x205));
        Assert(tray.ContextMenuStrip.Visible, "Native tray context menu did not open.");
        tray.ContextMenuStrip.Close();
        // Headless/remote sessions may not expose Explorer's notification area. Keep
        // the smoke test useful there while still exercising the full path on a desktop.
        if (!tray.ShowReminder("电量提醒功能检查（静音）", "Silent", "")) return;
        Assert(tray.ShowReminder("提示音回调检查", "Custom", path), "Windows rejected the custom notification.");
        // The previous notification may disappear after the next one has already been submitted.
        SendMessage(tray.Handle, 0x8001, new IntPtr(1), new IntPtr(0x403));
        SendMessage(tray.Handle, 0x8001, new IntPtr(1), new IntPtr(0x402));
        Assert(played.Count == 1 && played[0] == path, "Previous notification swallowed the custom sound.");
        Assert(tray.ShowReminder("静音回调检查", "Silent", ""), "Windows rejected the silent notification.");
        SendMessage(tray.Handle, 0x8001, new IntPtr(1), new IntPtr(0x402));
        Assert(played.Count == 1, "Silent notification played custom audio.");
    }

    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
