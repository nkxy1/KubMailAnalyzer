using Microsoft.UI.Xaml;

namespace KubMailAnalyzer;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        _window = window;
        window.Activate();

        // Optional: Datei (z. B. .eml oder .txt) als Argument übergeben -> direkt analysieren
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Length > 1 && File.Exists(cmdArgs[1]))
            window.LoadAndAnalyze(File.ReadAllText(cmdArgs[1]));
    }
}
