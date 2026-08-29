using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Etwas, das der Server an eine Stelle zuruecksetzen kann (Rundenstart).
    /// </summary>
    public interface IRespawnable
    {
        void ServerTeleport(Vector3 position, Quaternion rotation);
    }
}
