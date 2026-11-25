using KubMailAnalyzer.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KubMailAnalyzer;

/// <summary>Hilfsfunktionen für x:Bind-Funktionsbindungen und Badges.</summary>
public static class UiHelpers
{
    private static readonly SolidColorBrush PassBrush = new(Color.FromArgb(255, 16, 124, 16));
    private static readonly SolidColorBrush WarnBrush = new(Color.FromArgb(255, 202, 110, 0));
    private static readonly SolidColorBrush FailBrush = new(Color.FromArgb(255, 196, 43, 28));
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromArgb(255, 110, 110, 110));

    public static Brush StatusBrush(AuthStatus status) => status switch
    {
        AuthStatus.Pass => PassBrush,
        AuthStatus.Warning => WarnBrush,
        AuthStatus.Fail => FailBrush,
        _ => UnknownBrush
    };

    /// <summary>Färbt Verzögerungen: &gt; 5 min orange, &gt; 30 min oder negativ rot.</summary>
    public static Brush DelayBrush(TimeSpan? delay)
    {
        if (delay is not { } d)
            return (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        if (d < TimeSpan.Zero || d > TimeSpan.FromMinutes(30))
            return FailBrush;
        if (d > TimeSpan.FromMinutes(5))
            return WarnBrush;
        return (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
    }

    public static Border CreateBadge(string text, AuthStatus status) => new()
    {
        Background = StatusBrush(status),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 1, 8, 1),
        Child = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        }
    };
}
