using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Faerbt die Teamkennzeichen der Figur relativ zum lokalen Spieler:
    /// eigenes Team blau, Gegner rot. Laeuft auf jedem Client fuer dessen
    /// eigene Sicht.
    ///
    /// WICHTIG - was hier NICHT mehr passiert: bis zum 2026-09-05 hat diese
    /// Klasse die Grundfarbe JEDES Renderers der Figur ueberschrieben. Beim
    /// Gegner war das (1, 0.35, 0.30), flaechig ueber Gesicht, Haende und
    /// Kleidung. Das war der Grund, warum die Bots auf den Rundgangsbildern
    /// wie lachsfarbene Plastikpuppen aussahen, obwohl laengst ein echtes
    /// Modell mit Animationen dahintersteckt.
    ///
    /// Jetzt gilt: gibt es an der Figur <see cref="TeamMarker"/>-Teile
    /// (Armbinden, Rueckenpanel), wird NUR die gefaerbt. Gibt es keine - etwa
    /// bei der alten Wuerfel-Figur, wenn kein Modell vorhanden ist - bleibt es
    /// beim alten Verhalten, damit die Mannschaft dort erkennbar bleibt.
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
            _renderers = SammleRenderer();
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Nach dem nachtraeglichen Aufbau der Figur (<see cref="CharacterVisual"/>)
        /// aufrufen - sammelt die neuen Renderer ein und faerbt beim naechsten
        /// LateUpdate neu.</summary>
        public void RefreshRenderers()
        {
            _renderers = SammleRenderer();
            _applied = Color.clear;
        }

        /// <summary>Nur die Kennzeichen - und nur wenn es welche gibt.</summary>
        Renderer[] SammleRenderer()
        {
            var marken = GetComponentsInChildren<TeamMarker>(true);
            if (marken.Length > 0)
            {
                var liste = new System.Collections.Generic.List<Renderer>();
                foreach (var m in marken)
                {
                    var r = m.GetComponent<Renderer>();
                    if (r != null) liste.Add(r);
                }
                if (liste.Count > 0) return liste.ToArray();
            }
            // Rueckfall: Wuerfel-Figur ohne Kennzeichen. Ohne diesen Zweig
            // waeren dort Freund und Feind nicht mehr zu unterscheiden.
            return GetComponentsInChildren<Renderer>(true);
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
