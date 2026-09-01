using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Faerbt die Figur relativ zum lokalen Spieler: eigenes Team blau,
    /// Gegner rot. Laeuft auf jedem Client fuer dessen eigene Sicht.
    /// Faellt weg, sobald es echte Charaktermodelle gibt.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    public sealed class TeamTint : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color Friendly = new(0.30f, 0.50f, 1f);
        static readonly Color Enemy = new(1f, 0.35f, 0.30f);
        static readonly Color Neutral = new(0.7f, 0.7f, 0.7f);

        TeamMember _team;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        Color _applied = Color.clear;

        void Awake()
        {
            _team = GetComponent<TeamMember>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Nach dem nachtraeglichen Aufbau der Figur (<see cref="CharacterVisual"/>)
        /// aufrufen - sammelt die neuen Renderer ein und faerbt beim naechsten
        /// LateUpdate neu.</summary>
        public void RefreshRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _applied = Color.clear;
        }

        void LateUpdate()
        {
            int myTeam = LocalTeam();
            Color want =
                _team.TeamId == Team.None ? Neutral :
                myTeam == Team.None ? Neutral :
                _team.TeamId == myTeam ? Friendly : Enemy;

            if (want == _applied) return;
            _applied = want;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, want);
                r.SetPropertyBlock(_mpb);
            }
        }

        static int LocalTeam()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
                return Team.None;
            var tm = nm.LocalClient.PlayerObject.GetComponent<TeamMember>();
            return tm != null ? tm.TeamId : Team.None;
        }
    }
}
