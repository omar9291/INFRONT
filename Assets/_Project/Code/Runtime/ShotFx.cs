using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Alles, was die Optik für einen abgegebenen Schuss braucht. Der Server
    /// schickt es per RPC an alle Clients (siehe <see cref="NetworkWeapon"/>);
    /// hier wird nur gezeigt, nichts gerechnet.
    /// </summary>
    public readonly struct ShotFx
    {
        /// <summary>Mündungspunkt - Start der Schussspur, Ort des Mündungsfeuers.</summary>
        public readonly Vector3 Origin;
        /// <summary>Wo die Kugel auftrifft (oder das Ende der Reichweite).</summary>
        public readonly Vector3 End;
        /// <summary>Flächennormale am Auftreffpunkt (für das Ausrichten des Einschlags).</summary>
        public readonly Vector3 Normal;
        /// <summary>0 = nichts getroffen, 1 = Wand/Umgebung, 2 = Körper.</summary>
        public readonly byte Impact;

        public ShotFx(Vector3 origin, Vector3 end, Vector3 normal, byte impact)
        {
            Origin = origin;
            End = end;
            Normal = normal;
            Impact = impact;
        }

        public bool HitWall => Impact == 1;
        public bool HitBody => Impact == 2;
    }
}
