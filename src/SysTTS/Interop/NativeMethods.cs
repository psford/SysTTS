using System.Runtime.InteropServices;

namespace SysTTS.Interop;

/// <summary>
/// Win32 P/Invoke declarations for keyboard hooks and input simulation.
/// Uses nint for pointer types (.NET 8 convention).
/// </summary>
public static class NativeMethods
{
    // ==================== Hook Functions ====================

    /// <summary>
    /// Installs an application-defined hook procedure into a hook chain.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    /// <summary>
    /// Removes a hook procedure previously installed in a hook chain.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    /// <summary>
    /// Passes the hook information to the next hook procedure in the current hook chain.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    /// <summary>
    /// Retrieves a module handle for the specified module.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern nint GetModuleHandle(string? lpModuleName);

    // ==================== Input Simulation ====================

    /// <summary>
    /// Synthesizes keystrokes, mouse motions, and button clicks.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Retrieves the status of the specified virtual key.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    // ==================== Delegates ====================

    /// <summary>
    /// Application-defined callback function used with SetWindowsHookEx.
    /// Processes low-level keyboard input events.
    /// </summary>
    public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    // ==================== Structures ====================

    /// <summary>
    /// Contains information about a low-level keyboard input event.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        /// <summary>
        /// The virtual-key code. The value is between 1 and 254.
        /// </summary>
        public uint vkCode;

        /// <summary>
        /// The hardware scan code for the key.
        /// </summary>
        public uint scanCode;

        /// <summary>
        /// The extended-key flag, event-injected flags, context code, and transition-state flag.
        /// </summary>
        public uint flags;

        /// <summary>
        /// The time stamp for this message, equivalent to the time the input event was generated.
        /// </summary>
        public uint time;

        /// <summary>
        /// Additional information associated with the message.
        /// </summary>
        public nint dwExtraInfo;
    }

    /// <summary>
    /// Contains information about an input event.
    /// Uses explicit layout for union-like behavior.
    /// Size must be 40 bytes on x64 (type + 4 padding + ki[32 bytes] = 40).
    /// Win32 SendInput validates cbSize and silently fails if size doesn't match.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT
    {
        /// <summary>
        /// The type of input event.
        /// </summary>
        [FieldOffset(0)]
        public uint type;

        /// <summary>
        /// The information about a keyboard input event.
        /// </summary>
        [FieldOffset(8)]
        public KEYBDINPUT ki;
    }

    /// <summary>
    /// Contains information about a simulated keyboard event.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        /// <summary>
        /// A virtual-key code. The code is a value between 1 and 254.
        /// </summary>
        public ushort wVk;

        /// <summary>
        /// A hardware scan code for the key.
        /// </summary>
        public ushort wScan;

        /// <summary>
        /// Specifies various aspects of a keystroke.
        /// </summary>
        public uint dwFlags;

        /// <summary>
        /// The time stamp for the input in milliseconds.
        /// If this value is zero, the system will provide its own time stamp.
        /// </summary>
        public uint time;

        /// <summary>
        /// An additional value associated with the keystroke.
        /// </summary>
        public nint dwExtraInfo;
    }

    // ==================== Constants ====================

    // Hook IDs
    /// <summary>
    /// Installs a hook procedure that monitors low-level keyboard input events.
    /// </summary>
    public const int WH_KEYBOARD_LL = 13;

    // Window Messages
    /// <summary>
    /// Posted to the window with the keyboard focus when a nonsystem key is pressed or released.
    /// </summary>
    public const int WM_KEYDOWN = 0x0100;

    /// <summary>
    /// Posted to the window with the keyboard focus when a system key is pressed or released.
    /// </summary>
    public const int WM_SYSKEYDOWN = 0x0104;

    // Input Types
    /// <summary>
    /// The event is a keyboard event. Use the ki structure of the union.
    /// </summary>
    public const uint INPUT_KEYBOARD = 1;

    // Keyboard Event Flags
    /// <summary>
    /// If specified, the scan code was preceded by a prefix byte that has the value 0xE0 (224).
    /// </summary>
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    /// <summary>
    /// If specified, the key is being released. If not specified, the key is being pressed.
    /// </summary>
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // Virtual Key Codes
    /// <summary>
    /// CTRL key.
    /// </summary>
    public const int VK_CONTROL = 0x11;

    /// <summary>
    /// ALT key.
    /// </summary>
    public const int VK_ALT = 0x12;

    /// <summary>
    /// SHIFT key.
    /// </summary>
    public const int VK_SHIFT = 0x10;

    /// <summary>
    /// C key (ASCII 0x43).
    /// </summary>
    public const int VK_C = 0x43;

    /// <summary>
    /// F22 key (virtual key code 0x85).
    /// </summary>
    public const int VK_F22 = 0x85;

    /// <summary>
    /// F23 key (virtual key code 0x86).
    /// </summary>
    public const int VK_F23 = 0x86;

    /// <summary>
    /// F24 key (virtual key code 0x87).
    /// </summary>
    public const int VK_F24 = 0x87;

    /// <summary>
    /// F1 key.
    /// </summary>
    public const int VK_F1 = 0x70;

    /// <summary>
    /// F2 key.
    /// </summary>
    public const int VK_F2 = 0x71;

    /// <summary>
    /// F3 key.
    /// </summary>
    public const int VK_F3 = 0x72;

    /// <summary>
    /// F4 key.
    /// </summary>
    public const int VK_F4 = 0x73;

    /// <summary>
    /// F5 key.
    /// </summary>
    public const int VK_F5 = 0x74;

    /// <summary>
    /// F6 key.
    /// </summary>
    public const int VK_F6 = 0x75;

    /// <summary>
    /// F7 key.
    /// </summary>
    public const int VK_F7 = 0x76;

    /// <summary>
    /// F8 key.
    /// </summary>
    public const int VK_F8 = 0x77;

    /// <summary>
    /// F9 key.
    /// </summary>
    public const int VK_F9 = 0x78;

    /// <summary>
    /// F10 key.
    /// </summary>
    public const int VK_F10 = 0x79;

    /// <summary>
    /// F11 key.
    /// </summary>
    public const int VK_F11 = 0x7A;

    /// <summary>
    /// F12 key.
    /// </summary>
    public const int VK_F12 = 0x7B;

    /// <summary>
    /// F13 key.
    /// </summary>
    public const int VK_F13 = 0x7C;

    /// <summary>
    /// F14 key.
    /// </summary>
    public const int VK_F14 = 0x7D;

    /// <summary>
    /// F15 key.
    /// </summary>
    public const int VK_F15 = 0x7E;

    /// <summary>
    /// F16 key.
    /// </summary>
    public const int VK_F16 = 0x7F;

    /// <summary>
    /// F17 key.
    /// </summary>
    public const int VK_F17 = 0x80;

    /// <summary>
    /// F18 key.
    /// </summary>
    public const int VK_F18 = 0x81;

    /// <summary>
    /// F19 key.
    /// </summary>
    public const int VK_F19 = 0x82;

    /// <summary>
    /// F20 key.
    /// </summary>
    public const int VK_F20 = 0x83;

    /// <summary>
    /// F21 key.
    /// </summary>
    public const int VK_F21 = 0x84;
}
