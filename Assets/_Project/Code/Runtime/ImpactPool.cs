using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Einschlag-Optik für ALLE Schüsse zusammen (hängt sich einmal an
    /// <see cref="NetworkWeapon.AnyShotFx"/>):
    ///  - Wand: kurzer Funkenstrahl + ein Einschussloch, das bleibt
    ///  - Körper: kurzer roter Stoß am Trefferpunkt (kein bleibendes Loch)
    ///
    /// Alles per Code, feste Pools. Die ältesten Löcher werden recycelt.
    /// Sitzt auf dem HUD-Objekt der Arena.
    /// </summary>
    public sealed class ImpactPool : MonoBehaviour
    {
        public static ImpactPool Instance { get; private set; }

        const int HoleCount = 40;
        const int SparkCount = 14;
        const int PuffCount = 10;

        Transform[] _holes;
        int _holeNext;

        LineRenderer[] _sparks;
        float[] _sparkTimer;
        int _sparkNext;

        Transform[] _puffs;
        float[] _puffTimer;
        int _puffNext;

        Material _holeMat, _sparkMat, _puffMat;

        public int ActiveHolesForTests
        {
            get
            {
                int n = 0;
                if (_holes != null)
                    foreach (var h in _holes)
                        if (h != null && h.gameObject.activeSelf) n++;
                return n;
            }
        }

        public static ImpactPool EnsureForTests()
        {
            if (Instance == null)
                Instance = new GameObject("ImpactPool (Test)").AddComponent<ImpactPool>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildPools();
        }

        void OnEnable() => NetworkWeapon.AnyShotFx += OnShot;
        void OnDisable() => NetworkWeapon.AnyShotFx -= OnShot;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_holeMat != null) Destroy(_holeMat);
            if (_sparkMat != null) Destroy(_sparkMat);
            if (_puffMat != null) Destroy(_puffMat);
        }

        void BuildPools()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");

            // Einschussloch: dunkler runder Fleck, normal alpha-gemischt.
            _holeMat = new Material(unlit) { name = "HoleMat" };
            var holeTex = RadialTexture(new Color(0.03f, 0.03f, 0.04f), 1f);
            if (_holeMat.HasProperty("_BaseMap")) _holeMat.SetTexture("_BaseMap", holeTex);
            if (_holeMat.HasProperty("_MainTex")) _holeMat.SetTexture("_MainTex", holeTex);
            SetTransparent(_holeMat);
            if (_holeMat.HasProperty("_BaseColor")) _holeMat.SetColor("_BaseColor", Color.white);

            _sparkMat = new Material(unlit) { name = "SparkMat" };
            MakeAdditive(_sparkMat, new Color(1f, 0.85f, 0.4f, 1f));

            _puffMat = new Material(unlit) { name = "PuffMat" };
            var puffTex = RadialTexture(new Color(1f, 0.25f, 0.2f), 1f);
            if (_puffMat.HasProperty("_BaseMap")) _puffMat.SetTexture("_BaseMap", puffTex);
            if (_puffMat.HasProperty("_MainTex")) _puffMat.SetTexture("_MainTex", puffTex);
            MakeAdditive(_puffMat, new Color(1f, 0.3f, 0.25f, 1f));

            _holes = new Transform[HoleCount];
            for (int i = 0; i < HoleCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Hole_{i}";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.14f;
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _holeMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _holes[i] = go.transform;
            }

            _sparks = new LineRenderer[SparkCount];
            _sparkTimer = new float[SparkCount];
            for (int i = 0; i < SparkCount; i++)
            {
                var go = new GameObject($"Spark_{i}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = _sparkMat;
                lr.widthMultiplier = 0.03f;
                lr.numCapVertices = 1;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _sparks[i] = lr;
                _sparkTimer[i] = -1f;
            }

            _puffs = new Transform[PuffCount];
            _puffTimer = new float[PuffCount];
            for (int i = 0; i < PuffCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Puff_{i}";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _puffMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _puffs[i] = go.transform;
                _puffTimer[i] = -1f;
            }
        }

        void OnShot(ShotFx fx)
        {
            if (fx.HitWall) { PlaceHole(fx); Sparks(fx); }
            else if (fx.HitBody) { Puff(fx); }
        }

        /// <summary>Nur für Tests: einen Einschlag von Hand auslösen.</summary>
        public void SpawnForTests(ShotFx fx) => OnShot(fx);

        /// <summary>Nur für Tests: alle Effekte abschalten.</summary>
        public void ClearForTests()
        {
            if (_holes != null) foreach (var h in _holes) if (h != null) h.gameObject.SetActive(false);
            if (_puffs != null) foreach (var p in _puffs) if (p != null) p.gameObject.SetActive(false);
            if (_sparks != null) foreach (var s in _sparks) if (s != null) s.enabled = false;
            if (_puffTimer != null) for (int i = 0; i < _puffTimer.Length; i++) _puffTimer[i] = -1f;
            if (_sparkTimer != null) for (int i = 0; i < _sparkTimer.Length; i++) _sparkTimer[i] = -1f;
        }

        void PlaceHole(ShotFx fx)
        {
            var t = _holes[_holeNext];
            _holeNext = (_holeNext + 1) % HoleCount;
            t.position = fx.End + fx.Normal * 0.02f;
            t.rotation = Quaternion.LookRotation(-fx.Normal);
            t.gameObject.SetActive(true);
        }

        void Sparks(ShotFx fx)
        {
            int shots = Random.Range(2, 4);
            for (int s = 0; s < shots; s++)
            {
                int idx = _sparkNext;
                _sparkNext = (_sparkNext + 1) % SparkCount;

                var lr = _sparks[idx];
                Vector3 dir = (fx.Normal + Random.insideUnitSphere * 0.9f).normalized;
                float len = Random.Range(0.15f, 0.4f);
                lr.SetPosition(0, fx.End);
                lr.SetPosition(1, fx.End + dir * len);
                lr.enabled = true;
                _sparkTimer[idx] = 0.12f;
            }
        }

        void Puff(ShotFx fx)
        {
            var t = _puffs[_puffNext];
            int idx = _puffNext;
            _puffNext = (_puffNext + 1) % PuffCount;
            t.position = fx.End + fx.Normal * 0.05f;
            t.localScale = Vector3.one * 0.12f;
            t.gameObject.SetActive(true);
            _puffTimer[idx] = 0.14f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < SparkCount; i++)
            {
                if (_sparkTimer[i] < 0f) continue;
                _sparkTimer[i] -= dt;
                if (_sparkTimer[i] <= 0f) { _sparks[i].enabled = false; _sparkTimer[i] = -1f; }
            }

            var cam = Camera.main;
            for (int i = 0; i < PuffCount; i++)
            {
                if (_puffTimer[i] < 0f) continue;
                _puffTimer[i] -= dt;
                float k = 1f - Mathf.Clamp01(_puffTimer[i] / 0.14f);
                if (_puffTimer[i] <= 0f) { _puffs[i].gameObject.SetActive(false); _puffTimer[i] = -1f; continue; }
                _puffs[i].localScale = Vector3.one * Mathf.Lerp(0.12f, 0.4f, k);
                if (cam != null)
                    _puffs[i].rotation = Quaternion.LookRotation(_puffs[i].position - cam.transform.position);
            }
        }

        // ---- Material-Helfer ----

        static Texture2D RadialTexture(Color color, float edgeAlpha)
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            Vector2 c = new(n / 2f, n / 2f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (n / 2f);
                float a = Mathf.Clamp01(1f - d) * edgeAlpha;
                px[y * n + x] = new Color(color.r, color.g, color.b, a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void SetTransparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        static void MakeAdditive(Material m, Color c)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }
}
