using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die Faehigkeiten-Maschine (Etappe C): Bestueckung, Ladungen, der Server
    /// lehnt unerlaubte Nutzung ab.
    ///
    /// NICHT pruefbar: wie Rauch/Blitz aussehen.
    /// </summary>
    public sealed class AbilityTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [UnityTest]
        public IEnumerator Faehigkeit_geben_und_zuenden_verbraucht_eine_Ladung()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var holder = player.GetComponent<AbilityHolder>();
            Assert.IsNotNull(holder, "Spieler ohne AbilityHolder.");

            Assert.IsTrue(holder.ServerGrant(AbilityKind.Rauchwand), "Rauchwand konnte nicht gegeben werden.");
            int slot = (int)AbilitySlot.Q;
            Assert.AreEqual(AbilityKind.Rauchwand, holder.KindInSlot(slot));
            Assert.AreEqual(1, holder.ChargesInSlot(slot), "Rauchwand sollte 1 Ladung haben.");

            Assert.IsTrue(holder.ServerTryUse(slot), "Zuenden wurde abgelehnt.");
            yield return null;
            Assert.AreEqual(0, holder.ChargesInSlot(slot), "Ladung wurde nicht abgezogen.");

            // Es liegt jetzt eine Rauchwand in der Welt.
            Assert.IsNotNull(Object.FindAnyObjectByType<SmokeVolume>(), "Keine Rauchwolke erzeugt.");

            // Ohne Ladung geht nichts mehr.
            Assert.IsFalse(holder.ServerTryUse(slot), "Zweiter Einsatz ohne Ladung wurde zugelassen.");
        }

        [UnityTest]
        public IEnumerator Server_lehnt_ungueltige_Nutzung_ab()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var holder = player.GetComponent<AbilityHolder>();

            // Leerer Platz -> abgelehnt
            Assert.IsFalse(holder.ServerTryUse(0), "Leerer Platz haette abgelehnt werden muessen.");
            Assert.IsFalse(holder.ServerTryUse(2), "Leerer Platz haette abgelehnt werden muessen.");
            // Unsinns-Index -> abgelehnt
            Assert.IsFalse(holder.ServerTryUse(5), "Ungueltiger Platz haette abgelehnt werden muessen.");

            // Tot -> abgelehnt
            holder.ServerGrant(AbilityKind.Blendgranate);
            player.GetComponent<Health>().ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return MatchTestHarness.WaitUntil(
                () => !player.GetComponent<Health>().IsAlive, 3f, "Spieler nicht getoetet.");
            Assert.IsFalse(holder.ServerTryUse((int)AbilitySlot.G), "Toter konnte eine Faehigkeit zuenden.");
        }

        [UnityTest]
        public IEnumerator Blendgranate_blendet_einen_Bot_der_hinschaut()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            BotBrain bot = null;
            int myTeam = player.GetComponent<TeamMember>().TeamId;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId != myTeam) { bot = b; break; }
            Assert.IsNotNull(bot);

            MatchTestHarness.ClearArena();
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(bot, new Vector3(0f, 1f, 0f), out _));
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();

            Assert.IsFalse(bot.IsBlind, "Bot war schon geblendet.");

            // Blitz direkt vor die Augen des Bots, in Blickrichtung.
            var flashGo = new GameObject("TestFlash");
            flashGo.transform.position = bot.AimOrigin + bot.transform.forward * 2f;
            var flash = flashGo.AddComponent<FlashBurst>();
            flash.Init(10f, 2f);
            yield return null;
            yield return null;

            Assert.IsTrue(bot.IsBlind, "Der Bot wurde von der Blendgranate nicht geblendet.");
        }

        [UnityTest]
        public IEnumerator Splittergranate_macht_Flaechenschaden()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            BotBrain bot = null;
            int myTeam = player.GetComponent<TeamMember>().TeamId;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId != myTeam) { bot = b; break; }
            Assert.IsNotNull(bot);

            MatchTestHarness.ClearArena();
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(bot, new Vector3(0f, 1f, 0f), out _));
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();

            int hpBefore = bot.GetComponent<Health>().Current;

            var go = new GameObject("TestFrag");
            go.transform.position = bot.transform.position + Vector3.up * 1f;
            go.AddComponent<FragGrenade>().Init(5.5f, player.gameObject, myTeam);

            yield return MatchTestHarness.WaitUntil(
                () => bot.GetComponent<Health>().Current < hpBefore, 3f,
                "Die Splittergranate hat keinen Schaden gemacht.");
        }

        [UnityTest]
        public IEnumerator Scan_Puls_klaert_einen_Gegner_auf()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            BotBrain bot = null;
            int myTeam = player.GetComponent<TeamMember>().TeamId;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId != myTeam) { bot = b; break; }
            Assert.IsNotNull(bot);

            var botTm = bot.GetComponent<TeamMember>();
            Assert.IsFalse(ScanRegistry.IsRevealedTo(botTm, myTeam), "Bot war schon aufgeklaert.");

            var go = new GameObject("TestScan");
            go.transform.position = bot.transform.position;
            go.AddComponent<ScanPulse>().Init(30f, 3f, myTeam);
            yield return null;

            Assert.IsTrue(ScanRegistry.IsRevealedTo(botTm, myTeam),
                "Der Scan-Puls hat den Gegner nicht aufgeklaert.");
        }
    }
}
