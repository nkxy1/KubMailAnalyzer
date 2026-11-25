using System.Collections.ObjectModel;
using System.Text;
using KubMailAnalyzer.Models;
using KubMailAnalyzer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace KubMailAnalyzer;

public sealed partial class MainWindow : Window
{
    private HeaderAnalysis _analysis = new();
    private readonly ObservableCollection<HeaderField> _fields = new();
    private readonly ObservableCollection<ReceivedHop> _hops = new();
    private readonly ObservableCollection<AuthResult> _auth = new();

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 950));

        FieldsList.ItemsSource = _fields;
        HopsList.ItemsSource = _hops;
        AuthList.ItemsSource = _auth;
    }

    // ---------------------------------------------------------------- Aktionen

    private void Analyze_Click(object sender, RoutedEventArgs e) => Analyze();

    public void LoadAndAnalyze(string text)
    {
        HeaderInput.Text = text;
        Analyze();
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
            return;
        HeaderInput.Text = await content.GetTextAsync();
        Analyze();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        HeaderInput.Text = "";
        FilterBox.Text = "";
        _analysis = new HeaderAnalysis();
        _fields.Clear();
        _hops.Clear();
        _auth.Clear();
        SummaryPanel.Visibility = Visibility.Collapsed;
    }

    private void Analyze()
    {
        _analysis = MailHeaderParser.Parse(HeaderInput.Text);

        ApplyFieldFilter();
        Fill(_hops, _analysis.Hops);
        Fill(_auth, _analysis.AuthResults);
        UpdateSummary();

        if (_analysis.Fields.Count == 0)
            _ = ShowMessageAsync("Keine Header-Felder gefunden",
                "Der eingefügte Text enthält keine erkennbaren Header-Felder im Format „Name: Wert“.");
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFieldFilter();

    private void ApplyFieldFilter()
    {
        var filter = FilterBox.Text.Trim();
        var items = string.IsNullOrEmpty(filter)
            ? _analysis.Fields
            : _analysis.Fields.Where(f =>
                f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.Value.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.Category.Contains(filter, StringComparison.OrdinalIgnoreCase));
        Fill(_fields, items);
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private void ViewSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var selected = sender.SelectedItem;
        FieldsView.Visibility = selected == TabFields ? Visibility.Visible : Visibility.Collapsed;
        HopsView.Visibility = selected == TabHops ? Visibility.Visible : Visibility.Collapsed;
        AuthView.Visibility = selected == TabAuth ? Visibility.Visible : Visibility.Collapsed;
        FilterBox.Visibility = selected == TabFields ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CopyTable_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        var selected = ViewSelector.SelectedItem;

        if (selected == TabHops)
        {
            sb.AppendLine("Hop\tVon\tDurch\tProtokoll\tZeitpunkt\tVerzögerung");
            foreach (var h in _hops)
                sb.AppendLine(string.Join('\t', h.Hop, Tsv(h.From), Tsv(h.By), Tsv(h.With), h.TimestampText, h.DelayText));
        }
        else if (selected == TabAuth)
        {
            sb.AppendLine("Methode\tErgebnis\tDetails\tQuelle");
            foreach (var a in _auth)
                sb.AppendLine(string.Join('\t', a.Method, a.Result, Tsv(a.Details), Tsv(a.Source)));
        }
        else
        {
            sb.AppendLine("#\tFeld\tWert\tKategorie\tBeschreibung");
            foreach (var f in _fields)
                sb.AppendLine(string.Join('\t', f.Index, f.Name, Tsv(f.Value), f.Category, f.Description));
        }

        var package = new DataPackage();
        package.SetText(sb.ToString());
        Clipboard.SetContent(package);
    }

    private static string Tsv(string s) => s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

    // ---------------------------------------------------------------- Zusammenfassung

    private void UpdateSummary()
    {
        if (_analysis.Fields.Count == 0)
        {
            SummaryPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SummaryPanel.Visibility = Visibility.Visible;
        SumFrom.Text = _analysis.Get("From") ?? "–";
        SumTo.Text = _analysis.Get("To") ?? "–";
        SumSubject.Text = _analysis.Get("Subject") ?? "–";
        SumReturnPath.Text = _analysis.Get("Return-Path") ?? "–";

        var date = MailHeaderParser.ParseDate(_analysis.Get("Date"));
        SumDate.Text = date?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? _analysis.Get("Date") ?? "–";

        var transit = _analysis.TotalTransitTime;
        SumHops.Text = _analysis.Hops.Count == 0
            ? "–"
            : transit.HasValue
                ? $"{_analysis.Hops.Count} (Gesamtlaufzeit {ReceivedHop.FormatSpan(transit.Value.Duration())})"
                : _analysis.Hops.Count.ToString();
        SumFields.Text = _analysis.Fields.Count.ToString();

        SumAuth.Children.Clear();
        foreach (var method in new[] { "SPF", "DKIM", "DMARC" })
        {
            // Bestes Ergebnis anzeigen, da mehrere Prüfinstanzen beteiligt sein können
            var result = _analysis.AuthResults
                .Where(a => a.Method == method)
                .OrderBy(a => a.Status)
                .FirstOrDefault();
            SumAuth.Children.Add(UiHelpers.CreateBadge($"{method}: {result?.Result ?? "–"}",
                result?.Status ?? AuthStatus.Unknown));
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
