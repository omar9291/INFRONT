using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Liefert die aktuellen Eingaben eines Spielers. Der Charakter fragt hier
    /// jeden Frame den Zustand ab. Ueber diese Schnittstelle kann im Test eine
    /// gefaelschte Eingabequelle eingesetzt werden.
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>Bewegungsrichtung. x = seitlich, y = vorne/hinten. Bereich -1..1.</summary>
        Vector2 Move { get; }

        /// <summary>Blickrichtung um die Hochachse in Grad. Bestimmt, wohin "vorne" zeigt.</summary>
        float LookYaw { get; }

        /// <summary>Zielrichtung hoch/runter in Grad. Positiv = nach oben. Wird begrenzt.</summary>
        float LookPitch { get; }

        /// <summary>Haelt der Spieler die Sprint-Taste?</summary>
        bool Sprint { get; }

        /// <summary>Wurde in diesem Frame die Sprungtaste gedrueckt?</summary>
        bool JumpPressed { get; }

        /// <summary>Haelt der Spieler die Feuertaste? (Sturmgewehr = Dauerfeuer)</summary>
        bool FireHeld { get; }

        /// <summary>Wurde in diesem Frame die Nachladetaste gedrueckt?</summary>
        bool ReloadPressed { get; }

        /// <summary>Gewuenschter Waffenplatz in diesem Frame: -1 = keiner, 0 = Primaer, 1 = Pistole.</summary>
        int SwitchToSlot { get; }
    }
}
