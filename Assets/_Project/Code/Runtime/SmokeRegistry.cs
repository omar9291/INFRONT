using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Verzeichnis aller aktiven Rauchwolken. Die Bot-Sichtpruefung fragt hier:
    /// liegt eine Wolke zwischen Auge und Ziel? So blockiert Rauch die Sicht der
    /// Bots WIRKLICH - ohne Collider-Gebastel und ohne dass Kugeln am Rauch
    /// haengen bleiben.
    ///
    /// Rein server-relevante Logik; die Wolke selbst ist nur Optik.
    /// </summary>
    public static class SmokeRegistry
    {
        struct Cloud { public Transform T; public float Radius; }
        static readonly List<Cloud> _clouds = new();

        public static void Register(Transform t, float radius)
        {
            if (t != null) _clouds.Add(new Cloud { T = t, Radius = radius });
        }

        public static void Unregister(Transform t)
        {
            _clouds.RemoveAll(c => c.T == t);
        }

        public static void Reset() => _clouds.Clear();

        public static int ActiveCount => _clouds.Count;

        /// <summary>Schneidet die Strecke a..b eine aktive Rauchwolke?</summary>
        public static bool Blocks(Vector3 a, Vector3 b)
        {
            for (int i = _clouds.Count - 1; i >= 0; i--)
            {
                var c = _clouds[i];
                if (c.T == null) { _clouds.RemoveAt(i); continue; }
                if (SegmentHitsSphere(a, b, c.T.position, c.Radius))
                    return true;
            }
            return false;
        }

        static bool SegmentHitsSphere(Vector3 a, Vector3 b, Vector3 center, float radius)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.0001f) return (a - center).sqrMagnitude <= radius * radius;

            float t = Mathf.Clamp01(Vector3.Dot(center - a, ab) / len2);
            Vector3 closest = a + ab * t;
            return (closest - center).sqrMagnitude <= radius * radius;
        }
    }
}
