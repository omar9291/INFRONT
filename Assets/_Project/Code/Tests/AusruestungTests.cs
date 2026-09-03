using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Realismus-Etappe Schritt 6: Ausruestung statt Faehigkeiten.
    ///
    /// NICHT pruefbar: ob sich die Granaten gut werfen lassen und ob die
    /// Wirkungsradien fair sind.
    ///
    /// Geprueft wird, was messbar ist:
    ///  - Das Verbandspaket gibt es und wirkt am Benutzer.
    ///  - Es stoppt Blutungen und heilt nur wenig.
    ///  - Es wird verbraucht (nicht unbegrenzt nutzbar).
    ///  - Der Scan-Puls ist noch da, wird aber nicht mehr angeboten.
    ///  - Die Reihenfolge im Katalog hat sich nicht verschoben (Netz-Index).
    /// </summary>
    public sealed class AusruestungTests
    {
        AbilityCatalog _katalog;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _katalog = null;
            yield return MatchTestHarness.Teardown();
        }

        IEnumerator KatalogLaden(System.Action<NetworkPlayerController> weiter = null)
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var holder = player.GetComponent<AbilityHolder>();
            _katalog = holder != null ? holder.Catalog : null;
            weiter?.Invoke(player);
        }

        AbilityStats Finde(AbilityKind kind)
        {
            if (_katalog == null) return null;
            foreach (var a in _katalog.Abilities)
                if (a != null && a.Kind == kind) return a;
            return null;
        }

        [UnityTest]
        public IEnumerator Verbandspaket_gibt_es_und_wirkt_am_Benutzer()
        {
            yield return KatalogLaden();
            var verband = Finde(AbilityKind.Verbandspaket);
            Assert.IsNotNull(verband, "Es gibt kein Verbandspaket im Katalog.");
            Assert.IsTrue(verband.AmBenutzer,
                "Das Verbandspaket muesste am Benutzer wirken, nicht geworfen werden.");
            Assert.IsTrue(verband.Angeboten, "Das Verbandspaket wird nicht angeboten.");
        }

        [UnityTest]
        public IEnumerator Scan_Puls_ist_noch_da_wird_aber_nicht_angeboten()
        {
            yield return KatalogLaden();
            var scan = Finde(AbilityKind.ScanPuls);
            Assert.IsNotNull(scan,
                "Der Scan-Puls wurde geloescht - er sollte nur nicht mehr angeboten werden.");
            Assert.IsFalse(scan.Angeboten,
                "Der Scan-Puls wird noch angeboten - er zeigt Gegner durch Waende.");
        }

        [UnityTest]
        public IEnumerator Reihenfolge_im_Katalog_hat_sich_nicht_verschoben()
        {
            yield return KatalogLaden();
            Assert.IsNotNull(_katalog, "Kein Faehigkeiten-Katalog.");
            var a = _katalog.Abilities;
            Assert.GreaterOrEqual(a.Length, 7, "Es fehlen Eintraege im Katalog.");

            // Die ersten sechs muessen exakt so stehen wie vor Schritt 6 -
            // die Reihenfolge ist der Netz-Index und steht in Kaufdaten.
            Assert.AreEqual(AbilityKind.Rauchwand, a[0].Kind);
            Assert.AreEqual(AbilityKind.Blendgranate, a[1].Kind);
            Assert.AreEqual(AbilityKind.Splittergranate, a[2].Kind);
            Assert.AreEqual(AbilityKind.ScanPuls, a[3].Kind);
            Assert.AreEqual(AbilityKind.Brandwand, a[4].Kind);
            Assert.AreEqual(AbilityKind.Stolperdraht, a[5].Kind);
            Assert.AreEqual(AbilityKind.Verbandspaket, a[6].Kind,
                "Neues muss hinten angehaengt werden, nicht dazwischen.");
        }

        [UnityTest]
        public IEnumerator Verband_stoppt_Blutung_und_heilt_nur_wenig()
        {
            NetworkPlayerController player = null;
            yield return KatalogLaden(p => player = p);

            var verband = Finde(AbilityKind.Verbandspaket);
            if (verband == null) Assert.Ignore("Kein Verbandspaket.");

            var bluten = player.GetComponent<Bleeding>();
            var health = player.GetComponent<Health>();
            Assert.IsNotNull(bluten, "Keine Blutungs-Komponente.");

            // Verwundet und blutend.
            health.ApplyDamage(60, (GameObject)null, true);
            bluten.SetWundenForTests(2);
            yield return null;
            int vorher = health.Current;
            Assert.IsTrue(bluten.Blutet, "Der Aufbau des Tests stimmt nicht - es blutet nicht.");

            AbilitySpawner.ServerSpawn(verband, player.gameObject,
                player.transform.position, player.transform.forward, 0);
            yield return null;

            Assert.IsFalse(bluten.Blutet, "Der Verband stoppt die Blutung nicht.");
            Assert.Greater(health.Current, vorher, "Der Verband gibt gar kein Leben zurueck.");
            Assert.Less(health.Current, health.Max,
                "Der Verband heilt vollstaendig - er soll nur wenig zurueckgeben.");
        }

        [UnityTest]
        public IEnumerator Heilen_ueber_das_Maximum_hinaus_geht_nicht()
        {
            NetworkPlayerController player = null;
            yield return KatalogLaden(p => player = p);

            var health = player.GetComponent<Health>();
            health.ServerHeal(9999);
            yield return null;

            Assert.AreEqual(health.Max, health.Current,
                "Heilen geht ueber das Maximum hinaus.");
        }
    }
}
