using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Runtime.InteropServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ManyControl.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        private static System.Threading.Mutex? _singleInstanceMutex;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            _singleInstanceMutex = new System.Threading.Mutex(true, "ManyControl_SingleInstance_Mutex_94A58739", out bool isNewInstance);
            if (!isNewInstance)
            {
                BringExistingInstanceToFront();
                Process.GetCurrentProcess().Kill();
                return;
            }

            this.InitializeComponent();
        }

        private static void BringExistingInstanceToFront()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var process = Process.GetProcessesByName(current.ProcessName)
                    .FirstOrDefault(p => p.Id != current.Id);

                if (process != null && process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, 9); // 9 = SW_RESTORE
                    SetForegroundWindow(process.MainWindowHandle);
                }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
