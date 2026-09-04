using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Mündungsfeuer: bei jedem Schuss ein kurzer Lichtblitz am Lauf plus ein
    /// kleines helles Viereck, das zur Kamera schaut und sofort verblasst.
    /// Alles per Code, ein wiederverwendetes Licht / Viereck - kein Erzeugen
    /// und Zerstören pro Schuss.
    ///
    /// Hängt am Spieler- und am Bot-Prefab und lauscht auf
    /// <see cref="NetworkWeapon.FireVisual"/> (läuft auf jeder Instanz).
    /// </summary>
    [RequireComponent(typeof(NetworkWeapon))]
    public sealed class MuzzleFlash : MonoBehaviour
    {
        const float Duration = 0.055f;

        NetworkWeapon _weapon;
        Light _light;
        Transform _quad;
        Material _quadMat;
        float _t = -1f;

        void Awake()
        {
            _weapon = GetComponent<NetworkWeapon>();

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.82f, 0.5f);
            _light.range = 9f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;

            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "MuzzleQuad";
            var col = quadGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _quad = quadGo.transform;
            _quad.SetParent(transform, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _quadMat = UrpMaterial.NeuFx(additiv: true, "MuzzleMat");
            MakeAdditive(_quadMat, new Color(1f, 0.85f, 0.55f, 1f));
            var r = quadGo.GetComponent<Renderer>();
            r.sharedMaterial = _quadMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            quadGo.SetActive(false);
        }

        static void MakeAdditive(Material m, Color c)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            // Erst dieser Aufruf setzt das Schluesselwort, das die
            // Durchsichtigkeit im Shader wirklich einschaltet.
            UrpMaterial.Leuchtend(m);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        void OnEnable() => _weapon.FireVisual += OnFire;
        void OnDisable() => _weapon.FireVisual -= OnFire;

        void OnDestroy()
        {
            if (_quadMat != null) Destroy(_quadMat);
        }

        void OnFire(ShotFx fx)
        {
            _t = 0f;
            Vector3 dir = (fx.End - fx.Origin).sqrMagnitude > 0.001f
                ? (fx.End - fx.Origin).normalized
                : transform.forward;

            _light.transform.position = fx.Origin + dir * 0.2f;
            _light.intensity = 6f;

            _quad.position = fx.Origin + dir * 0.25f;
            _quad.localScale = Vector3.one * Random.Range(0.35f, 0.55f);
            _quad.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _quad.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            float k = _t / Duration;

            if (k >= 1f)
            {
                _t = -1f;
                _light.intensity = 0f;
                _quad.gameObject.SetActive(false);
                return;
            }

            _light.intensity = Mathf.Lerp(6f, 0f, k);

            // Viereck schaut zur Kamera und schrumpft weg.
            var cam = Camera.main;
            if (cam != null)
                _quad.rotation = Quaternion.LookRotation(_quad.position - cam.transform.position)
                                 * Quaternion.Euler(0f, 0f, _quad.eulerAngles.z);
            _quad.localScale = Vector3.one * Mathf.Lerp(0.5f, 0.05f, k);
        }
    }
}
