using System;
using System.IO;
using UnityEngine;

namespace Infront
{
    /// <summary>Die gesammelten Zahlen. Eine flache Klasse, damit JsonUtility sie kann.</summary>
    [Serializable]
    public sealed class StatistikDaten
    {
        public int Version = 1;

        public int Spiele;
        public int Siege;
        public int Runden;

        public int Schuesse;
        public int Treffer;
        public int Kopftreffer;

        public int Abschuesse;
        public int Tode;

        public int SekundenGespielt;
        public int Starts;              // wie oft das Spiel geoeffnet wurde
    }

    /// <summary>
    /// Die eigenen Zahlen - und zwar wirklich die eigenen.
    ///
    /// "Analytics" heisst in den meisten Spielen: Daten wandern zu einem
    /// fremden Rechner, und der Spieler sieht nie etwas davon. Hier ist es
    /// umgekehrt. Alles bleibt in einer Datei neben dem Profil, NICHTS wird
    /// verschickt, und die Zahlen sind im Menue zu sehen. Das ist der einzige
    /// Zweck, den sie in einem Spiel fuer einen Menschen haben.
    ///
    /// Praktischer Nebeneffekt: ohne Server, ohne Vertrag, ohne
    /// Datenschutzerklaerung fuer fremde Daten. Wer Zahlen ueber andere
    /// Menschen sammelt, muss dafuer geradestehen - das hier sammelt keine.
    ///
    /// Bewusst NICHT gespeichert: Zeitpunkte einzelner Spiele, Gegnernamen,
    /// irgendetwas, das eine Person beschreibt. Nur Summen.
    /// </summary>
    public static class Spielstatistik
    {
        const string FileName = "statistik.json";

        static StatistikDaten _daten;
        static bool _geladen;
        static float _sitzungBegonnen = -1f;

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        public static StatistikDaten Daten
        {
            get { if (!_geladen) Laden(); return _daten; }
        }

        /// <summary>Trefferquote 0..1. Ohne Schuesse: 0.</summary>
        public static float Trefferquote =>
            Daten.Schuesse > 0 ? Mathf.Clamp01((float)Daten.Treffer / Daten.Schuesse) : 0f;

        /// <summary>Anteil Kopftreffer an allen Treffern, 0..1.</summary>
        public static float Kopfquote =>
            Daten.Treffer > 0 ? Mathf.Clamp01((float)Daten.Kopftreffer / Daten.Treffer) : 0f;

        /// <summary>Abschuesse je Tod. Ohne Tode: die Abschuesse selbst.</summary>
        public static float Verhaeltnis =>
            Daten.Tode > 0 ? (float)Daten.Abschuesse / Daten.Tode : Daten.Abschuesse;

        // ------------------------------------------------------------------

        public static void Laden()
        {
            _geladen = true;
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _daten = JsonUtility.FromJson<StatistikDaten>(json) ?? new StatistikDaten();
                    return;
                }
            }
            catch (Exception e)
            {
                // Kaputte Datei darf das Spiel nicht aufhalten. Die alte bleibt
                // liegen, damit man noch hineinsehen kann.
                Debug.LogWarning("[Infront] Statistik nicht lesbar, fange neu an: " + e.Message);
            }
            _daten = new StatistikDaten();
        }

        public static void Speichern()
        {
            if (!_geladen) Laden();
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_daten, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Infront] Statistik nicht schreibbar: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        //  Zaehlen
        // ------------------------------------------------------------------

        /// <summary>Beim Programmstart einmal rufen.</summary>
        public static void StartGezaehlt()
        {
            Daten.Starts++;
            _sitzungBegonnen = Time.realtimeSinceStartup;
            Speichern();
        }

        /// <summary>Spielzeit dieser Sitzung dazuzaehlen und sichern.</summary>
        public static void SitzungSichern()
        {
            if (_sitzungBegonnen < 0f) return;
            int s = Mathf.RoundToInt(Time.realtimeSinceStartup - _sitzungBegonnen);
            if (s > 0) Daten.SekundenGespielt += s;
            _sitzungBegonnen = Time.realtimeSinceStartup;
            Speichern();
        }

        public static void Schuss()
        {
            Daten.Schuesse++;
            // Nicht bei jedem Schuss auf die Platte schreiben - das waere bei
            // Dauerfeuer zehnmal die Sekunde. Gesichert wird am Rundenende.
        }

        public static void Treffer(bool kopf)
        {
            Daten.Treffer++;
            if (kopf) Daten.Kopftreffer++;
        }

        public static void Abschuss() => Daten.Abschuesse++;

        public static void Tod() => Daten.Tode++;

        public static void RundeVorbei()
        {
            Daten.Runden++;
            Speichern();
        }

        public static void SpielVorbei(bool gewonnen)
        {
            Daten.Spiele++;
            if (gewonnen) Daten.Siege++;
            SitzungSichern();   // sichert mit
        }

        // ------------------------------------------------------------------

        /// <summary>Alles loeschen. Gehoert zum Recht, seine Daten loszuwerden.</summary>
        public static void AllesLoeschen()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning("[Infront] Statistik nicht loeschbar: " + e.Message); }
            _daten = new StatistikDaten();
            _geladen = true;
            _sitzungBegonnen = Time.realtimeSinceStartup;
        }

        /// <summary>Nur fuer Tests: gemerkten Zustand vergessen.</summary>
        public static void ForgetForTests() { _geladen = false; _daten = null; _sitzungBegonnen = -1f; }
    }
}
