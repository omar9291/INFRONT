using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Markiert ein Bauteil einer Figur als Teamkennzeichen - Armbinde,
    /// Rueckenpanel. Nur diese Teile faerbt <see cref="TeamTint"/> ein.
    ///
    /// Vorgeschichte (2026-09-05): TeamTint hat die Grundfarbe JEDES Renderers
    /// der Figur ueberschrieben. Beim Gegner war das (1, 0.35, 0.30), also ein
    /// kraeftiges Lachsrot - flaechig ueber Gesicht, Haende und Kleidung. Auf
    /// den Rundgangsbildern sahen die Bots dadurch aus wie Plastikpuppen, und
    /// das war der groesste einzelne Grund, warum die Karte trotz echter
    /// Modelle und Texturen nach Spielzeug aussah.
    ///
    /// Die Mannschaft muss trotzdem auf einen Blick erkennbar bleiben - das
    /// ist Spielbarkeit, nicht Optik. Echte Einheiten loesen das mit farbigem
    /// Klebeband am Oberarm, und genau das macht diese Markierung.
    /// </summary>
    public sealed class TeamMarker : MonoBehaviour
    {
    }
}
