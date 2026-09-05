using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Der Startbildschirm. Laeuft genau einmal beim Programmstart, noch bevor
    /// das Menue sichtbar wird.
    ///
    /// Warum es das gibt: der <see cref="LoadingOverlay"/> wurde bisher nur
    /// vom <see cref="GameFlow"/> beim Szenenwechsel benutzt. Die Menue-Szene
    /// ist aber die Startszene - beim Programmstart wechselt nichts, also war
    /// beim Start nie ein Ladebildschirm zu sehen. Genau das hat gefehlt.
    ///
    /// Der Balken zeigt hier **echte** Arbeit, keine erfundene Wartezeit:
    /// Profil, Einstellungen, Laufbahn, das Vorwaermen der Toene und der
    /// Aufbau des Menues. Nur die Mindestanzeige am Ende ist geschenkt - und
    /// die auch nur, damit man den Bildschirm ueberhaupt lesen kann.
    /// </summary>
    public sealed class BootFlow : MonoBehaviour
    {
        /// <summary>
        /// Wie lange der Startbildschirm mindestens steht. Auf einer schnellen
        /// Maschine ist die echte Arbeit in Sekundenbruchteilen fertig - ohne
        /// Mindestzeit blitzt der Bildschirm nur auf, und niemand liest den
        /// Tipp. Als Feld, damit man daran drehen kann.
        /// </summary>
        [SerializeField] float _mindestDauer = 3.2f;

        /// <summary>Notbremse: haengt eine Phase, geht es trotzdem weiter.</summary>
        [SerializeField] float _hoechstDauer = 20f;

        public static BootFlow Instance { get; private set; }

        /// <summary>Laeuft der Startbildschirm gerade?</summary>
        public static bool Running { get; private set; }

        /// <summary>Ist der Start durch? Vor dem ersten Frame schon true, wenn uebersprungen.</summary>
        public static bool Done { get; private set; }

        static readonly List<Action> _wartende = new List<Action>();

        /// <summary>
        /// Etwas erst nach dem Startbildschirm tun. Ist der Start schon durch,
        /// laeuft es sofort. So steht der Erstlauf-Ablauf nicht mitten im
        /// Ladebildschirm.
        /// </summary>
        public static void WhenDone(Action a)
        {
            if (a == null) return;
            if (Done) { a(); return; }
            _wartende.Add(a);
        }

        /// <summary>Nur fuer Tests: Zustand zuruecksetzen.</summary>
        public static void ResetForTests()
        {
            Running = false;
            Done = false;
            ForceRunForTests = false;
            _wartende.Clear();

            // Beim Programmstart legt der GameFlow schon einen BootFlow an.
            // Der wird hier nur stillgelegt, nicht zerstoert - sonst faende
            // der Verdrahtungstest ihn spaeter nicht mehr.
            if (Instance != null)
            {
                Instance.StopAllCoroutines();
                Instance.enabled = false;
                Instance = null;
            }
        }

        /// <summary>Nur fuer Tests: so tun, als waere der Start durch.</summary>
        public static void MarkDoneForTests() => Fertig();

        public string PhaseForTests { get; private set; } = "";

        /// <summary>
        /// Nur fuer Tests: den Ablauf auch im Testlauf wirklich durchlaufen
        /// lassen. Ohne das wuerde der Startbildschirm im Batchmode immer
        /// uebersprungen - und damit nie geprueft.
        /// </summary>
        public static bool ForceRunForTests;

        /// <summary>Nur fuer Tests: Mindest- und Hoechstdauer kuerzen.</summary>
        public void SetDauerForTests(float mindest, float hoechst)
        {
            _mindestDauer = mindest;
            _hoechstDauer = hoechst;
        }

        // ------------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit()
        {
            // Spielzeit dieser Sitzung festhalten, sonst geht sie verloren.
            Spielstatistik.SitzungSichern();
            Absturzbericht.Beenden();
        }

        void Start()
        {
            // So frueh wie moeglich, und auch wenn der Startbildschirm
            // uebersprungen wird: ein Absturz beim Laden ist genau der Fall,
            // fuer den es den Bericht gibt.
            Absturzbericht.Starten();
            Spielstatistik.StartGezaehlt();

            if (Ueberspringen())
            {
                Fertig();
                return;
            }
            StartCoroutine(Ablauf());
        }

        /// <summary>
        /// Der Startbildschirm faellt weg im Testlauf, beim automatischen
        /// Fotografieren und wenn ausdruecklich -skipintro uebergeben wurde.
        /// Sonst wartet jeder Testlauf drei Sekunden pro Start.
        /// </summary>
        static bool Ueberspringen()
        {
            if (ForceRunForTests) return false;
            if (Application.isBatchMode) return true;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-skipintro" || a == "-autoshot" || a == "-survey" || a == "-runTests") return true;
            }
            return false;
        }

        static void Fertig()
        {
            Running = false;
            Done = true;
            // Kopie laufen lassen: ein Wartender darf selbst wieder WhenDone rufen.
            var kopie = _wartende.ToArray();
            _wartende.Clear();
            for (int i = 0; i < kopie.Length; i++)
            {
                try { kopie[i]?.Invoke(); }
                catch (Exception e) { Debug.LogError("[Infront] Nach dem Start: " + e); }
            }
        }

        IEnumerator Ablauf()
        {
            Running = true;
            Done = false;

            var ov = LoadingOverlay.Instance;
            if (ov == null)
            {
                // Ohne Ladebildschirm nicht haengenbleiben - lieber direkt ins Menue.
                Fertig();
                yield break;
            }

            ov.Begin("INFRONT", GameText.Loading.Start);
            float begonnen = Time.unscaledTime;

            yield return Phase(ov, 0.12f, GameText.Loading.ReadingProfile, () => PlayerProfile.Load());
            yield return Phase(ov, 0.24f, GameText.Menu.Settings, () => GameSettings.Load());
            yield return Phase(ov, 0.34f, GameText.Menu.Career, () =>
            {
                var _ = CareerStats.Matches;
                Spielstatistik.Laden();
            });

            // Toene vorwaermen. Das ist die einzige Phase mit echtem Gewicht:
            // ohne sie ruckelt der erste Schuss, weil der Ton dann erst von der
            // Platte kommt. Ein paar Klaenge pro Frame, damit der Balken laeuft.
            yield return PhaseToene(ov, 0.34f, 0.72f);

            // Auf das Menue warten. MainMenuUi baut sich selbst ueber mehrere
            // Frames auf; vorher waere das Ausblenden ein Sprung ins Leere.
            ov.SetProgress(0.8f, GameText.Loading.BuildingMenu);
            PhaseForTests = GameText.Loading.BuildingMenu;
            float wartenBis = Time.unscaledTime + Mathf.Min(8f, _hoechstDauer);
            while (Time.unscaledTime < wartenBis)
            {
                var menu = FindAnyObjectByType<MainMenuUi>();
                if (menu != null && menu.IsBuiltForTests) break;
                yield return null;
            }

            ov.SetProgress(1f, GameText.Loading.Ready);
            PhaseForTests = GameText.Loading.Ready;

            float ende = begonnen + Mathf.Min(_mindestDauer, _hoechstDauer);
            while (Time.unscaledTime < ende) yield return null;

            yield return ov.PlayOutAndHide();
            Fertig();
        }

        IEnumerator Phase(LoadingOverlay ov, float p01, string name, Action arbeit)
        {
            ov.SetProgress(p01, name);
            PhaseForTests = name;
            yield return null;              // Balken einmal zeichnen lassen
            try { arbeit?.Invoke(); }
            catch (Exception e) { Debug.LogWarning("[Infront] Startphase '" + name + "': " + e.Message); }
            yield return null;
        }

        IEnumerator PhaseToene(LoadingOverlay ov, float von, float bis)
        {
            const string name = GameText.Loading.PreparingAudio;
            PhaseForTests = name;
            ov.SetProgress(von, name);

            var audio = AudioService.Instance;
            if (audio == null) { yield return null; yield break; }

            var werte = (SoundId[])Enum.GetValues(typeof(SoundId));
            for (int i = 0; i < werte.Length; i++)
            {
                try { audio.Resolve(werte[i]); }
                catch (Exception) { /* fehlender Ton darf den Start nicht stoppen */ }

                // Vier pro Frame: schnell genug, aber der Balken bewegt sich sichtbar.
                if ((i & 3) == 3)
                {
                    ov.SetProgress(Mathf.Lerp(von, bis, (i + 1f) / werte.Length), name);
                    yield return null;
                }
            }
            ov.SetProgress(bis, name);
            yield return null;
        }
    }
}
