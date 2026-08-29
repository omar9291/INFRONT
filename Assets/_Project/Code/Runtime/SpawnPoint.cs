using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Markiert eine Stelle, an der Figuren erscheinen koennen. Optional einem
    /// Team zugeordnet (Team.None = fuer alle).
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] int _teamId = Team.None;
        public int TeamId => _teamId;

        void OnEnable() => SpawnService.Register(this);
        void OnDisable() => SpawnService.Unregister(this);
    }
}
