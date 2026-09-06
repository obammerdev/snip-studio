using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SnipStudio.Services;

public sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private int _id = 0x6100;
    private uint _modifiers, _key;
    private bool _registered;
    public event Action? Pressed;
    public bool Registered => _registered;
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    public HotkeyService(IntPtr handle)
    {
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("Window handle unavailable.");
        _source.AddHook(Hook);
    }
    public static bool IsValid(uint modifiers, uint key) => (modifiers & ~7u) == 0 && (modifiers & 2) != 0 && (modifiers & 5) != 0 && (key is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39 or >= 0x70 and <= 0x7A);
    public bool TrySet(uint modifiers, uint key, out string error)
    {
        error = "";
        if (!IsValid(modifiers, key)) { error = "Use Ctrl with Alt or Shift, and a letter, number, or F1–F11."; return false; }
        if (_registered && _modifiers == modifiers && _key == key) return true;
        int candidate = _id == 0x6100 ? 0x6101 : 0x6100;
        if (!RegisterHotKey(_source.Handle, candidate, modifiers | 0x4000, key))
        { error = "That shortcut is already in use. Choose another combination; your existing shortcut is unchanged."; return false; }
        if (_registered) UnregisterHotKey(_source.Handle, _id);
        _id = candidate; _modifiers = modifiers; _key = key; _registered = true;
        return true;
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == _id) { handled = true; Pressed?.Invoke(); }
        return IntPtr.Zero;
    }
    public static string Display(uint modifiers, uint key)
    {
        string prefix = ((modifiers & 2) != 0 ? "Ctrl + " : "") + ((modifiers & 1) != 0 ? "Alt + " : "") + ((modifiers & 4) != 0 ? "Shift + " : "");
        return prefix + KeyInterop.KeyFromVirtualKey((int)key).ToString().Replace("D0", "0").Replace("D1", "1").Replace("D2", "2").Replace("D3", "3").Replace("D4", "4").Replace("D5", "5").Replace("D6", "6").Replace("D7", "7").Replace("D8", "8").Replace("D9", "9");
    }
    public void Dispose() { if (_registered) UnregisterHotKey(_source.Handle, _id); _source.RemoveHook(Hook); _registered = false; }
}
