using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Schreibt bei einem Absturz oder einem unbehandelten Fehler eine
    /// Textdatei - auf DIESEN Rechner, in einen Ordner, den man oeffnen kann.
    ///
    /// Warum nicht verschicken: dafuer braeuchte es einen Server, jemanden der
    /// dafuer geradesteht, und eine Einwilligung. Eine Datei, die der Spieler
    /// selbst schicken kann WENN er will, loest dasselbe Problem - jemand muss
    /// den Fehler nachvollziehen koennen - und niemand muss dafuer Daten
    /// abgeben, von denen er nichts weiss.
    ///
    /// In der Datei steht bewusst nichts ueber die Person: kein Name, kein
    /// Benutzerkonto, kein Pfad ins Benutzerverzeichnis. Nur was zum Suchen
    /// des Fehlers noetig ist.
    /// </summary>
    public static class Absturzbericht
    {
        const string OrdnerName = "abstuerze";
        const int MaxBerichte = 20;

        static bool _laeuft;
        static int _dieseSitzung;

        public static string Ordner => Path.Combine(Application.persistentDataPath, OrdnerName);

        /// <summary>Wie viele Berichte liegen da? Fuer die Anzeige im Menue.</summary>
        public static int Anzahl
        {
            get
            {
                try { return Directory.Exists(Ordner) ? Directory.GetFiles(Ordner, "*.txt").Length : 0; }
                catch (Exception) { return 0; }
            }
        }

        /// <summary>Mitschreiben einschalten. Mehrfacher Aufruf schadet nicht.</summary>
        public static void Starten()
        {
            if (_laeuft) return;
            _laeuft = true;
            Application.logMessageReceived += Aufgefangen;
        }

        public static void Beenden()
        {
            if (!_laeuft) return;
            _laeuft = false;
            Application.logMessageReceived -= Aufgefangen;
        }

        static void Aufgefangen(string nachricht, string stapel, LogType art)
        {
            if (art != LogType.Exception && art != LogType.Assert) return;

            // Ein kaputter Frame erzeugt sonst hunderte gleiche Dateien.
            if (_dieseSitzung >= 5) return;
            _dieseSitzung++;

            Schreiben(nachricht, stapel, art);
        }

        static void Schreiben(string nachricht, string stapel, LogType art)
        {
            try
            {
                Directory.CreateDirectory(Ordner);

                string stempel = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss",
                                                       CultureInfo.InvariantCulture);
                string datei = Path.Combine(Ordner, $"absturz-{stempel}-{_dieseSitzung}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("INFRONT - Absturzbericht");
                sb.AppendLine("Diese Datei liegt nur auf diesem Rechner. Es wird nichts verschickt.");
                sb.AppendLine("Wenn du willst, kannst du sie selbst weitergeben.");
                sb.AppendLine();
                sb.AppendLine("Zeitpunkt:   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",
                                                                     CultureInfo.InvariantCulture));
                sb.AppendLine("Art:         " + art);
                sb.AppendLine("Spielstand:  " + Application.version);
                sb.AppendLine("Unity:       " + Application.unityVersion);
                sb.AppendLine("System:      " + SystemInfo.operatingSystem);
                sb.AppendLine("Prozessor:   " + SystemInfo.processorType);
                sb.AppendLine("Grafik:      " + SystemInfo.graphicsDeviceName
                              + " (" + SystemInfo.graphicsDeviceType + ")");
                sb.AppendLine("Speicher:    " + SystemInfo.systemMemorySize + " MB");
                sb.AppendLine("Szene:       "
                              + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                sb.AppendLine();
                sb.AppendLine("Meldung:");
                sb.AppendLine(nachricht);
                sb.AppendLine();
                sb.AppendLine("Aufrufstapel:");
                sb.AppendLine(string.IsNullOrEmpty(stapel) ? "(keiner)" : stapel);

                File.WriteAllText(datei, sb.ToString());
                AufraeumenAltes();

                // Sagen, dass etwas geschrieben wurde. Sonst passiert das
                // lautlos, und niemand weiss, dass es die Datei gibt.
                Meldungen.Zeige("Fehlerbericht geschrieben - siehe DEINE DATEN",
                                Meldungen.Art.Fehler);
            }
            catch (Exception)
            {
                // Ein Fehler beim Schreiben des Fehlerberichts darf auf keinen
                // Fall wieder einen Fehler ausloesen - das gaebe eine Schleife.
            }
        }

        /// <summary>Nur die neuesten Berichte behalten.</summary>
        static void AufraeumenAltes()
        {
            try
            {
                var dateien = new DirectoryInfo(Ordner).GetFiles("*.txt")
                                                       .OrderByDescending(f => f.LastWriteTimeUtc)
                                                       .ToArray();
                for (int i = MaxBerichte; i < dateien.Length; i++) dateien[i].Delete();
            }
            catch (Exception) { /* egal */ }
        }

        /// <summary>Alle Berichte loeschen.</summary>
        public static void AllesLoeschen()
        {
            try { if (Directory.Exists(Ordner)) Directory.Delete(Ordner, true); }
            catch (Exception e) { Debug.LogWarning("[Infront] Berichte nicht loeschbar: " + e.Message); }
        }

        /// <summary>Nur fuer Tests: einen Bericht von Hand schreiben.</summary>
        public static void SchreibeTestbericht(string nachricht)
        {
            _dieseSitzung++;
            Schreiben(nachricht, "(Test)", LogType.Exception);
        }

        /// <summary>Nur fuer Tests: den Zaehler dieser Sitzung zuruecksetzen.</summary>
        public static void ForgetForTests() { _dieseSitzung = 0; }
    }
}
