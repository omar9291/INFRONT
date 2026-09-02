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

        /// <summary>Haelt der Spieler die Ziel-Taste (rechte Maustaste)? Kimme/Korn
        /// bzw. Zielfernrohr - langsamer, genauer, engeres Blickfeld.</summary>
        bool AimHeld { get; }

        /// <summary>Haelt der Spieler die Duck-Taste (Strg)? Kleiner, langsamer,
        /// leiser, ruhigeres Zielen.</summary>
        bool CrouchHeld { get; }

        /// <summary>Haelt der Spieler die Schleich-Taste (Alt)? Sehr langsam,
        /// dafuer fuer Gegner-Bots nicht hoerbar.</summary>
        bool WalkHeld { get; }

        /// <summary>Wurde in diesem Frame die Sprungtaste gedrueckt?</summary>
        bool JumpPressed { get; }

        /// <summary>Haelt der Spieler die Feuertaste? (Sturmgewehr = Dauerfeuer)</summary>
        bool FireHeld { get; }

        /// <summary>Wurde in diesem Frame die Nachladetaste gedrueckt?</summary>
        bool ReloadPressed { get; }

        /// <summary>Haelt der Spieler die Benutzen-Taste (E)? Bombe legen / entschaerfen.</summary>
        bool UseHeld { get; }

        /// <summary>Gewuenschter Waffenplatz in diesem Frame: -1 = keiner, 0 = Primaer, 1 = Pistole.</summary>
        int SwitchToSlot { get; }

        /// <summary>In diesem Frame gedrueckte Faehigkeiten-Taste:
        /// -1 = keine, 0 = Q, 1 = F, 2 = G.</summary>
        int UseAbilitySlot { get; }
    }
}
