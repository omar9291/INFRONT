using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kennt alle Spawn-Punkte der Szene und gibt einen zurueck.
    /// Bevorzugt Punkte des eigenen Teams, faellt sonst auf beliebige zurueck.
    /// </summary>
    public static class SpawnService
    {
        static readonly List<SpawnPoint> Points = new();
        static readonly List<SpawnPoint> Candidates = new();

        public static void Register(SpawnPoint point)
        {
            if (!Points.Contains(point))
                Points.Add(point);
        }

        public static void Unregister(SpawnPoint point) => Points.Remove(point);

        public static bool TryGetSpawn(out Vector3 position, out Quaternion rotation)
            => TryGetSpawn(Team.None, out position, out rotation);

        public static bool TryGetSpawn(int teamId, out Vector3 position, out Quaternion rotation)
        {
            Points.RemoveAll(p => p == null);

            Candidates.Clear();
            if (teamId != Team.None)
                foreach (var p in Points)
                    if (p.TeamId == teamId)
                        Candidates.Add(p);

            if (Candidates.Count == 0)
                Candidates.AddRange(Points);

            if (Candidates.Count == 0)
            {
                position = new Vector3(0f, 1f, 0f);
                rotation = Quaternion.identity;
                return false;
            }

            var chosen = Candidates[Random.Range(0, Candidates.Count)];
            position = chosen.transform.position;
            rotation = chosen.transform.rotation;
            return true;
        }
    }
}
