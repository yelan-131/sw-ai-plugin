using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Interop;
using SolidWorks.Interop.sldworks;
using SwComAddin.Views;

namespace SwComAddin
{
    /// <summary>
    /// SolidWorks TaskPane WPF keyboard input fix.
    ///
    /// Root cause: SolidWorks sends WM_KILLFOCUS to the WPF HWND,
    /// causing HwndSource to discard WM_CHAR. Also, UpdateSourceTrigger=PropertyChanged
    /// binding causes CaretIndex reset on every Text change.
    ///
    /// Fix:
    /// - XAML: use LostFocus binding (default) instead of PropertyChanged
    /// - WH_GETMESSAGE hook redirects keyboard messages to WPF HWND
    /// - WM_CHAR is intercepted: text is computed and set synchronously
    /// - After text change, CaretIndex is set immediately (no binding round-trip)
    /// </summary>
    public class SwTaskPaneControl : System.Windows.Forms.UserControl
    {
        private readonly ISldWorks _swApp;
        private readonly Services.SwConnector _connector;
        private readonly ElementHost _elementHost;
        private IntPtr _wpfHwnd;
        private IntPtr _msgHook;
        private GetMsgProc _hookProc;

        private const int WH_GETMESSAGE = 3;
        private const int WM_KEYFIRST = 0x0100;
        private const int WM_KEYLAST = 0x0109;
        private const int WM_CHAR = 0x0102;
        private const int WM_NULL = 0x0000;
        private const int PM_REMOVE = 0x0001;

        private delegate IntPtr GetMsgProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NATIVE_MSG
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, GetMsgProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public SwTaskPaneControl(ISldWorks swApp)
        {
            _swApp = swApp;
            _connector = new Services.SwConnector(swApp);

            var mainView = new MainTaskPaneView(_connector);

            _elementHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = mainView,
                TabIndex = 0
            };

            Controls.Add(_elementHost);
            HandleCreated += OnHandleCreated;
        }

        private void OnHandleCreated(object sender, EventArgs e)
        {
            CaptureWpfHwnd();
            _hookProc = HookCallback;
            _msgHook = SetWindowsHookEx(WH_GETMESSAGE, _hookProc,
                IntPtr.Zero, GetCurrentThreadId());
        }

        private void CaptureWpfHwnd()
        {
            try
            {
                if (_elementHost?.Child != null)
                {
                    var source = PresentationSource.FromVisual(_elementHost.Child) as HwndSource;
                    if (source != null)
                        _wpfHwnd = source.Handle;
                }
            }
            catch { }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == PM_REMOVE)
            {
                try
                {
                    // Peek message field only (offset 8 on x64, after IntPtr hwnd),
                    // avoiding full Marshal.PtrToStructure on every SW message.
                    int msgId = Marshal.ReadInt32(lParam, 8);

                    if (msgId >= WM_KEYFIRST && msgId <= WM_KEYLAST)
                    {
                        var msg = (NATIVE_MSG)Marshal.PtrToStructure(lParam, typeof(NATIVE_MSG));

                        if (_wpfHwnd == IntPtr.Zero)
                            CaptureWpfHwnd();

                        var focused = Keyboard.FocusedElement;
                        bool isInput = focused is System.Windows.Controls.TextBox
                            || focused is PasswordBox;

                        if (isInput && _wpfHwnd != IntPtr.Zero && msg.hwnd != _wpfHwnd)
                        {
                            msg.hwnd = _wpfHwnd;
                            Marshal.StructureToPtr(msg, lParam, false);
                        }

                        if (msgId == WM_CHAR && isInput && msg.hwnd == _wpfHwnd)
                        {
                            int charCode = msg.wParam.ToInt32();
                            if (charCode >= 0x20)
                            {
                                char c = (char)charCode;

                                if (focused is System.Windows.Controls.TextBox tb)
                                {
                                    string text = tb.Text;
                                    int ci = tb.CaretIndex;
                                    int sl = tb.SelectionLength;

                                    if (sl > 0)
                                    {
                                        int selStart = ci;
                                        if (ci + sl > text.Length && ci - sl >= 0)
                                            selStart = ci - sl;
                                        text = text.Remove(selStart, sl);
                                        ci = selStart;
                                    }

                                    text = text.Insert(ci, c.ToString());
                                    tb.Text = text;
                                    tb.CaretIndex = ci + 1;
                                    tb.SelectionLength = 0;
                                }
                                else if (focused is PasswordBox pb)
                                {
                                    pb.Password += c;
                                }

                                msg.message = WM_NULL;
                                Marshal.StructureToPtr(msg, lParam, false);
                            }
                        }
                    }
                }
                catch { }
            }
            return CallNextHookEx(_msgHook, nCode, wParam, lParam);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (_elementHost?.Child != null)
            {
                if (_wpfHwnd == IntPtr.Zero)
                    CaptureWpfHwnd();
                _elementHost.Focus();
                Keyboard.Focus(_elementHost.Child);
                if (_wpfHwnd != IntPtr.Zero)
                {
                    try { SetFocus(_wpfHwnd); } catch { }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _msgHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_msgHook);
                _msgHook = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }
    }
}
