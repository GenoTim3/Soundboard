using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Soundboard
{
    public class HotkeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static readonly Dictionary<string, uint> KeyCodes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Insert"] = 0x2D,
                ["Delete"] = 0x2E,
                ["Home"] = 0x24,
                ["End"] = 0x23,
                ["PageUp"] = 0x21,
                ["PageDown"] = 0x22,
                ["F1"] = 0x70,
                ["F2"] = 0x71,
                ["F3"] = 0x72,
                ["F4"] = 0x73,
                ["F5"] = 0x74,
                ["F6"] = 0x75,
                ["F7"] = 0x76,
                ["F8"] = 0x77,
            };

        private readonly IntPtr _handle;
        private readonly HwndSource _source;
        private readonly Dictionary<int, Action> _actions = new();
        private int _nextId = 9000;

        public HotkeyManager(Window window)
        {
            _handle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_handle)
                      ?? throw new InvalidOperationException("Window handle not ready.");
            _source.AddHook(Hook);
        }

        /// <summary>Accepts "Insert", "Ctrl+End", "Ctrl+Shift+F3", etc.</summary>
        public bool Register(string combo, Action action)
        {
            uint mods = MOD_NOREPEAT;
            string keyPart = combo;

            var parts = combo.Split('+', StringSplitOptions.TrimEntries);
            if (parts.Length > 1)
            {
                keyPart = parts[^1];
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    switch (parts[i].ToLowerInvariant())
                    {
                        case "ctrl" or "control": mods |= MOD_CONTROL; break;
                        case "alt": mods |= MOD_ALT; break;
                        case "shift": mods |= MOD_SHIFT; break;
                        default: return false;
                    }
                }
            }

            if (!KeyCodes.TryGetValue(keyPart, out var vk)) return false;

            int id = _nextId++;
            if (!RegisterHotKey(_handle, id, mods, vk)) return false;

            _actions[id] = action;
            return true;
        }

        public void Clear()
        {
            foreach (var id in _actions.Keys)
                UnregisterHotKey(_handle, id);
            _actions.Clear();
        }

        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
            {
                action();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Clear();
            _source.RemoveHook(Hook);
        }
    }
}