using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Markiert eine Stelle, an der Figuren erscheinen koennen.
    /// Wird vom <see cref="SpawnService"/> eingesammelt.
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        void OnEnable() => SpawnService.Register(this);
        void OnDisable() => SpawnService.Unregister(this);
    }
}
