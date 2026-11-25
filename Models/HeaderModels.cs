namespace KubMailAnalyzer.Models;

/// <summary>Ein einzelnes (entfaltetes und dekodiertes) Header-Feld.</summary>
public sealed class HeaderField
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public string RawValue { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
}

/// <summary>Eine Station auf dem Zustellweg (aus einem Received-Header).</summary>
public sealed class ReceivedHop
{
    public int Hop { get; init; }
    public string From { get; init; } = "";
    public string By { get; init; } = "";
    public string With { get; init; } = "";
    public DateTimeOffset? Timestamp { get; init; }
    public TimeSpan? Delay { get; set; }
    public string Raw { get; init; } = "";

    public string TimestampText => Timestamp?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "–";

    public string DelayText => Delay switch
    {
        null => "–",
        { } d when d < TimeSpan.Zero => $"-{FormatSpan(d.Negate())}",
        { } d => FormatSpan(d)
    };

    internal static string FormatSpan(TimeSpan d) =>
        d.TotalSeconds < 60 ? $"{d.TotalSeconds:0} s"
        : d.TotalMinutes < 60 ? $"{(int)d.TotalMinutes} min {d.Seconds} s"
        : $"{(int)d.TotalHours} h {d.Minutes} min";
}

/// <summary>Ergebnis einer Authentifizierungsprüfung (SPF, DKIM, DMARC, ...).</summary>
public sealed class AuthResult
{
    public string Method { get; init; } = "";
    public string Result { get; init; } = "";
    public string Details { get; init; } = "";
    public string Source { get; init; } = "";

    public AuthStatus Status => Result.ToLowerInvariant() switch
    {
        "pass" => AuthStatus.Pass,
        "fail" or "permerror" or "hardfail" => AuthStatus.Fail,
        "softfail" or "temperror" or "neutral" or "policy" => AuthStatus.Warning,
        _ => AuthStatus.Unknown
    };
}

public enum AuthStatus { Pass, Warning, Fail, Unknown }

/// <summary>Gesamtergebnis der Analyse.</summary>
public sealed class HeaderAnalysis
{
    public List<HeaderField> Fields { get; } = new();
    public List<ReceivedHop> Hops { get; } = new();
    public List<AuthResult> AuthResults { get; } = new();

    public string? Get(string name) =>
        Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

    public TimeSpan? TotalTransitTime
    {
        get
        {
            var stamps = Hops.Where(h => h.Timestamp.HasValue).Select(h => h.Timestamp!.Value).ToList();
            return stamps.Count >= 2 ? stamps[^1] - stamps[0] : null;
        }
    }
}
