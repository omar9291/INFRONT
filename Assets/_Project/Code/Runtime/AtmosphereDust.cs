using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Treibender Staub in der Arena - dieselbe Idee wie <see cref="MenuDust"/>
    /// im Menü, aber Dichte und Farbton folgen dem <see cref="WeatherDirector"/>.
    /// Mehrere Instanzen an verschiedenen Stellen der Karte (SceneBuilder),
    /// besonders in den Lichtschächten, wo der Staub aufblitzt.
    ///
    /// Baut sein Partikelsystem beim Start selbst. Rein optisch. Bei
    /// "Bild: Schlicht" fährt der WeatherDirector die Dichte auf 0.
    ///
    /// NICHT prüfbar: wie es aussieht. Prüfbar: dass es läuft und die
    /// Zieldichte ankommt (<see cref="Density01ForTests"/>).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class AtmosphereDust : MonoBehaviour
    {
        [SerializeField] Vector3 _boxSize = new Vector3(30f, 10f, 30f);

        ParticleSystem _ps;
        ParticleSystem.EmissionModule _emission;
        ParticleSystem.MainModule _main;

        float _cur;
        float _target = 0.4f;
        Color _tint = new Color(0.78f, 0.78f, 0.82f);

        public float Density01ForTests => _cur;
        public bool RunningForTests => _ps != null && _ps.isPlaying;

        void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _main = _ps.main;
            _main.loop = true;
            _main.startLifetime = 14f;
            _main.startSpeed = 0.12f;
            _main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            _main.startColor = new Color(_tint.r, _tint.g, _tint.b, 0.5f);
            _main.maxParticles = 600;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.gravityModifier = 0f;
            _main.prewarm = true;

            _emission = _ps.emission;
            _emission.enabled = true;
            _emission.rateOverTime = 0f;

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = _boxSize;

            var vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.09f, 0.09f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f),
                    new GradientAlphaKey(0.8f, 0.8f), new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var r = GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "AtmosphereDustMat" };
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);   // additiv - fängt Licht ein
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            r.material = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = -1;

            _ps.Play();
        }

        /// <summary>Zieldichte 0..1 und Farbton vom <see cref="WeatherDirector"/>.</summary>
        public void SetWeather(float density01, Color tint)
        {
            _target = Mathf.Clamp01(density01);
            _tint = tint;
        }

        void Update()
        {
            // Rückfallebene: bei "Bild: Schlicht" immer aus, auch ohne WeatherDirector.
            float target = GameSettings.GraphicsQuality == GameSettings.Graphics.Voll ? _target : 0f;
            _cur = Mathf.MoveTowards(_cur, target, Time.deltaTime / 2f);
            _emission.rateOverTime = _cur * 34f;
            _main.startColor = new Color(_tint.r, _tint.g, _tint.b, 0.5f);
        }
    }
}
