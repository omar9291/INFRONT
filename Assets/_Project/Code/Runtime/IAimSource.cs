using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Liefert Ursprung und Richtung fuer einen Schuss. Erfuellen sowohl der
    /// Spieler (Maus-Zielen) als auch der Bot (rechnet auf sein Ziel).
    /// Dadurch benutzt die Waffe fuer beide denselben Code.
    /// </summary>
    public interface IAimSource
    {
        Vector3 AimOrigin { get; }
        Vector3 AimDirection { get; }
    }
}
