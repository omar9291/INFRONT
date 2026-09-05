using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Bomben-Hinweise fuer den lokalen Spieler: "Du traegst die Bombe",
    /// Legen-/Entschaerfen-Balken, Aufforderung "E halten".
    ///
    /// Gezeichnet wird das im <see cref="HudController"/> (UI Toolkit) - diese
    /// Klasse ermittelt nur pro Frame den passenden Text und Fortschritt und
    /// reicht ihn weiter. Laeuft nur beim Besitzer.
    /// </summary>
    public sealed class BombHud : NetworkBehaviour
    {
        Health _health;
        TeamMember _team;

        static readonly Color Orange = new Color(1f, 0.6f, 0.1f);
        static readonly Color Blue = new Color(0.3f, 0.7f, 1f);

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _health = GetComponent<Health>();
            _team = GetComponent<TeamMember>();
        }

        void LateUpdate()
        {
            var hud = HudController.Instance;
            if (hud == null) return;

            var mm = MatchManager.Instance;
            var bomb = Bomb.Instance;
            if (mm == null || bomb == null || !mm.IsBombMode || _health == null || !_health.IsAlive)
            {
                hud.SetBombPrompt(null, 0f, Orange);
                return;
            }

            bool iAmAttacker = _team != null && _team.TeamId == mm.AttackingTeam;
            bool iAmDefender = _team != null && _team.TeamId == mm.DefendingTeam;
            bool iCarry = bomb.IsCarriedBy(gameObject);

            // --- Traeger: Bombe legen ---
            if (iCarry)
            {
                int site = BombSite.SiteAt(transform.position);
                if (site >= 0)
                {
                    float p = bomb.PlantProgress01;
                    if (p > 0f) hud.SetBombPrompt(GameText.Bomb.PlantingBomb, p, Orange);
                    else hud.SetBombPrompt(GameText.Bomb.HoldEToPlant, 0f, Orange);
                }
                else
                {
                    hud.SetBombPrompt(GameText.Bomb.CarryBomb, 0f, Orange);
                }
                return;
            }

            // --- Angreifer ohne Bombe, noch nicht gelegt ---
            if (iAmAttacker && !bomb.IsPlanted)
            {
                if (bomb.CurrentState == Bomb.State.Dropped)
                    hud.SetBombPrompt(GameText.Bomb.DroppedBomb, 0f, Orange);
                else if (bomb.IsCarried)
                    hud.SetBombPrompt(GameText.Bomb.TeammateHasBomb, 0f, Orange);
                else
                    hud.SetBombPrompt(null, 0f, Orange);
                return;
            }

            // --- Verteidiger: entschaerfen ---
            if (iAmDefender && bomb.IsPlanted)
            {
                float dist = Vector3.Distance(transform.position, bomb.transform.position);
                if (dist <= 3.5f)
                {
                    float p = bomb.DefuseProgress01;
                    if (p > 0f) hud.SetBombPrompt(GameText.Bomb.Defusing, p, Blue);
                    else hud.SetBombPrompt(GameText.Bomb.HoldEToDefuse, 0f, Blue);
                }
                else hud.SetBombPrompt(null, 0f, Blue);
                return;
            }

            // --- Angreifer ohne Bombe, gelegt ---
            if (iAmAttacker && bomb.IsPlanted)
            {
                hud.SetBombPrompt(GameText.Bomb.ProtectBomb, 0f, Orange);
                return;
            }

            hud.SetBombPrompt(null, 0f, Orange);
        }
    }
}
