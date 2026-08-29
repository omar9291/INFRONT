using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kennt alle Spawn-Punkte der Szene und gibt einen zurueck.
    /// Phase 2: zufaellige Wahl. Spaeter: den, der am weitesten von Gegnern weg ist.
    /// </summary>
    public static class SpawnService
    {
        static readonly List<SpawnPoint> Points = new();

        public static void Register(SpawnPoint point)
        {
            if (!Points.Contains(point))
                Points.Add(point);
        }

        public static void Unregister(SpawnPoint point)
        {
            Points.Remove(point);
        }

        public static bool TryGetSpawn(out Vector3 position, out Quaternion rotation)
        {
            Points.RemoveAll(p => p == null);

            if (Points.Count == 0)
            {
                position = new Vector3(0f, 1f, 0f);
                rotation = Quaternion.identity;
                return false;
            }

            var chosen = Points[Random.Range(0, Points.Count)];
            position = chosen.transform.position;
            rotation = chosen.transform.rotation;
            return true;
        }
    }
}
