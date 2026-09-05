using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>Prueft die begehbare Karte selbst, nicht Sollwerte des SceneBuilders.</summary>
    public sealed class MidMapTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static BoxCollider ColliderOf(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "Kartenteil fehlt: " + name);
            var col = go.GetComponent<BoxCollider>();
            Assert.IsNotNull(col, "Begehbarer Kartenteil ohne Collider: " + name);
            return col;
        }

        static float SurfaceY(Collider col, float x, float z)
        {
            bool hit = col.Raycast(new Ray(new Vector3(x, col.bounds.max.y + 2f, z),
                                           Vector3.down), out var result, 20f);
            Assert.IsTrue(hit, $"Keine Laufflaeche auf {col.name} bei x={x}, z={z}.");
            return result.point.y;
        }

        static (string deck, string ramp)[] Ramps => new[]
        {
            ("MidDais", "MidRamp_A"), ("MidDais", "MidRamp_B"),
            ("Balc_L", "BalcRamp_A_L"), ("Balc_L", "BalcRamp_B_L"),
            ("Balc_R", "BalcRamp_A_R"), ("Balc_R", "BalcRamp_B_R"),
        };

        [UnityTest]
        public IEnumerator Beide_Mittelrampen_haben_einen_sichtbar_freien_Zugang()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            var deck = ColliderOf("MidDais").bounds;
            var renderers = GameObject.Find("Map").GetComponentsInChildren<MeshRenderer>();
            foreach (string side in new[] { "A", "B" })
            {
                var ramp = ColliderOf("MidRamp_" + side).bounds;
                float edgeZ = ramp.center.z < deck.center.z ? deck.min.z : deck.max.z;
                // Der gesamte physisch begehbare Rampenquerschnitt muss frei
                // von sichtbaren Holmen/Pfosten sein, nicht nur seine Mittellinie.
                var passage = new Bounds(new Vector3(ramp.center.x, deck.max.y + 1f, edgeZ),
                                         new Vector3(ramp.size.x, 1.9f, 0.3f));
                int parts = 0;
                foreach (var r in renderers)
                {
                    if (!r.name.StartsWith("MidRail_" + side + "_")) continue;
                    parts++;
                    Assert.IsFalse(r.bounds.Intersects(passage),
                        $"{r.name} steht sichtbar quer im Zugang von {side}.");
                }
                Assert.Greater(parts, 4, "Das Gelaender darf nicht einfach verschwinden.");
            }
        }

        [UnityTest]
        public IEnumerator Rampen_treffen_Boden_und_Podest_ohne_Stufe()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            foreach (var pair in Ramps)
            {
                var deck = ColliderOf(pair.deck);
                var ramp = ColliderOf(pair.ramp);
                float sign = Mathf.Sign(ramp.bounds.center.z - deck.bounds.center.z);
                float edgeZ = sign > 0f ? deck.bounds.max.z : deck.bounds.min.z;
                float x = ramp.bounds.center.x;
                float outside = SurfaceY(ramp, x, edgeZ + sign * 0.03f);
                float inside = SurfaceY(deck, x, edgeZ - sign * 0.03f);
                Assert.Less(Mathf.Abs(outside - inside), 0.025f,
                    $"{pair.ramp} trifft {pair.deck} mit einer Stufe von {outside - inside:F3} m.");

                // Ein Punkt knapp innerhalb der unteren sichtbaren Stirnkante.
                float lowZ = (sign > 0f ? ramp.bounds.max.z : ramp.bounds.min.z) - sign * 0.15f;
                float low = SurfaceY(ramp, x, lowZ);
                float floor = float.NegativeInfinity;
                foreach (var ground in GameObject.Find("Ground").GetComponentsInChildren<Collider>())
                    if (ground.Raycast(new Ray(new Vector3(x, 2f, lowZ), Vector3.down),
                                       out var hit, 4f)) floor = Mathf.Max(floor, hit.point.y);
                Assert.IsFalse(float.IsNegativeInfinity(floor), "Kein Boden unter dem Rampenanfang.");
                Assert.Less(Mathf.Abs(low - floor), 0.06f,
                    $"{pair.ramp} beginnt {low - floor:F3} m ueber dem Boden.");
            }
        }

        [UnityTest]
        public IEnumerator Bots_erreichen_jedes_Podest_von_beiden_Rampen()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            foreach (var pair in Ramps)
            {
                var deck = ColliderOf(pair.deck);
                var ramp = ColliderOf(pair.ramp);
                float sign = Mathf.Sign(ramp.bounds.center.z - deck.bounds.center.z);
                float edgeZ = sign > 0f ? deck.bounds.max.z : deck.bounds.min.z;
                float x = ramp.bounds.center.x;
                var approach = new Vector3(x, SurfaceY(ramp, x, edgeZ + sign * 1.5f),
                                           edgeZ + sign * 1.5f);
                var landing = new Vector3(x, deck.bounds.max.y, edgeZ - sign * 1.5f);
                Assert.IsTrue(NavMesh.SamplePosition(approach, out var from, 0.3f, NavMesh.AllAreas),
                    "Rampe fehlt im NavMesh: " + pair.ramp);
                Assert.IsTrue(NavMesh.SamplePosition(landing, out var to, 0.3f, NavMesh.AllAreas),
                    "Podest-Landung fehlt im NavMesh: " + pair.deck);
                var path = new NavMeshPath();
                Assert.IsTrue(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                    $"Kein vollstaendiger Weg von {pair.ramp} auf {pair.deck}.");
                Assert.IsTrue(NavMesh.CalculatePath(to.position, from.position, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                    $"Kein vollstaendiger Rueckweg von {pair.deck} auf {pair.ramp}.");
            }
        }

        [UnityTest]
        public IEnumerator Spieler_geht_beide_Mittelrampen_ohne_Sprung_hoch_und_hinunter()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var input = new FakePlayerInput();
            player.SetInputSource(input);
            foreach (int side in new[] { -1, 1 })
            {
                foreach (bool uphill in new[] { true, false })
                {
                    input.Move = Vector2.zero;
                    input.LookYaw = (uphill ? -side : side) > 0 ? 0f : 180f;
                    MatchTestHarness.PlacePlayer(player,
                        new Vector3(0f, uphill ? 0.08f : 1.28f, side * (uphill ? 14f : 3.5f)),
                        input.LookYaw);
                    for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
                    player.SetMovementEnabled(true);
                    input.Move = Vector2.up;
                    float elapsed = 0f;
                    bool reached = false;
                    while (elapsed < 6f)
                    {
                        yield return new WaitForFixedUpdate();
                        elapsed += Time.fixedDeltaTime;
                        float z = side * player.transform.position.z;
                        reached = uphill ? z <= 3.5f : z >= 14f;
                        if (reached) break;
                    }
                    input.Move = Vector2.zero;
                    Assert.IsTrue(reached,
                        $"Spieler bleibt an Mittelrampe {side} ({(uphill ? "hoch" : "hinunter")}) bei {player.transform.position} haengen.");
                    Assert.AreEqual(uphill ? 1.2f : 0f, player.transform.position.y, 0.12f,
                        "Spieler hat die Laufhoehe am Ziel nicht erreicht.");
                }
            }
        }
    }
}
