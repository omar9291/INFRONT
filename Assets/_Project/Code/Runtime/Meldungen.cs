using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Kurze Hinweise, die von unten links hereinschieben und wieder gehen.
    ///
    /// Wofuer das NICHT da ist: Abschuesse und Rundenmeldungen. Dafuer gibt es
    /// die Abschussliste und das grosse Band in der Mitte. Wer alles in
    /// dieselbe Ecke wirft, macht das Wichtige unsichtbar.
    ///
    /// Wofuer es da ist: alles, was nicht das Gefecht betrifft und trotzdem
    /// nicht verschwiegen werden darf - Einstellung uebernommen, Fehlerbericht
    /// geschrieben, persoenliche Bestleistung, Statistik gespeichert. Sonst
    /// passieren diese Dinge lautlos, und der Spieler weiss nie, ob etwas
    /// angekommen ist.
    ///
    /// Die Anzeige haengt am HUD. Gibt es gerade keins (Menue, Testlauf),
    /// werden Meldungen gesammelt und beim naechsten Aufbau gezeigt - eine
    /// Meldung darf nicht verlorengehen, nur weil sie zur falschen Zeit kam.
    /// </summary>
    public static class Meldungen
    {
        /// <summary>Wie eine Meldung gemeint ist. Bestimmt die Farbe.</summary>
        public enum Art
        {
            /// <summary>Etwas ist passiert. Neutral.</summary>
            Info = 0,
            /// <summary>Etwas hat geklappt.</summary>
            Gut = 1,
            /// <summary>Etwas ist schiefgegangen - ohne Schuldzuweisung.</summary>
            Fehler = 2,
        }

        struct Eintrag { public string Text; public Art Art; }

        const int MaxSichtbar = 4;
        const float Standzeit = 4.5f;

        static readonly List<Eintrag> _warteschlange = new List<Eintrag>();
        static VisualElement _spalte;

        /// <summary>Nur fuer Tests: was zuletzt gemeldet wurde.</summary>
        public static string LetzteForTests { get; private set; } = "";

        /// <summary>Nur fuer Tests: wie viele gerade sichtbar sind.</summary>
        public static int SichtbarForTests => _spalte != null ? _spalte.childCount : 0;

        /// <summary>Eine Meldung zeigen.</summary>
        public static void Zeige(string text, Art art = Art.Info)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            LetzteForTests = text;

            if (_spalte == null)
            {
                // Noch keine Anzeige da - aufheben, nicht wegwerfen.
                _warteschlange.Add(new Eintrag { Text = text, Art = art });
                if (_warteschlange.Count > 8) _warteschlange.RemoveAt(0);
                return;
            }

            Anlegen(text, art);
        }

        /// <summary>Das HUD meldet sich an. Wird beim Aufbau der Anzeige gerufen.</summary>
        public static void Anhaengen(VisualElement wurzel)
        {
            if (wurzel == null) return;

            _spalte = new VisualElement { name = "meldungen" };
            _spalte.style.position = Position.Absolute;
            _spalte.style.left = 24f;
            _spalte.style.bottom = 96f;      // ueber dem Lebens-Kasten
            _spalte.style.flexDirection = FlexDirection.ColumnReverse;
            _spalte.pickingMode = PickingMode.Ignore;
            wurzel.Add(_spalte);

            // Was waehrenddessen aufgelaufen ist, jetzt nachholen.
            foreach (var e in _warteschlange) Anlegen(e.Text, e.Art);
            _warteschlange.Clear();
        }

        /// <summary>Das HUD geht weg.</summary>
        public static void Abhaengen()
        {
            _spalte = null;
        }

        static void Anlegen(string text, Art art)
        {
            if (_spalte == null) return;

            // Zu viele auf einmal: die aelteste geht.
            while (_spalte.childCount >= MaxSichtbar)
                _spalte.RemoveAt(0);

            var kasten = new VisualElement();
            kasten.style.flexDirection = FlexDirection.Row;
            kasten.style.alignItems = Align.Center;
            kasten.style.paddingLeft = 10f;
            kasten.style.paddingRight = 14f;
            kasten.style.paddingTop = 7f;
            kasten.style.paddingBottom = 7f;
            kasten.style.marginTop = 5f;
            kasten.style.backgroundColor = UiTheme.HudPanelBg;
            UiTheme.Square(kasten);
            UiTheme.Border(kasten, 1f, UiTheme.HudLine);
            kasten.pickingMode = PickingMode.Ignore;

            // Farbstreifen links - traegt die Bedeutung auch ohne Farbe, weil
            // er einfach da ist oder nicht.
            var streifen = new VisualElement();
            streifen.style.width = 3f;
            streifen.style.height = 16f;
            streifen.style.marginRight = 10f;
            streifen.style.backgroundColor = art switch
            {
                Art.Gut => UiTheme.Gut,
                Art.Fehler => UiTheme.Schlecht,
                _ => UiTheme.Ice,
            };
            kasten.Add(streifen);

            var label = new Label(text);
            label.style.color = UiTheme.Text;
            label.style.fontSize = 12f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.maxWidth = 340f;
            kasten.Add(label);

            _spalte.Add(kasten);

            // Von selbst wieder gehen. Ohne Bewegung, wenn der Spieler
            // "weniger Bewegung" eingestellt hat.
            float dauer = Standzeit;
            kasten.schedule.Execute(() =>
            {
                if (kasten.parent == null) return;
                if (GameSettings.ReduceMotion)
                {
                    kasten.RemoveFromHierarchy();
                    return;
                }
                kasten.style.opacity = 0f;
                kasten.schedule.Execute(() => kasten.RemoveFromHierarchy()).ExecuteLater(400);
            }).ExecuteLater((long)(dauer * 1000f));
        }

        /// <summary>Nur fuer Tests: alles vergessen.</summary>
        public static void ForgetForTests()
        {
            _warteschlange.Clear();
            _spalte = null;
            LetzteForTests = "";
        }
    }
}
