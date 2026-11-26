using System;
using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace ZzzBattery.Windows;

// Own the native icon so NIIF_NOSOUND can suppress Windows' sound for custom/silent reminders.
internal sealed class TrayIcon : Forms.NativeWindow, IDisposable
{
    private const int CallbackMessage = 0x8001;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private readonly Icon _icon;
    private readonly ReminderSoundPlayer _sound = new();
    private readonly Func<string, bool> _playSound;
    private string _text = "绝区零桌面电量";
    private string? _pendingSound;
    private bool _added;
    private bool _disposed;
    public Forms.ContextMenuStrip? ContextMenuStrip { get; set; }
    public event Action? Click;
    public event Action? BalloonClick;
    public event Action? SoundFailed;

    public TrayIcon(Icon icon, Func<string, bool>? playSound = null)
    {
        _icon = icon;
        _playSound = playSound ?? _sound.TryPlay;
        CreateHandle(new Forms.CreateParams { Caption = "ZZZBatteryTray" });
        AddIcon();
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_disposed || _text == value) return;
            _text = value;
            var data = Data(4); data.Tip = value.Length > 127 ? value[..127] : value;
            if (_added) ShellNotifyIcon(1, ref data);
        }
    }

    private NotifyIconData Data(uint flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = Handle, Id = 1, Flags = flags,
        Callback = CallbackMessage, Icon = _icon.Handle, Tip = _text,
        Info = "", Title = ""
    };

    private void AddIcon()
    {
        var data = Data(1 | 2 | 4);
        _added = ShellNotifyIcon(0, ref data) || ShellNotifyIcon(1, ref data);
    }

    public bool ShowReminder(string message, string sound, string path)
    {
        if (_disposed) return false;
        if (!_added) AddIcon();
        if (!_added) return false;
        _pendingSound = sound == "Custom" ? path : null;
        var data = Data(0x10);
        data.Info = message.Length > 255 ? message[..255] : message;
        data.Title = "绝区零 · 电量提醒";
        data.Timeout = 6000;
        data.InfoFlags = 1u | (sound is "Custom" or "Silent" ? 0x10u : 0u);
        var shown = ShellNotifyIcon(1, ref data);
        if (!shown) _pendingSound = null;
        return shown;
    }

    protected override void WndProc(ref Forms.Message message)
    {
        if (_taskbarCreated != 0 && (uint)message.Msg == _taskbarCreated) AddIcon();
        if (message.Msg == CallbackMessage)
        {
            switch (message.LParam.ToInt64())
            {
                case 0x202: Click?.Invoke(); break; // WM_LBUTTONUP
                case 0x205: // WM_RBUTTONUP
                    SetForegroundWindow(Handle);
                    ContextMenuStrip?.Show(Forms.Cursor.Position);
                    PostMessage(Handle, 0, IntPtr.Zero, IntPtr.Zero);
                    break;
                case 0x402: // NIN_BALLOONSHOW: respect Windows' decision to display the notification.
                    var path = _pendingSound; _pendingSound = null;
                    _sound.Stop();
                    if (path is not null && !_playSound(path))
                    { SystemSounds.Exclamation.Play(); SoundFailed?.Invoke(); }
                    break;
                // HIDE/TIMEOUT may belong to a previous notification while the next one is queued.
                case 0x405: BalloonClick?.Invoke(); break;
            }
        }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var data = Data(0);
        if (_added) ShellNotifyIcon(2, ref data);
        _added = false; _sound.Dispose(); ContextMenuStrip?.Dispose(); DestroyHandle(); _icon.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, Callback; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint operation, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
