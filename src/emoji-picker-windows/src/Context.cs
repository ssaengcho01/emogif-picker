using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EmojiPicker
{
    // 입력 중인 칸에서 "커서 앞에 쓴 글"을 읽어 오기.
    // 1) 메모장·카카오톡 같은 일반 입력칸(Edit/RichEdit): Windows 메시지로 직접 읽기
    // 2) 그 밖의 앱(브라우저, 워드 등): Windows UI Automation(접근성 기능)으로 읽기
    // 읽을 수 없거나 비밀번호 칸이면 null → 일반 목록을 보여 줌
    static class Context
    {
        public const int MaxChars = 200;
        public static string LastFull; // 커서 앞 글 전체 (자동 꾸미기용, 줄바꿈 포함)

        // 단축키를 누른 순간 호출. 느린 앱 때문에 창이 늦게 뜨지 않도록 최대 timeoutMs만 기다림
        public static string ReadBeforeCaret(IntPtr foreground, int timeoutMs)
        {
            string result = null;
            var th = new Thread(() =>
            {
                try { result = Read(foreground); } catch { }
            });
            th.IsBackground = true;
            th.SetApartmentState(ApartmentState.MTA);
            th.Start();
            LastFull = null;
            if (!th.Join(timeoutMs)) return null;
            if (result != null) { var f = result.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd(); LastFull = f.Length > 5000 ? f.Substring(f.Length - 5000) : f; }
            return Clean(result);
        }

        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            s = s.Replace("\r\n", "\n").Replace('\r', '\n');
            // 마지막 줄(지금 쓰고 있는 문장) 위주로, 비어 있으면 그 앞 줄
            var lines = s.Split('\n');
            string last = null;
            for (int i = lines.Length - 1; i >= 0; i--)
                if (lines[i].Trim().Length > 0) { last = lines[i].Trim(); break; }
            if (last == null) return null;
            if (last.Length > MaxChars) last = last.Substring(last.Length - MaxChars);
            return last;
        }

        static string Read(IntPtr fg)
        {
            var focus = Native.FocusedHandle(fg);
            if (focus != IntPtr.Zero)
            {
                var cls = Native.ClassName(focus).ToLowerInvariant();
                if (cls == "edit" || cls.Contains("richedit") || cls.Contains(".edit."))
                {
                    if (Native.IsPasswordEdit(focus)) return null;
                    var t = Native.EditTextBeforeCaret(focus);
                    if (t != null) return t;
                }
            }
            return Uia.TextBeforeCaret();
        }
    }

    // ---------- UI Automation (COM) ----------
    // .NET의 UIAutomationClient.dll 없이 Windows에 기본으로 있는 COM 인터페이스를 직접 사용.
    // 필요한 함수까지만 순서대로 선언 (_ 로 시작하는 것은 쓰지 않는 자리).
    static class Uia
    {
        const int UIA_TextPatternId = 10014, UIA_ValuePatternId = 10002, UIA_IsPasswordPropertyId = 30019;
        const int Endpoint_Start = 0, Endpoint_End = 1, TextUnit_Character = 0;

        public static string TextBeforeCaret()
        {
            var type = Type.GetTypeFromCLSID(new Guid("ff48dba4-60ef-4201-aa87-54103eef594e"));
            var auto = (IUIAutomation)Activator.CreateInstance(type);
            var el = auto.GetFocusedElement();
            if (el == null) return null;
            try
            {
                var pw = el.GetCurrentPropertyValue(UIA_IsPasswordPropertyId);
                if (pw is bool && (bool)pw) return null;
            }
            catch { }

            try
            {
                var tp = el.GetCurrentPattern(UIA_TextPatternId) as IUIAutomationTextPattern;
                if (tp != null)
                {
                    var sel = tp.GetSelection();
                    if (sel != null && sel.Length > 0)
                    {
                        var caret = sel.GetElement(0);
                        var r = caret.Clone();
                        r.MoveEndpointByRange(Endpoint_End, caret, Endpoint_Start); // 선택 영역 앞쪽 끝 = 커서
                        r.MoveEndpointByUnit(Endpoint_Start, TextUnit_Character, -Context.MaxChars * 2);
                        var text = r.GetText(Context.MaxChars * 4);
                        if (!string.IsNullOrEmpty(text)) return text;
                    }
                }
            }
            catch { }

            try
            {
                // 커서 위치는 몰라도 입력칸 전체 내용은 읽을 수 있는 경우 (대개 끝에서 쓰는 중)
                var vp = el.GetCurrentPattern(UIA_ValuePatternId) as IUIAutomationValuePattern;
                if (vp != null)
                {
                    var v = vp.CurrentValue;
                    if (!string.IsNullOrEmpty(v) && v.Length < 20000) return v;
                }
            }
            catch { }
            return null;
        }

        [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IUIAutomation
        {
            void _CompareElements();
            void _CompareRuntimeIds();
            void _GetRootElement();
            void _ElementFromHandle();
            void _ElementFromPoint();
            IUIAutomationElement GetFocusedElement();
        }

        [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IUIAutomationElement
        {
            void _SetFocus();
            void _GetRuntimeId();
            void _FindFirst();
            void _FindAll();
            void _FindFirstBuildCache();
            void _FindAllBuildCache();
            void _BuildUpdatedCache();
            [return: MarshalAs(UnmanagedType.Struct)] object GetCurrentPropertyValue(int propertyId);
            void _GetCurrentPropertyValueEx();
            void _GetCachedPropertyValue();
            void _GetCachedPropertyValueEx();
            void _GetCurrentPatternAs();
            void _GetCachedPatternAs();
            [return: MarshalAs(UnmanagedType.IUnknown)] object GetCurrentPattern(int patternId);
        }

        [ComImport, Guid("32eba289-3583-42c9-9c59-3b6d9a1e9b6a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IUIAutomationTextPattern
        {
            void _RangeFromPoint();
            void _RangeFromChild();
            IUIAutomationTextRangeArray GetSelection();
        }

        [ComImport, Guid("ce4ae76a-e717-4c98-81ea-47371d028eb6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IUIAutomationTextRangeArray
        {
            int Length { get; }
            IUIAutomationTextRange GetElement(int index);
        }

        [ComImport, Guid("a543cc6a-f4ae-494b-8239-c814481187fa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IUIAutomationTextRange
        {
            IUIAutomationTextRange Clone();
            void _Compare();
            void _CompareEndpoints();
            void _ExpandToEnclosingUnit();
            void _FindAttribute();
            void _FindText();
            void _GetAttributeValue();
            void _GetBoundingRectangles();
            void _GetEnclosingElement();
            [return: MarshalAs(UnmanagedType.BStr)] string GetText(int maxLength);
            void _Move();
            int MoveEndpointByUnit(int endpoint, int unit, int count);
            void MoveEndpointByRange(int srcEndPoint, IUIAutomationTextRange range, int targetEndPoint);
        }

        [ComImport, Guid("a94cd8b1-0844-4cd6-9d2d-640537ab39e9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IUIAutomationValuePattern
        {
            void _SetValue();
            string CurrentValue { [return: MarshalAs(UnmanagedType.BStr)] get; }
        }
    }
}
