using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Gibt einem Bot im Bomben-Modus ein Ziel: legen, die Bombe eskortieren,
    /// einen Platz bewachen oder entschaerfen. Steuert NICHT den Kampf - sobald
    /// der <see cref="BotBrain"/> einen Gegner sieht, kaempft er wie immer und
    /// kehrt danach zum Auftrag zurueck.
    ///
    /// Laeuft nur auf dem Server. Setzt bei jedem Tick:
    ///  - <see cref="BotBrain.ServerSetObjective"/> / <see cref="BotBrain.ServerClearObjective"/>
    ///  - <see cref="BombAction.ServerSetUsing"/> (E halten zum Legen/Entschaerfen)
    /// </summary>
    [RequireComponent(typeof(BotBrain))]
    [RequireComponent(typeof(TeamMember))]
    public sealed class BotObjective : NetworkBehaviour
    {
        // Wie nah der Bot am Platz / an der Bombe sein muss, um E zu halten.
        const float DefuseHoldRange = 2.8f;
        const float Interval = 0.25f;

        BotBrain _brain;
        TeamMember _team;
        Health _health;
        BombAction _bomb;

        float _tick;

        void Awake()
        {
            _brain = GetComponent<BotBrain>();
            _team = GetComponent<TeamMember>();
            _health = GetComponent<Health>();
            _bomb = GetComponent<BombAction>();
        }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;

            _tick -= Time.deltaTime;
            if (_tick > 0f) return;
            _tick = Interval;

            var mm = MatchManager.Instance;
            var bomb = Bomb.Instance;

            bool canPlay = mm != null && bomb != null && mm.IsBombMode
                           && mm.CurrentPhase == MatchManager.Phase.Playing
                           && _health != null && _health.IsAlive;

            if (!canPlay)
            {
                _brain.ServerClearObjective();
                _bomb?.ServerSetUsing(false);
                return;
            }

            if (_team.TeamId == mm.AttackingTeam)
                TickAttacker(bomb);
            else
                TickDefender(bomb);
        }

        void TickAttacker(Bomb bomb)
        {
            // Bombe schon gelegt -> den Platz halten, kein E mehr.
            if (bomb.IsPlanted)
            {
                _bomb?.ServerSetUsing(false);
                _brain.ServerSetObjective(bomb.transform.position);
                return;
            }

            // Ich trage die Bombe -> zum naechsten Platz und legen.
            if (bomb.IsCarriedBy(gameObject))
            {
                int siteHere = BombSite.SiteAt(transform.position);
                if (siteHere >= 0)
                {
                    // Auf dem Platz: stehen bleiben und E halten.
                    _bomb?.ServerSetUsing(true);
                    _brain.ServerSetObjective(transform.position);
                }
                else
                {
                    _bomb?.ServerSetUsing(false);
                    _brain.ServerSetObjective(NearestSiteCenter(transform.position));
                }
                return;
            }

            // Ein Mitspieler traegt sie -> mitlaufen und schuetzen.
            _bomb?.ServerSetUsing(false);
            if (bomb.IsCarried)
            {
                var carrier = ResolveCarrier(bomb);
                _brain.ServerSetObjective(carrier != null
                    ? carrier.transform.position
                    : NearestSiteCenter(transform.position));
            }
            else if (bomb.CurrentState == Bomb.State.Dropped)
            {
                // Bombe liegt am Boden -> hinlaufen (aufheben passiert automatisch).
                _brain.ServerSetObjective(bomb.transform.position);
            }
            else
            {
                _brain.ServerSetObjective(NearestSiteCenter(transform.position));
            }
        }

        void TickDefender(Bomb bomb)
        {
            if (bomb.IsPlanted)
            {
                // Zur Bombe, und in Reichweite E halten zum Entschaerfen.
                Vector3 bombPos = bomb.transform.position;
                _brain.ServerSetObjective(bombPos);
                bool close = Vector3.Distance(transform.position, bombPos) <= DefuseHoldRange;
                _bomb?.ServerSetUsing(close);
                return;
            }

            // Noch nicht gelegt: die Verteidiger auf die Plaetze aufteilen.
            _bomb?.ServerSetUsing(false);
            _brain.ServerSetObjective(GuardSiteCenter());
        }

        // ----------------------------------------------------------------

        Vector3 NearestSiteCenter(Vector3 from)
        {
            Vector3 best = from;
            float bestDist = float.MaxValue;
            foreach (var site in BombSite.All)
            {
                if (site == null) continue;
                float d = Vector3.Distance(from, site.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = site.transform.position;
                }
            }
            return best;
        }

        Vector3 GuardSiteCenter()
        {
            int count = BombSite.All.Count;
            if (count == 0) return transform.position;
            // Nach Team-Slot aufteilen: Slot 1 -> Platz 0, Slot 2 -> Platz 1, ...
            int slot = Mathf.Max(0, _team.Slot - 1);
            var site = BombSite.All[slot % count];
            return site != null ? site.transform.position : transform.position;
        }

        static TeamMember ResolveCarrier(Bomb bomb)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            return nm.SpawnManager.SpawnedObjects.TryGetValue(bomb.CarrierId, out var no) && no != null
                ? no.GetComponent<TeamMember>()
                : null;
        }
    }
}
