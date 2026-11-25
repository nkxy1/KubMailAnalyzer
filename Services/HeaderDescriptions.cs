namespace KubMailAnalyzer.Services;

/// <summary>Deutsche Beschreibungen und Kategorien bekannter Header-Felder.</summary>
public static class HeaderDescriptions
{
    private static readonly Dictionary<string, (string Category, string Description)> Known =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["From"] = ("Absender/Empfänger", "Autor der Nachricht (angezeigter Absender)"),
            ["Sender"] = ("Absender/Empfänger", "Tatsächlicher Versender, falls abweichend von From"),
            ["Reply-To"] = ("Absender/Empfänger", "Adresse, an die Antworten gehen sollen"),
            ["To"] = ("Absender/Empfänger", "Primäre Empfänger"),
            ["Cc"] = ("Absender/Empfänger", "Empfänger in Kopie"),
            ["Bcc"] = ("Absender/Empfänger", "Empfänger in Blindkopie"),
            ["Delivered-To"] = ("Absender/Empfänger", "Postfach, an das zugestellt wurde"),
            ["Return-Path"] = ("Absender/Empfänger", "Envelope-Absender (MAIL FROM), Ziel für Bounces"),
            ["Envelope-To"] = ("Absender/Empfänger", "Envelope-Empfänger (RCPT TO)"),
            ["X-Original-To"] = ("Absender/Empfänger", "Ursprünglicher Envelope-Empfänger vor Weiterleitung"),

            ["Subject"] = ("Nachricht", "Betreff"),
            ["Date"] = ("Nachricht", "Erstellungszeitpunkt laut Absender-Client"),
            ["Message-ID"] = ("Nachricht", "Weltweit eindeutige Kennung der Nachricht"),
            ["In-Reply-To"] = ("Nachricht", "Message-ID der Nachricht, auf die geantwortet wird"),
            ["References"] = ("Nachricht", "Message-IDs des bisherigen Gesprächsverlaufs"),
            ["Thread-Topic"] = ("Nachricht", "Thema der Unterhaltung (Outlook/Exchange)"),
            ["Thread-Index"] = ("Nachricht", "Konversationsindex (Outlook/Exchange)"),
            ["Importance"] = ("Nachricht", "Wichtigkeit"),
            ["Priority"] = ("Nachricht", "Priorität"),
            ["X-Priority"] = ("Nachricht", "Priorität (1 = hoch, 3 = normal, 5 = niedrig)"),
            ["Content-Language"] = ("Nachricht", "Sprache des Inhalts"),
            ["Accept-Language"] = ("Nachricht", "Bevorzugte Sprachen des Absenders"),
            ["Disposition-Notification-To"] = ("Nachricht", "Anforderung einer Lesebestätigung"),
            ["Auto-Submitted"] = ("Nachricht", "Kennzeichnet automatisch erzeugte Nachrichten"),
            ["Precedence"] = ("Nachricht", "Art der Nachricht (bulk, list, junk)"),

            ["Received"] = ("Transport", "Station auf dem Zustellweg (neueste oben)"),
            ["X-Received"] = ("Transport", "Zusätzliche, interne Transportstation"),
            ["X-Originating-IP"] = ("Transport", "IP-Adresse des einliefernden Clients"),
            ["X-Mailer"] = ("Transport", "Verwendetes Mailprogramm"),
            ["User-Agent"] = ("Transport", "Verwendetes Mailprogramm"),
            ["X-MS-Exchange-Organization-AuthSource"] = ("Transport", "Exchange-Server, der die Nachricht authentifiziert hat"),
            ["X-MS-Exchange-Organization-Network-Message-Id"] = ("Transport", "Exchange-interne Nachrichten-ID"),

            ["Authentication-Results"] = ("Sicherheit", "Ergebnisse von SPF-, DKIM- und DMARC-Prüfungen"),
            ["ARC-Authentication-Results"] = ("Sicherheit", "Authentifizierungsergebnisse in der ARC-Kette"),
            ["ARC-Seal"] = ("Sicherheit", "ARC-Siegel (Authenticated Received Chain)"),
            ["ARC-Message-Signature"] = ("Sicherheit", "ARC-Signatur der Nachricht"),
            ["DKIM-Signature"] = ("Sicherheit", "DKIM-Signatur der absendenden Domain"),
            ["Received-SPF"] = ("Sicherheit", "Ergebnis der SPF-Prüfung"),
            ["X-MS-Exchange-Organization-SCL"] = ("Sicherheit", "Spam Confidence Level (-1 bis 9)"),
            ["X-Forefront-Antispam-Report"] = ("Sicherheit", "Antispam-Bericht von Microsoft 365"),
            ["X-Microsoft-Antispam"] = ("Sicherheit", "Antispam-Bewertung von Microsoft 365 (BCL)"),
            ["X-Spam-Status"] = ("Sicherheit", "Spam-Bewertung (SpamAssassin)"),
            ["X-Spam-Score"] = ("Sicherheit", "Spam-Punktzahl"),
            ["X-Spam-Flag"] = ("Sicherheit", "Als Spam markiert (YES/NO)"),
            ["X-Spam-Level"] = ("Sicherheit", "Spam-Level als Sternchen"),
            ["X-Virus-Scanned"] = ("Sicherheit", "Virenscanner, der die Nachricht geprüft hat"),

            ["MIME-Version"] = ("Inhalt", "MIME-Version"),
            ["Content-Type"] = ("Inhalt", "Medientyp und Zeichensatz des Inhalts"),
            ["Content-Transfer-Encoding"] = ("Inhalt", "Kodierung des Inhalts (z. B. base64, quoted-printable)"),
            ["Content-Disposition"] = ("Inhalt", "Darstellungsart (inline/attachment)"),

            ["List-Unsubscribe"] = ("Verteiler", "Link/Adresse zum Abmelden"),
            ["List-Unsubscribe-Post"] = ("Verteiler", "One-Click-Abmeldung (RFC 8058)"),
            ["List-Id"] = ("Verteiler", "Kennung der Mailingliste"),
            ["Feedback-ID"] = ("Verteiler", "Kennung für Feedback-Loops (z. B. Gmail)"),
        };

    public static (string Category, string Description) Describe(string name)
    {
        if (Known.TryGetValue(name, out var info))
            return info;

        if (name.StartsWith("X-MS-", StringComparison.OrdinalIgnoreCase))
            return ("Microsoft", "Microsoft Exchange / Microsoft 365 spezifisches Feld");
        if (name.StartsWith("X-Google-", StringComparison.OrdinalIgnoreCase) || name.StartsWith("X-Gm-", StringComparison.OrdinalIgnoreCase))
            return ("Google", "Google/Gmail spezifisches Feld");
        if (name.StartsWith("X-Spam", StringComparison.OrdinalIgnoreCase))
            return ("Sicherheit", "Spam-Filter-Information");
        if (name.StartsWith("List-", StringComparison.OrdinalIgnoreCase))
            return ("Verteiler", "Mailinglisten-Information (RFC 2369)");
        if (name.StartsWith("X-", StringComparison.OrdinalIgnoreCase))
            return ("Benutzerdefiniert", "Nicht standardisiertes Zusatzfeld");

        return ("Sonstige", "");
    }
}
