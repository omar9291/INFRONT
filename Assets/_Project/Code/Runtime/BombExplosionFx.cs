using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Optik der Bomben-Explosion. Sitzt am Bomben-Prefab und wird per RPC
    /// (<see cref="Bomb"/>) auf allen Clients ausgeloest - der Server rechnet
    /// den Schaden, hier passiert nur Show.
    ///
    /// Alles per Code, keine Asset-Dateien:
    ///  - eine wachsende, verblassende Feuerkugel (additiver Unlit-Shader),
    ///  - ein kurzer oranger Lichtblitz,
    ///  - ein Vollbild-Aufblitzen (OnGUI), Staerke nach Entfernung zur Kamera,
    ///  - kurzes Kamera-Wackeln ueber <see cref="FirstPersonCamera.Shake"/>.
    /// </summary>
    public sealed class BombExplosionFx : MonoBehaviour
    {
        const float Duration = 0.9f;     // Lebensdauer der Feuerkugel
        const float MaxRadius = 7f;      // Endgroesse der Kugel
        const float FlashRange = 45f;    // ab hier kein Bildschirm-Blitz mehr

        GameObject _sphere;
        Renderer _sphereRenderer;
        Material _material;
        Light _flash;

        Vector3 _center;
        float _t = -1f;          // < 0 = inaktiv
        float _screenFlash;      // 0..1, klingt ab
        Texture2D _flashTex;

        // "Kurz taub" nach einer nahen Explosion: Tiefpass auf dem Ohr + Klingeln.
        AudioLowPassFilter _earFilter;
        float _deafen;           // 1 -> 0

        void Awake()
        {
            // Feuerkugel
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "ExplosionSphere";
            var col = _sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _sphere.transform.SetParent(transform, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _material = new Material(shader) { name = "ExplosionMat" };
            SetupAdditive(_material);

            _sphereRenderer = _sphere.GetComponent<Renderer>();
            _sphereRenderer.sharedMaterial = _material;
            _sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sphereRenderer.receiveShadows = false;
            _sphere.SetActive(false);

            // Lichtblitz
            var lightGo = new GameObject("ExplosionLight");
            lightGo.transform.SetParent(transform, false);
            _flash = lightGo.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.color = new Color(1f, 0.6f, 0.25f);
            _flash.range = 22f;
            _flash.intensity = 0f;
            _flash.shadows = LightShadows.None;
            lightGo.SetActive(true);

            _flashTex = Texture2D.whiteTexture;
        }

        static void SetupAdditive(Material m)
        {
            // URP/Unlit zur Laufzeit auf additiven, tiefenschreibfreien Blend stellen.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 1f);       // Additive
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            SetColor(m, new Color(1f, 0.55f, 0.15f, 1f));
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_sphere != null) Destroy(_sphere);
            // Filter nicht mit rausnehmen (koennte woanders gebraucht werden) -
            // aber sicher abschalten, damit nach Szenenwechsel nichts dumpf bleibt.
            if (_earFilter != null) { _earFilter.cutoffFrequency = 22000f; _earFilter.enabled = false; }
        }

        /// <summary>Explosion an dieser Weltposition zeigen (auf allen Clients).</summary>
        public void Play(Vector3 center)
        {
            _center = center;
            _t = 0f;

            AudioService.Instance?.PlayAt(SoundId.BombeExplosion, center, 1f);

            if (_sphere != null)
            {
                _sphere.transform.position = center;
                _sphere.transform.localScale = Vector3.one * 0.4f;
                _sphere.SetActive(true);
            }
            if (_flash != null)
            {
                _flash.transform.position = center;
                _flash.intensity = 12f;
            }

            // Bildschirm-Blitz und Kamera-Wackeln nach Naehe zur Kamera.
            var cam = Camera.main;
            if (cam != null)
            {
                float dist = Vector3.Distance(center, cam.transform.position);
                float near = Mathf.Clamp01(1f - dist / FlashRange);
                _screenFlash = near * 0.7f;

                var fpc = cam.GetComponent<FirstPersonCamera>();
                if (fpc != null && near > 0.01f)
                    fpc.Shake(Mathf.Lerp(0.15f, 1.1f, near), 0.5f);

                // War die Explosion nah? Kurz "taub": alles dumpf + Ohren-Klingeln.
                if (near > 0.12f)
                {
                    AudioService.Instance?.Play2D(SoundId.OhrenPfeifen, near * 0.8f);

                    // Schritt 7: gibt es das allgemeine Ohrenklingeln, macht
                    // das die Daempfung - sonst wuerden sich zwei Tiefpaesse
                    // auf derselben Kamera gegenseitig ueberschreiben.
                    // Ohne EarRinging bleibt der alte Weg als Rueckfall.
                    if (EarRinging.Vorhanden)
                    {
                        // WICHTIG: center, nicht transform.position. Das
                        // FX-Objekt selbst bleibt im Ursprung stehen - mit
                        // transform.position lag die Explosion immer bei (0,0,0)
                        // und damit fast nie in Ohrenreichweite.
                        EarRinging.ExplosionAt(center);
                    }
                    else
                    {
                        _deafen = Mathf.Max(_deafen, near);
                        EnsureEarFilter(cam);
                    }
                }
            }
        }

        void EnsureEarFilter(Camera cam)
        {
            if (_earFilter != null || cam == null) return;
            _earFilter = cam.GetComponent<AudioLowPassFilter>();
            if (_earFilter == null) _earFilter = cam.gameObject.AddComponent<AudioLowPassFilter>();
            _earFilter.lowpassResonanceQ = 1f;
        }

        void Update()
        {
            if (_screenFlash > 0f)
                _screenFlash = Mathf.Max(0f, _screenFlash - Time.deltaTime * 2.2f);

            // Taubheit klingt ueber ~3 s ab.
            if (_deafen > 0f)
            {
                _deafen = Mathf.Max(0f, _deafen - Time.deltaTime * 0.32f);
                if (_earFilter != null)
                {
                    float muffle = Mathf.SmoothStep(0f, 1f, _deafen);
                    _earFilter.enabled = _deafen > 0.01f;
                    _earFilter.cutoffFrequency = Mathf.Lerp(22000f, 480f, muffle);
                }
            }

            if (_t < 0f) return;

            _t += Time.deltaTime;
            float k = _t / Duration;   // 0..1

            if (k >= 1f)
            {
                _t = -1f;
                if (_sphere != null) _sphere.SetActive(false);
                if (_flash != null) _flash.intensity = 0f;
                return;
            }

            // Kugel: schnell aufblaehen, dann verblassen.
            float radius = Mathf.Lerp(0.4f, MaxRadius, Mathf.Sqrt(k));
            if (_sphere != null)
            {
                _sphere.transform.position = _center;
                _sphere.transform.localScale = Vector3.one * radius;
            }

            float alpha = 1f - k;
            SetColor(_material, new Color(1f, Mathf.Lerp(0.55f, 0.2f, k), 0.12f, alpha));

            if (_flash != null)
                _flash.intensity = Mathf.Lerp(12f, 0f, k);
        }

        void OnGUI()
        {
            if (_screenFlash <= 0f) return;
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.6f, _screenFlash);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flashTex);
            GUI.color = prev;
        }
    }
}
