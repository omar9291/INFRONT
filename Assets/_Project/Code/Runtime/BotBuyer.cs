using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Laesst einen Bot zu Beginn der Kaufzeit automatisch einkaufen:
    /// die teuerste Waffe, die er sich leisten kann, danach eine Weste,
    /// wenn noch genug uebrig ist. Mit etwas Zufall, damit nicht alle Bots
    /// gleich einkaufen (manchmal spart einer).
    ///
    /// Laeuft nur auf dem Server.
    /// </summary>
    [RequireComponent(typeof(PurchaseAgent))]
    [RequireComponent(typeof(Wallet))]
    public sealed class BotBuyer : NetworkBehaviour
    {
        PurchaseAgent _agent;
        Wallet _wallet;
        float _delay;
        bool _boughtThisRound;
        int _lastRoundStamp = -1;

        void Awake()
        {
            _agent = GetComponent<PurchaseAgent>();
            _wallet = GetComponent<Wallet>();
        }

        void Update()
        {
            if (!IsServer) return;

            var mm = MatchManager.Instance;
            if (mm == null || mm.SuspendedForTests) return;

            if (mm.CurrentPhase != MatchManager.Phase.Playing)
                return;

            // Neue Runde erkennen: der Kauf-Endzeitpunkt springt bei jedem
            // Rundenstart nach vorne.
            int stamp = Mathf.RoundToInt((float)mm.BuyEndTime * 4f);
            if (stamp != _lastRoundStamp)
            {
                _lastRoundStamp = stamp;
                _boughtThisRound = false;
                _delay = Random.Range(0.3f, 2.5f);
            }

            if (_boughtThisRound || !mm.IsBuyTime) return;

            _delay -= Time.deltaTime;
            if (_delay > 0f) return;

            _boughtThisRound = true;
            ServerAutoBuy();
        }

        void ServerAutoBuy()
        {
            var catalog = _agent.Catalog;
            if (catalog == null) return;

            // Ab und zu eine Sparrunde (nur mit Pistole), wenn man noch nicht
            // reich ist.
            if (_wallet.Money < 4000 && Random.value < 0.15f)
                return;

            // Teuerste kaufbare Waffe zuerst.
            int bestIndex = -1;
            int bestPrice = -1;
            for (int i = 0; i < catalog.BuyEntries.Length; i++)
            {
                int price = catalog.BuyEntries[i].Price;
                if (price <= _wallet.Money && price > bestPrice)
                {
                    bestPrice = price;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
                _agent.ServerBuyWeapon(bestIndex);

            // Weste, wenn danach noch ein Polster bleibt.
            if (_wallet.Money >= _agent.ArmorPrice + 500)
                _agent.ServerBuyArmor();

            // Eine Faehigkeit, wenn noch Geld da ist (mal Rauch, mal Blend).
            var abilities = _agent.AbilityCatalog;
            if (abilities != null && abilities.Abilities.Length > 0)
            {
                int start = Random.Range(0, abilities.Abilities.Length);
                for (int n = 0; n < abilities.Abilities.Length; n++)
                {
                    int i = (start + n) % abilities.Abilities.Length;
                    var a = abilities.Abilities[i];
                    if (a != null && _wallet.Money >= a.Price + 300)
                    {
                        _agent.ServerBuyAbility(i);
                        break;
                    }
                }
            }
        }
    }
}
