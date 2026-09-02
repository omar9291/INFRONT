using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Feiner, langsam treibender Staub in der Menü-Kulisse - Partikel, die im
    /// warmen Licht aufblitzen und der Szene Tiefe geben. Baut sein
    /// Partikelsystem beim Start selbst, damit nichts im Editor serialisiert
    /// werden muss. Rein optisch.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class MenuDust : MonoBehaviour
    {
        [SerializeField] Vector3 _boxSize = new Vector3(26f, 9f, 22f);
        [SerializeField] int _count = 190;
        [SerializeField] Color _tint = new Color(1f, 0.86f, 0.66f, 0.5f);

        void Awake()
        {
            var ps = GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 16f;
            main.startSpeed = 0.10f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = _tint;
            main.maxParticles = _count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.prewarm = true;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = _count / 16f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = _boxSize;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(0.015f, 0.07f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            // Sanft ein- und wieder ausblenden, damit nichts hart erscheint.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var renderer = GetComponent<ParticleSystemRenderer>();
            // Denselben Shader wie MuzzleFlash nehmen - der ist erwiesenermaßen
            // im Build enthalten. Particles/Unlit kann wegoptimiert werden.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "MenuDustMat" };
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);       // additiv
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", _tint);
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -1;

            ps.Play();
        }
    }
}
