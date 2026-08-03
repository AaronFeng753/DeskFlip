using System.Runtime.InteropServices;
using DeskFlip.Gesture;

namespace DeskFlip.Switch;

/// <summary>
/// Switches virtual desktops through the official Win+Ctrl+←/→ shortcut channel.
/// The full key sequence is injected in a single <c>SendInput</c> batch so
/// it is atomic: LWin↓ Ctrl↓ Arrow↓ Arrow↑ Ctrl↑ LWin↑. Batching guarantees the LWin
/// key-up never reaches the shell alone, which would pop the Start menu.
/// </summary>
public sealed class DesktopSwitcher
{
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_LCONTROL = 0xA2;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// Injects the shortcut for one desktop switch. Returns false when SendInput was
    /// blocked (e.g. an elevated foreground process / UIPI); the failure
    /// is silent by design.
    /// </summary>
    public bool Switch(SwitchDirection direction)
    {
        var arrow = direction == SwitchDirection.Left ? VK_LEFT : VK_RIGHT;
        var inputs = new[]
        {
            Key(VK_LWIN, 0),
            Key(VK_LCONTROL, 0),
            Key(arrow, KEYEVENTF_EXTENDEDKEY),
            Key(arrow, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP),
            Key(VK_LCONTROL, KEYEVENTF_KEYUP),
            Key(VK_LWIN, KEYEVENTF_KEYUP),
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        return sent == inputs.Length;
    }

    private static Input Key(ushort vk, uint flags) => new()
    {
        Type = INPUT_KEYBOARD,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = vk,
                ScanCode = 0,
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero,
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    // The INPUT union must match the largest member (MOUSEINPUT = 32 bytes on x64);
    // a short union makes cbSize wrong and SendInput fails with ERROR_INVALID_PARAMETER.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
}
