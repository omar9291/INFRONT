using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kurzes Gedaechtnis fuer Geraeusche in der Welt (Schuesse, Sprint-Schritte).
    /// Die Bots fragen hier: hoere ich gerade einen Gegner? So gewinnt man in
    /// CS Runden - weil man Schritte hoert.
    ///
    /// Rein server-relevant. Eintraege verfallen nach kurzer Zeit.
    /// </summary>
    public static class SoundEvents
    {
        struct Noise { public Vector3 Pos; public float Loud; public int Team; public float Until; }
        static readonly List<Noise> _noises = new();

        public const float ShotLoud = 42f;
        public const float SprintLoud = 15f;
        public const float WalkLoud = 6f;

        public static void ServerReport(Vector3 pos, float loudness, int sourceTeam)
        {
            _noises.Add(new Noise
            {
                Pos = pos,
                Loud = loudness,
                Team = sourceTeam,
                Until = Time.time + 2f,
            });
            if (_noises.Count > 64) _noises.RemoveAt(0);
        }

        public static void Reset() => _noises.Clear();

        /// <summary>Hoert der Zuhoerer gerade ein GEGNERISCHES Geraeusch? Liefert
        /// die Stelle des lautesten hoerbaren Geraeuschs.</summary>
        public static bool TryHear(Vector3 listener, int listenerTeam, float hearingScale, out Vector3 pos)
        {
            pos = default;
            float best = -1f;
            float now = Time.time;

            for (int i = _noises.Count - 1; i >= 0; i--)
            {
                var n = _noises[i];
                if (now > n.Until) { _noises.RemoveAt(i); continue; }
                if (n.Team == listenerTeam) continue;                 // eigenes Team ignorieren

                float d = Vector3.Distance(listener, n.Pos);
                float range = n.Loud * Mathf.Max(0.2f, hearingScale);
                if (d > range) continue;

                float score = n.Loud * (1f - d / range);              // nah + laut = wichtig
                if (score > best) { best = score; pos = n.Pos; }
            }
            return best >= 0f;
        }
    }
}
