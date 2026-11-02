using Avalonia;
using System;
using System.Runtime.InteropServices;

namespace CraneControl.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Domyślny zegar Windows tyka co ~15,6 ms, co rozbijałoby cykl komunikacji 10 ms.
        var highResolutionTimer = OperatingSystem.IsWindows() && TimeBeginPeriod(1) == 0;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (highResolutionTimer)
            {
                TimeEndPeriod(1);
            }
        }
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint milliseconds);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
