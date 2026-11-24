using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace EmojiPicker
{
    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_CONTROL = 0x2, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
        public const uint VK_OEM_PERIOD = 0xBE;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);

        // 한글 문자열이 깨지지 않도록 유니코드(W) 버전 사용
        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint n, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct GUITHREADINFO
        {
            public int cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion U; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4;

        static INPUT Key(ushort vk, ushort scan, uint flags)
        {
            var i = new INPUT { type = INPUT_KEYBOARD };
            i.U.ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags };
            return i;
        }

        // 아직 눌려 있는 Ctrl/Win/Shift/Alt를 떼어 주기 (단축키 직후 입력이 단축키로 해석되는 것 방지)
        static void ReleaseModifiers(List<INPUT> list)
        {
            foreach (ushort vk in new ushort[] { 0x11, 0x10, 0x12, 0x5B, 0x5C })
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) list.Add(Key(vk, 0, KEYEVENTF_KEYUP));
        }

        // 유니코드 문자 그대로 입력 (이모지 = UTF-16 서로게이트 쌍도 그대로 전달)
        public static bool TypeText(string text)
        {
            var list = new List<INPUT>();
            ReleaseModifiers(list);
            foreach (char c in text)
            {
                list.Add(Key(0, c, KEYEVENTF_UNICODE));
                list.Add(Key(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
            }
            var arr = list.ToArray();
            return SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT))) == arr.Length;
        }

        public static void PressCtrlA()
        {
            var list = new List<INPUT>();
            ReleaseModifiers(list);
            list.Add(Key(0x11, 0, 0));
            list.Add(Key(0x41, 0, 0));
            list.Add(Key(0x41, 0, KEYEVENTF_KEYUP));
            list.Add(Key(0x11, 0, KEYEVENTF_KEYUP));
            var arr = list.ToArray();
            SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void PressCtrlV()
        {
            var list = new List<INPUT>();
            ReleaseModifiers(list);
            list.Add(Key(0x11, 0, 0));
            list.Add(Key(0x56, 0, 0));
            list.Add(Key(0x56, 0, KEYEVENTF_KEYUP));
            list.Add(Key(0x11, 0, KEYEVENTF_KEYUP));
            var arr = list.ToArray();
            SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        // 지금 입력 중인 창의 캐럿(커서) 화면 좌표. 알 수 없으면 null
        public static Point? CaretPosition(IntPtr hwnd)
        {
            uint pid;
            uint tid = GetWindowThreadProcessId(hwnd, out pid);
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
            if (!GetGUIThreadInfo(tid, ref info) || info.hwndCaret == IntPtr.Zero) return null;
            var r = info.rcCaret;
            if (r.Right == 0 && r.Bottom == 0 && r.Left == 0 && r.Top == 0) return null;
            var p = new POINT { X = r.Left, Y = r.Bottom };
            if (!ClientToScreen(info.hwndCaret, ref p)) return null;
            return new Point(p.X, p.Y);
        }

        // 입력 중이던 창으로 확실하게 포커스 되돌리기
        public static void FocusWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
            uint pid;
            uint target = GetWindowThreadProcessId(hwnd, out pid);
            uint me = GetCurrentThreadId();
            bool attached = target != me && AttachThreadInput(me, target, true);
            SetForegroundWindow(hwnd);
            if (attached) AttachThreadInput(me, target, false);
        }

        // ---------- 커서 앞 글 읽기용 ----------
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder buf, int max);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout, out IntPtr result);

        [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessageTimeoutText(IntPtr hWnd, int msg, IntPtr wParam, System.Text.StringBuilder lParam, int flags, int timeout, out IntPtr result);

        const int SMTO_ABORTIFHUNG = 0x2;

        // 그 창 안에서 실제로 키보드 입력을 받고 있는 칸
        public static IntPtr FocusedHandle(IntPtr hwnd)
        {
            uint pid;
            uint tid = GetWindowThreadProcessId(hwnd, out pid);
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
            if (!GetGUIThreadInfo(tid, ref info)) return IntPtr.Zero;
            return info.hwndFocus;
        }

        public static string ClassName(IntPtr hwnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool IsPasswordEdit(IntPtr hwnd)
        {
            return (GetWindowLong(hwnd, -16) & 0x20) != 0; // ES_PASSWORD
        }

        // 일반 입력칸(Edit/RichEdit)의 글 중 커서 앞부분
        public static string EditTextBeforeCaret(IntPtr hwnd)
        {
            IntPtr len;
            if (SendMessageTimeout(hwnd, 0x000E, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 200, out len) == IntPtr.Zero) return null; // WM_GETTEXTLENGTH
            int n = (int)len;
            if (n <= 0) return "";
            if (n > 200000) return null;
            var sb = new System.Text.StringBuilder(n + 2);
            IntPtr got;
            if (SendMessageTimeoutText(hwnd, 0x000D, (IntPtr)(n + 1), sb, SMTO_ABORTIFHUNG, 200, out got) == IntPtr.Zero) return null; // WM_GETTEXT
            var text = sb.ToString();
            IntPtr sel;
            if (SendMessageTimeout(hwnd, 0x00B0, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 200, out sel) != IntPtr.Zero) // EM_GETSEL
            {
                long v = (long)sel;
                if (v != -1)
                {
                    int start = (int)(v & 0xFFFF);
                    // 글이 아주 길면 위치값이 잘리므로 그때는 전체 글 끝을 사용
                    if (text.Length < 0xFFFF && start <= text.Length) text = text.Substring(0, start);
                }
            }
            return text;
        }

        public static void RoundCorners(IntPtr hwnd)
        {
            try
            {
                int pref = 2; // DWMWCP_ROUND (Windows 11)
                DwmSetWindowAttribute(hwnd, 33, ref pref, 4);
            }
            catch { }
        }
    }
}
