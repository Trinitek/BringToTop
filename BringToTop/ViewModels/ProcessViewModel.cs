using ReactiveUI;
using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;
using Vanara.PInvoke;
using System.Reactive.Linq;

namespace BringToTop.ViewModels
{
    public class ProcessViewModel : UXViewModel
    {
        private Process _process;

        private int _processId;
        public int ProcessId
        {
            get => _processId;
            private set => this.RaiseAndSetIfChanged(ref _processId, value);
        }

        private ImageSource _icon;
        public ImageSource Icon
        {
            get => _icon;
            private set => this.RaiseAndSetIfChanged(ref _icon, value);
        }

        private string _windowName;
        public string WindowName
        {
            get => _windowName;
            private set => this.RaiseAndSetIfChanged(ref _windowName, value);
        }

        private string _processName;
        public string ProcessName
        {
            get => _processName;
            set => this.RaiseAndSetIfChanged(ref _processName, value);
        }

        private bool _alwaysOnTop;
        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set => this.RaiseAndSetIfChanged(ref _alwaysOnTop, value);
        }

        private readonly bool _isInitialized;

        public ProcessViewModel(Process process)
        {
            Load(process);

            this.WhenAnyValue(vm => vm.AlwaysOnTop)
                .Where(alwaysOnTop => _isInitialized)
                .Do(alwaysOnTop =>
                {
                    if (alwaysOnTop)
                    {
                        User32_Gdi.SetWindowPos(
                            hWnd: _process.MainWindowHandle,
                            hWndInsertAfter: User32_Gdi.SpecialWindowHandles.HWND_TOPMOST,
                            X: 0,
                            Y: 0,
                            cx: 0,
                            cy: 0,
                            uFlags: User32_Gdi.SetWindowPosFlags.SWP_NOMOVE | User32_Gdi.SetWindowPosFlags.SWP_NOSIZE);
                    }
                    else
                    {
                        User32_Gdi.SetWindowPos(
                            hWnd: _process.MainWindowHandle,
                            hWndInsertAfter: User32_Gdi.SpecialWindowHandles.HWND_NOTOPMOST,
                            X: 0,
                            Y: 0,
                            cx: 0,
                            cy: 0,
                            uFlags: User32_Gdi.SetWindowPosFlags.SWP_NOMOVE | User32_Gdi.SetWindowPosFlags.SWP_NOSIZE);
                    }
                })
                .Subscribe();

            _isInitialized = true;
        }

        public void Load(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            _process = process;

            WindowName = process.MainWindowTitle;
            ProcessId = process.Id;
            ProcessName = process.ProcessName;

            // TODO: Fix GetProcessHIcon -- will freeze if the target window does not respond to messages!
            HICON hIcon = HICON.NULL;// GetProcessHIcon(process);

            var icon = hIcon.ToIcon();

            if (!hIcon.IsNull)
            {
                using (var ms = new MemoryStream())
                {
                    var iconImageSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon: (IntPtr)hIcon,
                        sourceRect: new Int32Rect(0, 0, icon.Width, icon.Height),
                        sizeOptions: BitmapSizeOptions.FromEmptyOptions());

                    iconImageSource.Freeze();

                    Icon = iconImageSource;
                }
            }

            var windowStyle = (User32_Gdi.WindowStylesEx)User32_Gdi.GetWindowLong(
                hWnd: process.MainWindowHandle,
                nIndex: User32_Gdi.WindowLongFlags.GWL_EXSTYLE);

            AlwaysOnTop = (windowStyle & User32_Gdi.WindowStylesEx.WS_EX_TOPMOST) != 0;
        }

        private static HICON GetProcessHIcon(Process process)
        {
            try
            {
                HICON hIcon = default(IntPtr);

                // Get the main window's current icon

                hIcon = User32_Gdi.SendMessage(
                    hWnd: process.MainWindowHandle,
                    msg: 0x007f             /* WM_GETICON */,
                    wParam: new IntPtr(1)   /* ICON_BIG */,
                    lParam: IntPtr.Zero);

                if (hIcon.IsNull)
                {
                    // Fallback to icon defined by the window class

                    hIcon = User32_Gdi.GetClassLong(
                        hWnd: process.MainWindowHandle,
                        nIndex: -14         /* GCL_HICON */);
                }
                if (hIcon.IsNull)
                {
                    // Fallback to default application icon

                    hIcon = User32_Gdi.LoadImage(
                        hinst: process.MainWindowHandle,
                        lpszName: 0x7f00    /* IDI_APPLICATION */,
                        uType: User32_Gdi.LoadImageType.IMAGE_ICON,
                        cxDesired: 32,
                        cyDesired: 32,
                        fuLoad: User32_Gdi.LoadImageOptions.LR_SHARED);
                }

                return hIcon;
            }
            catch
            {
                return HICON.NULL;
            }
        }
    }
}
