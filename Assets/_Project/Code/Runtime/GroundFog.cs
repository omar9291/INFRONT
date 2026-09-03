using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Flache Nebelbank knapp über dem Boden. Anders als der Distanz-Nebel
    /// (<see cref="RenderSettings.fog"/>) verdeckt sie keinen stehenden Gegner -
    /// sie liegt unter Hüfthöhe und gibt der Karte trotzdem eine schwere,
    /// diesige Stimmung. Stärke und Farbe steuert der <see cref="WeatherDirector"/>.
    ///
    /// Baut sein Partikelsystem beim Start selbst (nichts im Editor
    /// serialisiert). Rein optisch, keine Spiel-Logik, keine Netzwerk-Daten.
    /// Bei "Bild: Schlicht" fährt der WeatherDirector die Stärke auf 0.
    ///
    /// NICHT prüfbar: wie es aussieht. Prüfbar: dass es läuft und dass die
    /// Zielstärke ankommt (<see cref="Intensity01ForTests"/>).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class GroundFog : MonoBehaviour
    {
        const float MaxAlpha = 0.16f;   // Deckkraft bei voller Nebelbank
        const float MaxRate = 26f;      // Partikel/s bei voller Stärke

        ParticleSystem _ps;
        ParticleSystem.EmissionModule _emission;
        ParticleSystem.MainModule _main;

        float _cur;                 // 0..1, folgt weich dem Ziel
        float _target;
        Color _tint = new Color(0.72f, 0.75f, 0.78f);

        public float Intensity01ForTests => _cur;
        public bool RunningForTests => _ps != null && _ps.isPlaying;

        void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _main = _ps.main;
            _main.loop = true;
            _main.startLifetime = 22f;
            _main.startSpeed = 0.06f;
            _main.startSize = new ParticleSystem.MinMaxCurve(5f, 11f);
            _main.startColor = WithAlpha(0f);
            _main.maxParticles = 700;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.gravityModifier = 0f;
            _main.prewarm = true;
            _main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);

            _emission = _ps.emission;
            _emission.enabled = true;
            _emission.rateOverTime = 0f;

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(96f, 0.8f, 96f);
            shape.position = new Vector3(0f, 0.35f, 0f);

            // Sehr flach halten: kaum Auftrieb, damit die Bank unten bleibt.
            var vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            vel.y = new ParticleSystem.MinMaxCurve(0.0f, 0.03f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

            // Weich ein- und ausblenden, damit nichts hart erscheint.
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var renderer = GetComponent<ParticleSystemRenderer>();
            // Denselben Shader wie MenuDust - der ist erwiesenermaßen im Build.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "GroundFogMat" };
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);       // Alpha (NICHT additiv - Nebel dunkelt eher ab)
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -2;

            _ps.Play();
        }

        /// <summary>Zielstärke 0..1 und Farbton. Der Übergang läuft weich (~2 s).</summary>
        public void SetTarget(float intensity01, Color tint)
        {
            _target = Mathf.Clamp01(intensity01);
            _tint = tint;
        }

        void Update()
        {
            // Rückfallebene: bei "Bild: Schlicht" immer aus, auch ohne WeatherDirector.
            float target = GameSettings.GraphicsQuality == GameSettings.Graphics.Voll ? _target : 0f;
            _cur = Mathf.MoveTowards(_cur, target, Time.deltaTime / 2f);
            _emission.rateOverTime = _cur * MaxRate;
            _main.startColor = WithAlpha(_cur * MaxAlpha);
        }

        Color WithAlpha(float a) => new Color(_tint.r, _tint.g, _tint.b, a);
    }
}
