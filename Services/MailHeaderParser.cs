using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KubMailAnalyzer.Models;

namespace KubMailAnalyzer.Services;

/// <summary>
/// Zerlegt einen E-Mail-Header (RFC 5322) in seine Felder, dekodiert
/// RFC-2047-Wörter und wertet Received- sowie Authentifizierungs-Header aus.
/// </summary>
public static partial class MailHeaderParser
{
    static MailHeaderParser()
    {
        // Für ISO-8859-x, Windows-125x usw. in kodierten Wörtern
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static HeaderAnalysis Parse(string input)
    {
        var analysis = new HeaderAnalysis();
        if (string.IsNullOrWhiteSpace(input))
            return analysis;

        var index = 0;
        foreach (var (name, raw) in UnfoldFields(input))
        {
            var (category, description) = HeaderDescriptions.Describe(name);
            analysis.Fields.Add(new HeaderField
            {
                Index = ++index,
                Name = name,
                RawValue = raw,
                Value = DecodeEncodedWords(raw),
                Category = category,
                Description = description
            });
        }

        ParseReceived(analysis);
        ParseAuthentication(analysis);
        return analysis;
    }

    // ---------------------------------------------------------------- Entfalten

    private static IEnumerable<(string Name, string Value)> UnfoldFields(string input)
    {
        var lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string? currentName = null;
        var currentValue = new StringBuilder();
        var started = false;

        foreach (var line in lines)
        {
            if (line.Length == 0 || line.Trim().Length == 0)
            {
                // Leerzeile nach dem Header = Beginn des Bodys -> Ende
                if (started) break;
                continue;
            }

            var isContinuation = line[0] == ' ' || line[0] == '\t';
            var match = isContinuation ? Match.Empty : FieldLineRegex().Match(line);

            if (match.Success)
            {
                if (currentName != null)
                    yield return (currentName, Normalize(currentValue.ToString()));

                started = true;
                currentName = match.Groups["name"].Value.Trim();
                currentValue.Clear().Append(match.Groups["value"].Value);
            }
            else if (currentName != null)
            {
                // Fortsetzungszeile (oder fehlerhaft umbrochene Zeile)
                currentValue.Append(' ').Append(line.Trim());
            }
        }

        if (currentName != null)
            yield return (currentName, Normalize(currentValue.ToString()));
    }

    private static string Normalize(string value) => MultiSpaceRegex().Replace(value, " ").Trim();

    // ---------------------------------------------------------------- RFC 2047

    public static string DecodeEncodedWords(string value)
    {
        if (!value.Contains("=?", StringComparison.Ordinal))
            return value;

        // Leerraum zwischen zwei kodierten Wörtern wird laut RFC 2047 entfernt
        value = BetweenEncodedWordsRegex().Replace(value, "");

        // Aufeinanderfolgende Wörter mit gleichem Zeichensatz gemeinsam dekodieren,
        // damit über Wortgrenzen geteilte Multibyte-Zeichen korrekt bleiben.
        var sb = new StringBuilder();
        var pos = 0;
        string? pendingCharset = null;
        var pendingBytes = new List<byte>();

        void Flush()
        {
            if (pendingCharset == null) return;
            sb.Append(GetEncoding(pendingCharset).GetString(pendingBytes.ToArray()));
            pendingBytes.Clear();
            pendingCharset = null;
        }

        foreach (Match m in EncodedWordRegex().Matches(value))
        {
            if (m.Index > pos)
            {
                Flush();
                sb.Append(value, pos, m.Index - pos);
            }

            var charset = m.Groups["charset"].Value.Split('*')[0]; // RFC 2231 Sprachsuffix
            var bytes = m.Groups["enc"].Value.Equals("B", StringComparison.OrdinalIgnoreCase)
                ? DecodeBase64(m.Groups["text"].Value)
                : DecodeQ(m.Groups["text"].Value);

            if (bytes == null)
            {
                Flush();
                sb.Append(m.Value);
            }
            else
            {
                if (pendingCharset != null && !pendingCharset.Equals(charset, StringComparison.OrdinalIgnoreCase))
                    Flush();
                pendingCharset = charset;
                pendingBytes.AddRange(bytes);
            }

            pos = m.Index + m.Length;
        }

        Flush();
        if (pos < value.Length)
            sb.Append(value, pos, value.Length - pos);
        return sb.ToString();
    }

    private static byte[]? DecodeBase64(string text)
    {
        try
        {
            var padded = text.PadRight(text.Length + (4 - text.Length % 4) % 4, '=');
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[] DecodeQ(string text)
    {
        var bytes = new List<byte>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '_')
                bytes.Add((byte)' ');
            else if (c == '=' && i + 2 < text.Length &&
                     byte.TryParse(text.AsSpan(i + 1, 2), NumberStyles.HexNumber, null, out var b))
            {
                bytes.Add(b);
                i += 2;
            }
            else
                bytes.Add((byte)c);
        }
        return bytes.ToArray();
    }

    private static Encoding GetEncoding(string charset)
    {
        try { return Encoding.GetEncoding(charset); }
        catch (ArgumentException) { return Encoding.UTF8; }
    }

    // ---------------------------------------------------------------- Received

    private static void ParseReceived(HeaderAnalysis analysis)
    {
        // Received-Header stehen in umgekehrter Reihenfolge (neueste zuerst)
        var received = analysis.Fields
            .Where(f => f.Name.Equals("Received", StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .ToList();

        var hopNo = 0;
        DateTimeOffset? previous = null;
        foreach (var field in received)
        {
            var value = field.Value;
            var semicolon = value.LastIndexOf(';');
            var datePart = semicolon >= 0 ? value[(semicolon + 1)..] : "";
            var mainPart = semicolon >= 0 ? value[..semicolon] : value;

            var hop = new ReceivedHop
            {
                Hop = ++hopNo,
                From = ExtractClause(mainPart, "from"),
                By = ExtractClause(mainPart, "by"),
                With = ExtractClause(mainPart, "with"),
                Timestamp = ParseDate(datePart),
                Raw = value
            };

            if (hop.Timestamp.HasValue && previous.HasValue)
                hop.Delay = hop.Timestamp.Value - previous.Value;
            if (hop.Timestamp.HasValue)
                previous = hop.Timestamp;

            analysis.Hops.Add(hop);
        }
    }

    private static string ExtractClause(string text, string keyword)
    {
        var m = Regex.Match(text,
            $@"(?:^|\s){keyword}\s+(?<val>.*?)(?=\s+(?:from|by|via|with|id|for)\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? m.Groups["val"].Value.Trim() : "";
    }

    private static readonly string[] DateFormats =
    {
        "ddd, d MMM yyyy H:mm:ss zzz",
        "d MMM yyyy H:mm:ss zzz",
        "ddd, d MMM yyyy H:mm zzz",
        "d MMM yyyy H:mm zzz",
        "ddd, d MMM yy H:mm:ss zzz",
        "d MMM yy H:mm:ss zzz",
    };

    public static DateTimeOffset? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Kommentare wie "(CET)" oder "(UTC)" entfernen
        var s = CommentRegex().Replace(text, " ");
        s = MultiSpaceRegex().Replace(s, " ").Trim();

        // Zeitzonen-Namen in Offsets übersetzen
        s = Regex.Replace(s, @"\b(GMT|UTC|UT|Z)$", "+00:00", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bEST$", "-05:00"); s = Regex.Replace(s, @"\bEDT$", "-04:00");
        s = Regex.Replace(s, @"\bCST$", "-06:00"); s = Regex.Replace(s, @"\bCDT$", "-05:00");
        s = Regex.Replace(s, @"\bMST$", "-07:00"); s = Regex.Replace(s, @"\bMDT$", "-06:00");
        s = Regex.Replace(s, @"\bPST$", "-08:00"); s = Regex.Replace(s, @"\bPDT$", "-07:00");

        // +0100 -> +01:00
        s = Regex.Replace(s, @"([+-])(\d{2})(\d{2})$", "$1$2:$3");

        if (DateTimeOffset.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var result))
            return result;

        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out result)
            ? result
            : null;
    }

    // ---------------------------------------------------------------- Authentifizierung

    private static void ParseAuthentication(HeaderAnalysis analysis)
    {
        foreach (var field in analysis.Fields)
        {
            if (field.Name.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase) ||
                field.Name.Equals("ARC-Authentication-Results", StringComparison.OrdinalIgnoreCase))
            {
                // Ergebnisse sind durch ';' getrennt. Das erste Element ist normalerweise die
                // prüfende Instanz (authserv-id) – Microsoft 365 lässt diese aber weg.
                var parts = field.Value.Split(';');
                var hasAuthServId = !AuthMethodRegex().IsMatch(parts[0]);
                var source = hasAuthServId && parts[0].Trim().Length > 0
                    ? $"{field.Name} ({parts[0].Trim()})"
                    : field.Name;
                foreach (var part in hasAuthServId ? parts.Skip(1) : parts)
                {
                    var m = AuthMethodRegex().Match(part);
                    if (!m.Success) continue;
                    analysis.AuthResults.Add(new AuthResult
                    {
                        Method = m.Groups["method"].Value.ToUpperInvariant(),
                        Result = m.Groups["result"].Value.ToLowerInvariant(),
                        Details = MultiSpaceRegex().Replace(part[(m.Index + m.Length)..], " ").Trim(),
                        Source = source
                    });
                }
            }
            else if (field.Name.Equals("Received-SPF", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(field.Value, @"^\s*(?<result>\w+)\s*(?<rest>.*)$", RegexOptions.Singleline);
                if (m.Success)
                {
                    analysis.AuthResults.Add(new AuthResult
                    {
                        Method = "SPF",
                        Result = m.Groups["result"].Value.ToLowerInvariant(),
                        Details = m.Groups["rest"].Value.Trim(),
                        Source = field.Name
                    });
                }
            }
        }
    }

    // ---------------------------------------------------------------- Regexe

    [GeneratedRegex(@"^(?<name>[!-9;-~]+)\s*:\s?(?<value>.*)$")]
    private static partial Regex FieldLineRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"=\?(?<charset>[^?]+)\?(?<enc>[BbQq])\?(?<text>[^?]*)\?=")]
    private static partial Regex EncodedWordRegex();

    [GeneratedRegex(@"(?<=\?=)\s+(?==\?)")]
    private static partial Regex BetweenEncodedWordsRegex();

    [GeneratedRegex(@"\([^()]*\)")]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"^\s*(?<method>spf|dkim|dmarc|arc|compauth|bimi|smtp\.auth|auth|iprev|dkim-atps)\s*=\s*(?<result>[\w-]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex AuthMethodRegex();
}
