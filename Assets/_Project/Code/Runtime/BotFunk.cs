using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Was ein Bot ruft, hoeren seine Leute auch.
    ///
    /// Bisher waren die Ansagen ("Feind gesichtet!") reine Anzeige - Text im
    /// Meldungsfenster, sonst nichts. Fuenf Bots liefen deshalb als fuenf
    /// Einzelgaenger durch die Halle und kamen einer nach dem anderen an.
    ///
    /// Damit daraus keine Gedankenuebertragung wird, gelten drei Grenzen:
    ///
    ///  - REICHWEITE: nur wer nah genug ist, hoert es. Ein Funkspruch aus dem
    ///    anderen Ende der Halle kommt nicht an.
    ///  - ALTER: nach ein paar Sekunden ist die Meldung wertlos. Der Gegner
    ///    steht dann nicht mehr da.
    ///  - VERZOEGERUNG: ganz kurz passiert nichts. Jemand muss den Satz erst
    ///    sagen, und der andere muss ihn hoeren.
    ///
    /// Nur Server. Bots laufen ausschliesslich dort.
    /// </summary>
    public static class BotFunk
    {
        struct Meldung
        {
            public int Team;
            public Vector3 Ort;
            public Vector3 Sprecher;
            public float Zeit;
        }

        /// <summary>Wie weit ein Funkspruch getragen wird.</summary>
        public const float Reichweite = 45f;

        /// <summary>Nach so vielen Sekunden ist eine Meldung wertlos.</summary>
        public const float Haltbarkeit = 6f;

        /// <summary>So lange braucht es, bis eine Meldung angekommen ist.</summary>
        public const float Verzoegerung = 0.6f;

        static readonly List<Meldung> _meldungen = new List<Meldung>();

        /// <summary>Ein Bot meldet einen Feind. <paramref name="sprecher"/> ist,
        /// wo der Melder steht - danach richtet sich die Reichweite.</summary>
        public static void ServerFeindGesichtet(int team, Vector3 sprecher, Vector3 ort)
        {
            Aufraeumen();
            _meldungen.Add(new Meldung
            {
                Team = team,
                Ort = ort,
                Sprecher = sprecher,
                Zeit = Time.time,
            });
        }

        /// <summary>
        /// Die naechstgelegene brauchbare Meldung fuer dieses Team holen.
        /// Liefert false, wenn nichts angekommen ist.
        /// </summary>
        public static bool TryEmpfangen(int team, Vector3 hoerer, out Vector3 ort)
        {
            Aufraeumen();

            bool gefunden = false;
            float beste = float.MaxValue;
            ort = Vector3.zero;

            for (int i = 0; i < _meldungen.Count; i++)
            {
                var m = _meldungen[i];
                if (m.Team != team) continue;

                float alter = Time.time - m.Zeit;
                if (alter < Verzoegerung) continue;          // noch nicht angekommen
                if (alter > Haltbarkeit) continue;           // schon veraltet

                float d = Vector3.Distance(hoerer, m.Sprecher);
                if (d > Reichweite) continue;                // zu weit weg
                if (d < 0.5f) continue;                      // das ist der Melder selbst

                if (d < beste) { beste = d; ort = m.Ort; gefunden = true; }
            }

            return gefunden;
        }

        // --- Wiederholsperre fuer Ansagen ---------------------------------
        //
        // Jeder Bot hat seine eigene Sprechpause, aber niemand hat auf die
        // anderen geachtet. Sehen fuenf Bots gleichzeitig denselben Gegner,
        // rufen alle fuenf im selben Moment "Enemy spotted!", und die
        // Abschussliste besteht aus fuenf gleichen Zeilen. Das ist kein
        // Funkverkehr, das ist Rauschen - und es verdeckt die Zeilen, auf die
        // es ankommt (wer wen ausgeschaltet hat).
        //
        // Deshalb: derselbe Satz geht je Team nur einmal in diesem Fenster
        // hinaus. Wer zu spaet kommt, schweigt. Das taktische Weiterreichen
        // der Feindposition (ServerFeindGesichtet) bleibt davon unberuehrt -
        // gedrosselt wird nur, was auf dem Bildschirm steht.

        /// <summary>So lange wird derselbe Satz im selben Team nicht wiederholt.</summary>
        public const float Wiederholsperre = 6f;

        static readonly Dictionary<string, float> _zuletztGerufen = new Dictionary<string, float>();

        /// <summary>Darf dieser Satz fuer dieses Team gerade heraus? Sagt beim
        /// ersten Mal ja und merkt sich den Zeitpunkt.</summary>
        public static bool DarfRufen(int team, string satz)
        {
            if (string.IsNullOrEmpty(satz)) return false;

            string schluessel = team + "|" + satz;
            if (_zuletztGerufen.TryGetValue(schluessel, out float wann)
                && Time.time - wann < Wiederholsperre)
                return false;

            _zuletztGerufen[schluessel] = Time.time;
            return true;
        }

        static void Aufraeumen()
        {
            for (int i = _meldungen.Count - 1; i >= 0; i--)
                if (Time.time - _meldungen[i].Zeit > Haltbarkeit)
                    _meldungen.RemoveAt(i);
        }

        /// <summary>Beim Rundenwechsel: alles vergessen.</summary>
        public static void Reset()
        {
            _meldungen.Clear();
            _zuletztGerufen.Clear();
        }

        /// <summary>Nur fuer Tests.</summary>
        public static int AnzahlForTests => _meldungen.Count;
    }
}
