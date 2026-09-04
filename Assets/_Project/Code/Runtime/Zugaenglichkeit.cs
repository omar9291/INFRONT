using UnityEngine;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Traegt die Zugaenglichkeits-Einstellungen dorthin, wo sie wirken muessen.
    ///
    /// Manches liest sich direkt aus <see cref="GameSettings"/> - Farben etwa
    /// holt <see cref="UiTheme"/> selbst. Die Groesse der Oberflaeche dagegen
    /// haengt am PanelSettings-Asset, und das ist eine einzige gemeinsame Datei
    /// fuer Menue und Anzeige im Spiel. Genau dafuer gibt es diese Stelle.
    ///
    /// Wichtig: das Asset wird zur Laufzeit veraendert. Im Editor bleibt der
    /// Wert danach stehen, deshalb wird beim Beenden auf 1 zurueckgesetzt.
    /// </summary>
    public static class Zugaenglichkeit
    {
        static PanelSettings _panel;
        static float _zuletzt = -1f;

        /// <summary>Das gemeinsame Panel holen (und merken).</summary>
        static PanelSettings Panel()
        {
            if (_panel == null) _panel = Resources.Load<PanelSettings>("InfrontPanel");
            return _panel;
        }

        /// <summary>
        /// Die eingestellte Groesse auf die Oberflaeche legen. Darf jeden Frame
        /// gerufen werden - es passiert nur etwas, wenn sich der Wert geaendert hat.
        /// </summary>
        public static void UiGroesseAnwenden()
        {
            float s = Mathf.Clamp(GameSettings.UiScale, 0.8f, 1.6f);
            if (Mathf.Approximately(s, _zuletzt)) return;

            var p = Panel();
            if (p == null) return;

            p.scale = s;
            _zuletzt = s;
        }

        /// <summary>Beim Beenden zuruecksetzen, damit im Editor nichts haengenbleibt.</summary>
        public static void Zuruecksetzen()
        {
            var p = Panel();
            if (p != null) p.scale = 1f;
            _zuletzt = -1f;
        }

        /// <summary>Nur fuer Tests: gemerkten Zustand vergessen.</summary>
        public static void ForgetForTests() { _panel = null; _zuletzt = -1f; }

        /// <summary>Nur fuer Tests: die aktuell gesetzte Groesse ablesen.</summary>
        public static float AktuelleGroesseForTests
        {
            get { var p = Panel(); return p != null ? p.scale : 1f; }
        }
    }

    /// <summary>
    /// Sorgt dafuer, dass <see cref="Zugaenglichkeit"/> auch wirklich laeuft.
    /// Haengt am GameFlow-Objekt und ueberlebt damit jeden Szenenwechsel.
    /// </summary>
    public sealed class ZugaenglichkeitAnwender : MonoBehaviour
    {
        void OnEnable() => Zugaenglichkeit.UiGroesseAnwenden();

        void Update() => Zugaenglichkeit.UiGroesseAnwenden();

        void OnApplicationQuit() => Zugaenglichkeit.Zuruecksetzen();
    }
}
