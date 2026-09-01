using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Ein Bombenplatz (A oder B). Kein Netzwerk-Objekt - nur eine Zone in der
    /// Szene. Der Server fragt ueber <see cref="SiteAt"/> ab, ob ein Kaempfer
    /// gerade auf einem Platz steht.
    ///
    /// Die Zone ist ein einfacher Quader um den Ursprung des Objekts
    /// (halbe Kantenlaengen in <see cref="_halfExtents"/>).
    /// </summary>
    public sealed class BombSite : MonoBehaviour
    {
        public static readonly List<BombSite> All = new();

        [SerializeField] int _siteId;
        [SerializeField] Vector3 _halfExtents = new(6f, 2.5f, 7f);

        public int SiteId => _siteId;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 d = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(d.x) <= _halfExtents.x
                && Mathf.Abs(d.y) <= _halfExtents.y
                && Mathf.Abs(d.z) <= _halfExtents.z;
        }

        /// <summary>SiteId des Platzes, auf dem der Punkt liegt - sonst -1.</summary>
        public static int SiteAt(Vector3 worldPoint)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].Contains(worldPoint))
                    return All[i].SiteId;
            return -1;
        }

        /// <summary>Mittelpunkt eines Platzes (fuer Tests / KI). Fallback: Vector3.zero.</summary>
        public static Vector3 CenterOf(int siteId)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].SiteId == siteId)
                    return All[i].transform.position;
            return Vector3.zero;
        }
    }
}
