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
    /// Die Einstellung "Bild: Schlicht" (<see cref="GameSettings.Graphics"/>)
    /// schaltet Volume UND Nebel komplett ab. Das ist die Rueckfallebene: sollte
    /// die volle Optik auf einem Rechner Streifen oder Ruckeln machen, ist man
    /// mit einem Klick wieder beim schlichten Bild.
    ///
    /// Haengt im Menue und in der Arena (SceneBuilder).
    /// NICHT pruefbar: wie es aussieht.
    /// </summary>
    public sealed class PostFxController : MonoBehaviour
    {
        static readonly Color FogColor = new Color(0.05f, 0.06f, 0.08f);
        const float FogDensity = 0.010f;

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

            var bloom = _profile.Add<Bloom>(true);
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.82f));

            var vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(0.30f);
            vignette.smoothness.Override(0.42f);
            vignette.color.Override(new Color(0.02f, 0.02f, 0.03f));

            var color = _profile.Add<ColorAdjustments>(true);
            color.contrast.Override(12f);
            color.saturation.Override(4f);
            color.colorFilter.Override(new Color(1f, 0.96f, 0.90f));
            color.postExposure.Override(0.08f);

            var white = _profile.Add<WhiteBalance>(true);
            white.temperature.Override(8f);   // leicht waermer
            white.tint.Override(-3f);

            var grain = _profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.8f);
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
