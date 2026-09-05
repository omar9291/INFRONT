using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Infront
{
    /// <summary>
    /// Bild-Aufwertung ("Der Look"). Baut zur Laufzeit ein globales
    /// Post-Processing-Volume - ganz ohne Volume-Profil-Asset, alles per Code:
    ///
    ///  - ACES-Tonemapping (satte, filmische Farben statt ausgewaschen)
    ///  - Bloom (helle Dinge strahlen: Muendungsfeuer, Bombenlicht, Akzente)
    ///  - Vignette (dunkle Bildraender)
    ///  - Farbanpassung: leicht mehr Kontrast, warmer Filter -> Dark-Tactical
    ///  - Filmkorn
    ///  - Nebel in der Ferne (Tiefe, macht die Karte lesbar)
    ///
    /// Ist <see cref="_menuLook"/> gesetzt (nur in der Menue-Szene, vom
    /// SceneBuilder), kommt der Kino-Look obendrauf: die 3D-Kulisse verschwimmt
    /// per Tiefenunschaerfe, die Bildraender werden dunkler und die wenigen
    /// Lichtquellen strahlen staerker. So steht die Oberflaeche klar davor,
    /// wie das Hintergrundbild bei CS2 oder Valorant.
    ///
    /// Die Einstellung "Bild: Schlicht" (<see cref="GameSettings.Graphics"/>)
    /// schaltet Volume UND Nebel komplett ab. Das ist die Rueckfallebene: sollte
    /// die volle Optik auf einem Rechner Streifen oder Ruckeln machen, ist man
    /// mit einem Klick wieder beim schlichten Bild (dann auch ohne Unschaerfe).
    ///
    /// Haengt im Menue und in der Arena (SceneBuilder).
    /// NICHT pruefbar: wie es aussieht.
    /// </summary>
    public sealed class PostFxController : MonoBehaviour
    {
        // Rundgang 2026-09-04: dieser Wert ist praktisch schwarz. Entfernung
        // hat dadurch abgedunkelt statt zu dunsten - genau falsch herum. Der
        // WeatherDirector setzt im Spiel eigene Werte (0,55 aufwaerts), diese
        // Farbe greift nur, wenn kein Wetter laeuft. Sie liegt jetzt in
        // derselben Groessenordnung.
        static readonly Color FogColor = new Color(0.54f, 0.57f, 0.62f);
        const float FogDensity = 0.010f;

        [Tooltip("Nur in der Menue-Szene: Tiefenunschaerfe + staerkere Bildraender (Kino-Look).")]
        [SerializeField] bool _menuLook;

        Volume _volume;
        VolumeProfile _profile;
        GameSettings.Graphics _applied = (GameSettings.Graphics)(-1);

        // ---- Test-Haken (Optik selbst ist nicht pruefbar) ----
        public bool HasProfileForTests => _profile != null && _profile.components.Count >= 5;
        public bool VolumeActiveForTests => _volume != null && _volume.enabled && _volume.weight > 0.5f;
        public bool FullQualityForTests => GameSettings.GraphicsQuality == GameSettings.Graphics.Voll;

        void Awake()
        {
            BuildProfile();

            var go = new GameObject("InfrontPostFxVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.sharedProfile = _profile;

            Apply(force: true);
        }

        void OnEnable() => Apply(force: true);

        void Update()
        {
            // Wechselt der Nutzer die Einstellung im Menue, schlaegt es hier durch.
            if (_applied != GameSettings.GraphicsQuality)
                Apply(force: false);
        }

        void OnDestroy()
        {
            if (_volume != null) Destroy(_volume.gameObject);
            if (_profile != null) Destroy(_profile);
            // Nebel nicht in anderen Szenen haengen lassen.
            RenderSettings.fog = false;
        }

        void BuildProfile()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "InfrontPostFx (Laufzeit)";

            var tone = _profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            // P6: das Menue soll ernster wirken - weniger Leuchten, gedeckter,
            // ruhiger. Das Spiel selbst bleibt wie es ist.
            var bloom = _profile.Add<Bloom>(true);
            // Screenshot-Test 2026-09-04: 0.9 liess Leuchtstreifen und
            // Bombenplatz-Markierungen zu Weiss ausbrennen. Deutlich runter.
            bloom.intensity.Override(_menuLook ? 0.6f : 0.45f);
            bloom.threshold.Override(_menuLook ? 1.2f : 1.15f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.82f));

            var vignette = _profile.Add<Vignette>(true);
            // Im Spiel kostet eine starke Vignette Sicht in den Ecken - dort
            // steht der Gegner. Im Menue ist sie Gestaltung und bleibt.
            vignette.intensity.Override(_menuLook ? 0.46f : 0.20f);
            vignette.smoothness.Override(_menuLook ? 0.5f : 0.42f);
            vignette.color.Override(new Color(0.02f, 0.02f, 0.03f));

            var color = _profile.Add<ColorAdjustments>(true);
            // ACES bringt bereits kraeftigen eigenen Kontrast mit. Die +12
            // obendrauf haben im Spiel die Schattenseiten zugedrueckt: gemessen
            // waren 27 % jedes Bildes praktisch schwarz. Im Menue darf es
            // haerter bleiben, da steht die Oberflaeche im Vordergrund.
            color.contrast.Override(_menuLook ? 16f : 5f);
            color.saturation.Override(_menuLook ? -10f : 4f);   // Menue entsaettigt = ernster
            // Der warme Filter im Spiel wird halbiert (0,96/0,90 -> 0,98/0,95).
            // Er stammt aus einer Zeit, in der die Halle selbst kuehl-grau war
            // und Waerme von aussen dazukam. Seit der Boden am gebackenen Licht
            // teilnimmt, bringen die Lampen ihre Waerme selbst mit - gemessen
            // ueber alle 27 Rundgangbilder stieg der Rot-Blau-Abstand von +2,7
            // auf +20,1. Ein zweites Mal Waerme obendrauf macht daraus Sepia.
            color.colorFilter.Override(_menuLook ? new Color(0.92f, 0.93f, 0.96f) : new Color(1f, 0.98f, 0.95f));
            color.postExposure.Override(_menuLook ? -0.04f : 0f);   // im Spiel keine Extra-Belichtung (Screenshot-Test 2)

            var white = _profile.Add<WhiteBalance>(true);
            // Im Menue darf es warm bleiben, das ist Gestaltung. Im Spiel
            // reicht die Haelfte - siehe die Begruendung beim Farbfilter.
            white.temperature.Override(_menuLook ? 8f : 4f);
            white.tint.Override(-3f);

            var grain = _profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.8f);

            if (_menuLook)
            {
                // Kino-Look: die 3D-Kulisse weich verschwimmen lassen, damit die
                // Menue-Oberflaeche klar und ruhig davor steht. Gauss ist guenstig
                // und laeuft ueberall - Bokeh waere schoener, aber teurer.
                var dof = _profile.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(5f);
                dof.gaussianEnd.Override(17f);
                dof.gaussianMaxRadius.Override(1.3f);
                dof.highQualitySampling.Override(true);
            }
        }

        void Apply(bool force)
        {
            var q = GameSettings.GraphicsQuality;
            if (!force && q == _applied) return;
            _applied = q;

            bool full = q == GameSettings.Graphics.Voll;

            if (_volume != null)
            {
                _volume.enabled = full;
                _volume.weight = full ? 1f : 0f;
            }

            RenderSettings.fog = full;
            if (full)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = FogColor;
                RenderSettings.fogDensity = FogDensity;
            }
        }
    }
}
