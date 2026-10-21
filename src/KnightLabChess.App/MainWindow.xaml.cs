using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KnightLabChess.App.ViewModels;

namespace KnightLabChess.App;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyWindowsTitleBarTheme();
    }

    private void ApplyWindowsTitleBarTheme()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int trueValue = 1;
            // Native Windows 11 and Windows 10 (20H1+) dark title bar & system caption buttons
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, sizeof(int)) != 0)
            {
                // Fallback for earlier Windows 10 builds (1809 - 1909)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueValue, sizeof(int));
            }

            // In Windows 11 build 22000+, blend title bar with application theme background (#121417 -> BGR 0x00171412)
            int titleBarColor = 0x00171412;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref titleBarColor, sizeof(int));

            // Title text color: white
            int textColor = 0x00FFFFFF;
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
        }
        catch
        {
            // Graceful fallback if dwmapi is not available
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
