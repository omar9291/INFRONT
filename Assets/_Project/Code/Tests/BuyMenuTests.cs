using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Gruppe C.3 - Kaufmenue mit Geld: kaufen zieht Geld ab, zu wenig Geld
    /// kauft nicht, kaufen nur in der Kaufzeit, wer stirbt verliert die Waffe,
    /// wer ueberlebt behaelt sie, Rundensieg bringt mehr Geld als Niederlage,
    /// die Weste halbiert den Koerperschaden.
    /// </summary>
    public sealed class BuyMenuTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static List<BotBrain> BotsOnTeam(int team)
        {
            var list = new List<BotBrain>();
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId == team)
                    list.Add(b);
            return list;
        }

        // Index der Spieler-Sturmgewehr / -MP im Katalog (siehe SceneBuilder).
        const int PlayerSturmgewehr = 0;
        const int PlayerMp = 1;
        const int BuyEntryMp = 0;
        const int BuyEntrySturmgewehr = 1;

        [UnityTest]
        public IEnumerator Kauf_zieht_Geld_ab_und_gibt_die_Waffe()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            var weapon = player.GetComponent<NetworkWeapon>();
            var wallet = player.GetComponent<Wallet>();
            var agent = player.GetComponent<PurchaseAgent>();

            weapon.ServerSetPistolOnly();
            wallet.ServerSet(10000);
            match.ForceBuyTimeForTests = true;
            yield return null;

            Assert.IsTrue(agent.ServerBuyWeapon(BuyEntrySturmgewehr), "Kauf abgelehnt.");
            yield return null;

            Assert.AreEqual(10000 - 2700, wallet.Money, "Falscher Preis abgezogen.");
            Assert.IsTrue(weapon.HasPrimary, "Keine Primaerwaffe nach Kauf.");
            Assert.AreEqual(PlayerSturmgewehr, weapon.PrimaryIndex, "Falsche Waffe gegeben.");
        }

        [UnityTest]
        public IEnumerator Zu_wenig_Geld_kein_Kauf()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            var weapon = player.GetComponent<NetworkWeapon>();
            var wallet = player.GetComponent<Wallet>();
            var agent = player.GetComponent<PurchaseAgent>();

            weapon.ServerSetPistolOnly();
            wallet.ServerSet(1000);   // Scharfschuetzengewehr kostet 4750
            match.ForceBuyTimeForTests = true;
            yield return null;

            Assert.IsFalse(agent.ServerBuyWeapon(BuyEntrySturmgewehr), "Kauf trotz zu wenig Geld.");
            Assert.AreEqual(1000, wallet.Money, "Geld trotzdem abgezogen.");
            Assert.IsFalse(weapon.HasPrimary, "Waffe trotzdem gegeben.");
        }

        [UnityTest]
        public IEnumerator Kauf_nur_in_der_Kaufzeit()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            var weapon = player.GetComponent<NetworkWeapon>();
            var wallet = player.GetComponent<Wallet>();
            var agent = player.GetComponent<PurchaseAgent>();

            weapon.ServerSetPistolOnly();
            wallet.ServerSet(10000);
            match.ForceBuyTimeForTests = false;
            match.ServerApplyTestConfig(15, 999f, 5f);   // setzt die Kauf-Endzeit auf 0
            yield return null;
            Assert.IsFalse(match.IsBuyTime, "Kaufzeit sollte vorbei sein.");

            Assert.IsFalse(agent.ServerBuyWeapon(BuyEntryMp), "Kauf ausserhalb der Kaufzeit.");
            Assert.AreEqual(10000, wallet.Money);
            Assert.IsFalse(weapon.HasPrimary);
        }

        [UnityTest]
        public IEnumerator Wer_stirbt_verliert_die_Primaerwaffe()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });
            match.SuspendedForTests = false;

            var weapon = player.GetComponent<NetworkWeapon>();
            var wallet = player.GetComponent<Wallet>();
            var agent = player.GetComponent<PurchaseAgent>();

            weapon.ServerSetPistolOnly();
            wallet.ServerSet(10000);
            match.ForceBuyTimeForTests = true;
            yield return null;
            Assert.IsTrue(agent.ServerBuyWeapon(BuyEntryMp), "Kauf abgelehnt.");
            Assert.IsTrue(weapon.HasPrimary);
            match.ForceBuyTimeForTests = false;

            player.GetComponent<Health>().ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            yield return null;

            match.StartRound();
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();

            Assert.IsFalse(weapon.HasPrimary, "Toter hat seine Primaerwaffe behalten.");
        }

        [UnityTest]
        public IEnumerator Wer_ueberlebt_behaelt_die_Primaerwaffe()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });
            match.SuspendedForTests = false;

            var weapon = player.GetComponent<NetworkWeapon>();
            var wallet = player.GetComponent<Wallet>();
            var agent = player.GetComponent<PurchaseAgent>();

            weapon.ServerSetPistolOnly();
            wallet.ServerSet(10000);
            match.ForceBuyTimeForTests = true;
            yield return null;
            Assert.IsTrue(agent.ServerBuyWeapon(BuyEntryMp), "Kauf abgelehnt.");
            match.ForceBuyTimeForTests = false;

            // Spieler lebt -> Rundenstart darf die Waffe nicht wegnehmen
            match.StartRound();
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();

            Assert.IsTrue(weapon.HasPrimary, "Ueberlebender hat seine Waffe verloren.");
            Assert.AreEqual(PlayerMp, weapon.PrimaryIndex, "Falsche Waffe nach Rundenstart.");
        }

        [UnityTest]
        public IEnumerator Rundensieg_gibt_mehr_Geld_als_Niederlage()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            int enemyTeam = Team.Opponent(myTeam);

            var myWallet = player.GetComponent<Wallet>();
            var enemyBot = BotsOnTeam(enemyTeam)[0];
            var enemyWallet = enemyBot.GetComponent<Wallet>();
            Assert.IsNotNull(enemyWallet, "Bot ohne Wallet - Prefab nicht neu gebaut?");

            // Geld festhalten, solange noch alles ausgesetzt ist (keine Bot-Kaeufe).
            int myBefore = myWallet.Money;
            int enemyBefore = enemyWallet.Money;

            match.SuspendedForTests = false;

            foreach (var e in BotsOnTeam(enemyTeam))
                e.GetComponent<Health>().ApplyDamage(9999, player.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(MatchManager.Phase.RoundOver, match.CurrentPhase, "Runde endete nicht.");

            int myGain = myWallet.Money - myBefore;
            int enemyGain = enemyWallet.Money - enemyBefore;

            Assert.Greater(myGain, enemyGain, $"Sieger-Geld ({myGain}) nicht groesser als Verlierer-Geld ({enemyGain}).");
            Assert.GreaterOrEqual(myGain, 3000, "Rundensieg bringt zu wenig.");
            Assert.Greater(enemyGain, 0, "Verlierer bekommt gar nichts.");
        }

        [UnityTest]
        public IEnumerator Weste_halbiert_den_Koerperschaden()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bot = BotsOnTeam(Team.Opponent(player.GetComponent<TeamMember>().TeamId))[0];
            var hp = bot.GetComponent<Health>();

            hp.ResetFull(); hp.ServerClearArmor();
            hp.ApplyDamage(40, (GameObject)null);
            yield return null;
            int dropOhne = hp.Max - hp.Current;

            hp.ResetFull(); hp.ServerGiveArmor(hp.MaxArmor);
            hp.ApplyDamage(40, (GameObject)null);
            yield return null;
            int dropMit = hp.Max - hp.Current;

            Assert.AreEqual(40, dropOhne, "Ohne Weste sollte voller Schaden ankommen.");
            Assert.Less(dropMit, dropOhne, "Weste hat den Schaden nicht verringert.");
            Assert.AreEqual(20, dropMit, "Weste sollte die Haelfte schlucken.");

            // Kopfschuss ignoriert die Weste
            hp.ResetFull(); hp.ServerGiveArmor(hp.MaxArmor);
            hp.ApplyDamage(40, null, true);
            yield return null;
            Assert.AreEqual(40, hp.Max - hp.Current, "Kopfschuss sollte an der Weste vorbeigehen.");
        }
    }
}
