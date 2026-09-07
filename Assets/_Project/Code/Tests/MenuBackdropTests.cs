using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>Prüft die technische Struktur der 3D-Menü-Kulisse.</summary>
    public sealed class MenuBackdropTests
    {
        [UnityTest]
        public IEnumerator Menue_hat_Operator_und_Dreipunktlicht()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MenuScene);
            yield return null;
            yield return null;

            Assert.IsNotNull(GameObject.Find("Backdrop"), "Die 3D-Kulisse fehlt.");
            var operatorGo = GameObject.Find("BD_Operator");
            Assert.IsNotNull(operatorGo, "Der Operator fehlt in der Menü-Kulisse.");
            Assert.IsNotNull(operatorGo.GetComponent<MenuOperatorMotion>(), "Der Operator bewegt sich nicht dezent.");
            Assert.Greater(operatorGo.GetComponentsInChildren<Renderer>(true).Length, 0,
                "Der Operator hat kein sichtbares Modell.");
            Assert.AreEqual(0, operatorGo.GetComponentsInChildren<Collider>(true).Length,
                "Die Menüfigur darf keine Spiel-Kollision besitzen.");

            foreach (var lightName in new[] { "BD_Operator_Key", "BD_Operator_Rim", "BD_Operator_Fill" })
                Assert.IsNotNull(GameObject.Find(lightName)?.GetComponent<Light>(),
                    "Licht der Menüfigur fehlt: " + lightName);
        }
    }
}
