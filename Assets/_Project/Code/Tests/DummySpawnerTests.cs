using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    public sealed class DummySpawnerTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [UnityTest]
        public IEnumerator Normales_Match_enthaelt_keine_Trainingsziele()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            Assert.IsNotNull(Object.FindAnyObjectByType<DummySpawner>(),
                "Das Trainingssystem soll erhalten bleiben.");
            Assert.AreEqual(0, Object.FindObjectsByType<TargetDummy>(FindObjectsSortMode.None).Length,
                "Ein Trainingsziel ist ohne ausdruecklichen Auftrag im normalen Match gespawnt.");
        }

        [UnityTest]
        public IEnumerator Ausdruecklich_angefordertes_Trainingsziel_steht_auf_dem_Boden()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { }, withTrainingDummy: true);
            var targets = Object.FindObjectsByType<TargetDummy>(FindObjectsSortMode.None);
            Assert.AreEqual(1, targets.Length, "Der Test muss genau sein angefordertes Ziel erhalten.");
            var body = targets[0].transform.Find("Body").GetComponent<Renderer>();
            float floor = float.NegativeInfinity;
            var ray = new Ray(body.bounds.center + Vector3.up * 2f, Vector3.down);
            foreach (var col in GameObject.Find("Ground").GetComponentsInChildren<Collider>())
                if (col.Raycast(ray, out var hit, 8f)) floor = Mathf.Max(floor, hit.point.y);
            Assert.IsFalse(float.IsNegativeInfinity(floor), "Kein Boden unter dem Trainingsziel.");
            Assert.AreEqual(floor, body.bounds.min.y, 0.025f,
                "Das Trainingsziel schwebt ueber dem Boden oder steckt darin.");
        }
    }
}
