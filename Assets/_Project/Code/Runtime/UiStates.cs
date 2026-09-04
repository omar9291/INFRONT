using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Die vier Zustaende, die jede Oberflaeche koennen muss, an einer Stelle:
    ///
    ///  - LEER    : es gibt noch nichts zu zeigen (erste Runde nicht gespielt)
    ///  - LAEDT   : es dauert gerade
    ///  - FEHLER  : etwas ist schiefgegangen, mit Grund und Ausweg
    ///  - NETZ    : die Verbindung fehlt oder ist abgerissen
    ///
    /// Warum an einer Stelle: sonst erfindet jede Oberflaeche ihre eigene
    /// Version, und drei von vier vergessen den Ausweg-Knopf. Ein leerer
    /// Bildschirm ohne Erklaerung ist der haeufigste Anfaengerfehler in
    /// Oberflaechen - er sieht aus wie ein Absturz.
    ///
    /// Regeln, an die sich jeder Zustand hier haelt:
    ///  1. Immer sagen, WAS los ist - nie nur ein Symbol.
    ///  2. Immer sagen, was der Spieler TUN kann.
    ///  3. Nie dem Spieler die Schuld geben.
    ///
    /// NICHT pruefbar: ob es schoen aussieht. Pruefbar: dass jeder Zustand
    /// einen Titel, einen Text und (ausser beim Laden) einen Ausweg hat.
    /// </summary>
    public static class UiStates
    {
        public enum Kind { Leer, Laedt, Fehler, Netz }

        /// <summary>
        /// Baut einen Zustands-Block. <paramref name="aktion"/> und
        /// <paramref name="aktionText"/> sind der Ausweg - beim Laden darf er
        /// fehlen, sonst nicht.
        /// </summary>
        public static VisualElement Panel(Kind kind, string titel, string text,
                                          string aktionText = null, Action aktion = null)
        {
            var box = new VisualElement();
            box.name = "state-" + kind.ToString().ToLowerInvariant();
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            box.style.paddingTop = 28f; box.style.paddingBottom = 28f;
            box.style.paddingLeft = 24f; box.style.paddingRight = 24f;

            var marke = new VisualElement();
            marke.style.width = 34f;
            marke.style.height = 3f;
            marke.style.marginBottom = 14f;
            marke.style.backgroundColor = FarbeFuer(kind);
            box.Add(marke);

            var t = new Label(titel);
            t.style.color = UiTheme.Text;
            t.style.fontSize = 15f;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.letterSpacing = 2f;
            t.style.marginBottom = 8f;
            t.style.whiteSpace = WhiteSpace.Normal;
            t.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(t);

            var b = new Label(text);
            b.style.color = UiTheme.TextDim;
            b.style.fontSize = 12f;
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.maxWidth = 420f;
            box.Add(b);

            if (!string.IsNullOrEmpty(aktionText) && aktion != null)
            {
                var knopf = new Button(aktion) { text = aktionText };
                knopf.name = "state-action";
                knopf.style.marginTop = 16f;
                knopf.style.height = 34f;
                knopf.style.paddingLeft = 18f; knopf.style.paddingRight = 18f;
                knopf.style.fontSize = 12f;
                knopf.style.letterSpacing = 2f;
                knopf.style.color = UiTheme.Text;
                knopf.style.backgroundColor = UiTheme.Glass;
                UiTheme.Square(knopf);
                UiTheme.Border(knopf, 1f, UiTheme.Edge);
                box.Add(knopf);
            }

            return box;
        }

        static Color FarbeFuer(Kind kind) => kind switch
        {
            Kind.Fehler => new Color(0.78f, 0.32f, 0.30f),
            Kind.Netz => new Color(0.85f, 0.62f, 0.25f),
            Kind.Laedt => UiTheme.Ice,
            _ => UiTheme.TextDim,
        };

        // --- Fertige Faelle, damit sie ueberall gleich klingen ---------------

        /// <summary>Noch keine Runde gespielt.</summary>
        public static VisualElement KeineLaufbahn(Action spielen) => Panel(
            Kind.Leer,
            "NO ROUNDS YET",
            "Your wins, streaks and aces show up here once you have played.",
            "START YOUR FIRST ROUND", spielen);

        /// <summary>Es dauert gerade. Kein Ausweg-Knopf - warten ist die Aktion.</summary>
        public static VisualElement Laedt(string was) => Panel(
            Kind.Laedt, "LOADING", was);

        /// <summary>Etwas ist schiefgegangen.</summary>
        public static VisualElement Fehler(string grund, Action nochmal) => Panel(
            Kind.Fehler,
            "THAT DID NOT WORK",
            grund + "\n\nThis is not on you. Give it another try - " +
            "if it happens again, the reason is in the crash report.",
            "TRY AGAIN", nochmal);

        /// <summary>Verbindung weg. Im Host-Modus heisst das: die Runde ist vorbei.</summary>
        public static VisualElement Netz(Action zurueck) => Panel(
            Kind.Netz,
            "CONNECTION LOST",
            "The game runs as its own host on this computer. If the " +
            "connection drops, the running round cannot be saved - " +
            "but your career progress stays saved.",
            "BACK TO MENU", zurueck);
    }
}
