using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// "Der Koerper" (Etappe 1): Zielen ueber die rechte Maustaste, Ducken (Strg)
    /// und Schleichen (Alt).
    ///
    /// NICHT pruefbar: wie sich das Zielen anfuehlt, ob das Zielfernrohr-Bild
    /// gut aussieht, ob die Bewegung "Gewicht" hat. Geprueft wird nur:
    ///  - Ducken senkt die Augenhoehe und die Kapsel und macht langsamer.
    ///  - Schleichen macht (fuer Gegner-Bots) unhoerbar.
    ///  - Die Ziel-Taste schlaegt server-autoritativ als "zielt gerade" durch.
    ///  - Eine Fernrohr-Waffe zoomt beim Zielen die Kamera und zeigt das Rohr.
    /// </summary>
    public sealed class AimAndCrouchTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static IEnumerator Fixed(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Ducken_senkt_Augenhoehe_und_Kapsel()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            player.SetMovementEnabled(true);

            var cc = player.GetComponent<CharacterController>();
            float standEye = player.AimOrigin.y;
            float standHeight = cc.height;

            var input = new FakePlayerInput { CrouchHeld = true };
            player.SetInputSource(input);
            yield return Fixed(60);

            Assert.Greater(player.Crouch01, 0.8f, "Der Spieler ist nicht in die Hocke gegangen.");
            Assert.Less(player.AimOrigin.y, standEye - 0.3f,
                $"Die Augenhoehe ist beim Ducken nicht gesunken ({standEye:F2} -> {player.AimOrigin.y:F2}).");
            Assert.Less(cc.height, standHeight - 0.3f,
                $"Die Kapsel ist beim Ducken nicht kleiner geworden ({standHeight:F2} -> {cc.height:F2}).");

            // Wieder aufstehen
            input.CrouchHeld = false;
            yield return Fixed(60);
            Assert.Less(player.Crouch01, 0.2f, "Der Spieler ist nicht wieder aufgestanden.");
        }

        [UnityTest]
        public IEnumerator Ducken_macht_langsamer_als_normales_Gehen()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.ClearArena();

            // 1) Normal vorwaerts gehen
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            player.SetMovementEnabled(true);
            var input = new FakePlayerInput { Move = new Vector2(0f, 1f) };
            player.SetInputSource(input);
            yield return Fixed(25);
            Vector3 a0 = player.transform.position;
            yield return Fixed(50);
            float walkDist = Vector3.Distance(a0, player.transform.position);

            // 2) Geduckt vorwaerts
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            player.SetMovementEnabled(true);
            input.CrouchHeld = true;
            yield return Fixed(45);   // erst ganz runter ducken
            Vector3 b0 = player.transform.position;
            yield return Fixed(50);
            float crouchDist = Vector3.Distance(b0, player.transform.position);

            Assert.Greater(walkDist, 0.5f, "Der Spieler ist im Gehen gar nicht vom Fleck gekommen.");
            Assert.Less(crouchDist, walkDist * 0.6f,
                $"Geduckt war nicht deutlich langsamer (gehen={walkDist:F2}, geduckt={crouchDist:F2}).");
        }

        [UnityTest]
        public IEnumerator Schleichen_ist_fuer_Gegner_nicht_hoerbar()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.ClearArena();
            int enemyTeam = Team.Opponent(player.GetComponent<TeamMember>().TeamId);

            // Normal gehen -> Schritte sind hoerbar
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            player.SetMovementEnabled(true);
            var input = new FakePlayerInput { Move = new Vector2(0f, 1f) };
            player.SetInputSource(input);
            yield return Fixed(90);
            bool heardWalking = SoundEvents.TryHear(player.transform.position, enemyTeam, 1f, out _);
            Assert.IsTrue(heardWalking, "Normale Schritte haetten hoerbar sein muessen.");

            // Schleichen (Alt) -> nichts mehr
            SoundEvents.Reset();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            player.SetMovementEnabled(true);
            input.WalkHeld = true;
            yield return Fixed(90);
            bool heardSneaking = SoundEvents.TryHear(player.transform.position, enemyTeam, 1f, out _);
            Assert.IsFalse(heardSneaking, "Schleichen darf fuer Gegner nicht hoerbar sein.");
        }

        [UnityTest]
        public IEnumerator Zielen_Taste_schlaegt_server_autoritativ_durch()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);

            var input = new FakePlayerInput();
            player.SetInputSource(input);
            yield return Fixed(10);
            Assert.IsFalse(player.ServerAimHeld, "Ohne Tastendruck sollte nicht gezielt werden.");

            input.AimHeld = true;
            yield return Fixed(20);
            Assert.IsTrue(player.ServerAimHeld, "Die Ziel-Taste kam nicht beim Server an.");
            Assert.Greater(player.Aim01, 0.5f, "Die Ziel-Blende ist nicht aufgegangen.");

            // Beim Sprinten zaehlt Zielen nicht (Streuungs-Vorteil waere sonst ein Exploit).
            input.Sprint = true;
            yield return Fixed(10);
            Assert.IsFalse(player.ServerAimHeld, "Zielen darf beim Sprinten nicht zaehlen.");
        }

        [UnityTest]
        public IEnumerator Zielfernrohr_zoomt_die_Kamera_und_zeigt_das_Rohr()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            var cam = Camera.main;
            float baseFov = cam.fieldOfView;

            var weapon = player.GetComponent<NetworkWeapon>();
            var input = new FakePlayerInput();
            player.SetInputSource(input);

            // Sturmgewehr (Standard aus dem Prefab) hat kein Fernrohr.
            input.AimHeld = true;
            yield return Fixed(40);
            Assert.Less(player.ScopeAmount01, 0.05f, "Das Sturmgewehr sollte kein Fernrohr-Bild haben.");

            // Scharfschuetzengewehr = Katalog-Index 2, ScopeZoom 4.
            input.AimHeld = false;
            weapon.ServerSetPrimary(2);
            yield return Fixed(30);
            input.AimHeld = true;
            yield return Fixed(60);

            Assert.Greater(player.ScopeAmount01, 0.5f, "Das Zielfernrohr-Bild ist nicht aufgezogen.");
            Assert.Less(cam.fieldOfView, baseFov - 20f,
                $"Die Kamera hat beim Zielen nicht gezoomt ({baseFov:F0} -> {cam.fieldOfView:F0}).");

            input.AimHeld = false;
            yield return Fixed(60);
            Assert.Less(player.ScopeAmount01, 0.1f, "Das Fernrohr-Bild ist nicht wieder verschwunden.");
            Assert.Greater(cam.fieldOfView, baseFov - 5f, "Die Kamera ist nicht wieder herausgezoomt.");
        }
    }
}
