using System;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Das oertliche Spielerprofil - die Antwort auf "Konto anlegen", ohne
    /// Server, ohne Rechnung und ohne rechtlichen Verantwortlichen.
    ///
    /// Gespeichert wird in einer JSON-Datei neben den Spielstaenden, nicht in
    /// PlayerPrefs. Grund: eine Datei kann der Spieler sehen, sichern, kopieren
    /// und loeschen - genau das, was ein Konto koennen muesste (Datenexport und
    /// Kontoloeschung), nur ohne dass jemand seine Daten herausgeben muss.
    ///
    /// Bewusst NICHT enthalten: E-Mail, Passwort, irgendeine Kennung, die eine
    /// Person identifiziert. Damit faellt das Ganze nicht unter die DSGVO -
    /// es verlaesst dieses Geraet nie.
    ///
    /// NICHT pruefbar: ob es sich gut anfuehlt. Pruefbar: speichern, laden,
    /// Erstlauf-Erkennung, Loeschen, und dass beschaedigte Dateien nicht das
    /// Spiel mitreissen.
    /// </summary>
    [Serializable]
    public sealed class ProfileData
    {
        public string Name = "";
        public int Version = 1;
        public bool OnboardingDone;
        public bool ControlsSeen;
        public long CreatedUnix;
        public long LastPlayedUnix;
        public int MatchesPlayed;
        public int SecondsPlayed;
    }

    public static class PlayerProfile
    {
        const string FileName = "profil.json";

        static ProfileData _data;
        static bool _loaded;

        /// <summary>Wo die Datei liegt. Der Spieler darf das wissen.</summary>
        public static string FilePath =>
            System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static ProfileData Data
        {
            get { if (!_loaded) Load(); return _data; }
        }

        /// <summary>Erster Start ueberhaupt? Dann laeuft der Erstlauf-Ablauf.</summary>
        public static bool IsFirstRun => !Data.OnboardingDone;

        /// <summary>Angezeigter Name. Leer heisst: noch nicht gesetzt.</summary>
        public static string DisplayName
        {
            get => string.IsNullOrWhiteSpace(Data.Name) ? "PLAYER" : Data.Name.Trim();
            set { Data.Name = value ?? ""; Save(); }
        }

        public static void Load()
        {
            _loaded = true;
            _data = new ProfileData();

            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    string json = System.IO.File.ReadAllText(FilePath);
                    var geladen = JsonUtility.FromJson<ProfileData>(json);
                    // Eine beschaedigte oder leere Datei darf das Spiel nicht
                    // mitreissen - dann faengt das Profil eben neu an.
                    if (geladen != null) _data = geladen;
                }
                else
                {
                    _data.CreatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profil] Konnte {FilePath} nicht lesen ({e.Message}) - " +
                                 "es wird ein frisches Profil benutzt. Nichts geht verloren, " +
                                 "die alte Datei bleibt liegen.");
                _data = new ProfileData { CreatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            }
        }

        public static void Save()
        {
            if (!_loaded) Load();
            _data.LastPlayedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try
            {
                System.IO.File.WriteAllText(FilePath, JsonUtility.ToJson(_data, true));
            }
            catch (Exception e)
            {
                // Schreiben kann fehlschlagen (volle Platte, Rechte). Das Spiel
                // laeuft weiter, der Fortschritt dieser Sitzung ist dann weg.
                Debug.LogWarning($"[Profil] Konnte nicht speichern: {e.Message}");
            }
        }

        /// <summary>Der Erstlauf ist durch. Wird nicht noch einmal gezeigt.</summary>
        public static void MarkOnboardingDone()
        {
            Data.OnboardingDone = true;
            Save();
        }

        public static void MarkControlsSeen()
        {
            Data.ControlsSeen = true;
            Save();
        }

        public static void RecordMatch(int sekunden)
        {
            Data.MatchesPlayed += 1;
            Data.SecondsPlayed += Mathf.Max(0, sekunden);
            Save();
        }

        /// <summary>
        /// Alles loeschen - das oertliche Gegenstueck zur Kontoloeschung.
        /// Entfernt die Profildatei UND die Laufbahn-Werte. Danach ist das
        /// Spiel wie frisch installiert.
        /// </summary>
        public static void DeleteEverything()
        {
            // Wer "alles loeschen" sagt, meint alles - nicht nur das Profil.
            Spielstatistik.AllesLoeschen();
            Absturzbericht.AllesLoeschen();

            try
            {
                if (System.IO.File.Exists(FilePath)) System.IO.File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profil] Loeschen fehlgeschlagen: {e.Message}");
            }

            CareerStats.ResetForTests();
            _data = new ProfileData { CreatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            _loaded = true;
            Save();
        }

        /// <summary>Nur fuer Tests: Zustand im Speicher wegwerfen.</summary>
        public static void ForgetForTests() { _loaded = false; _data = null; }
    }
}
