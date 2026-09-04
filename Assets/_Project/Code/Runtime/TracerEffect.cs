using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Die Leuchtspur eines Schusses.
    ///
    /// Bis 2026-09-04 wurde bei jedem Schuss die **ganze** Strecke vom Lauf bis
    /// zum Treffer als eine undurchsichtige gelbe Linie gezeichnet, 7 cm breit
    /// und bis zu 45 m lang. Auf den Rundgang-Bildern lagen dadurch gelbe
    /// Bretter quer durch die Halle - mitten durch Kisten und Waende hindurch,
    /// weil eine undurchsichtige Linie an jeder Stelle gleich hell ist. Genau
    /// das hat der Spieler als "da bugt was in der Mitte" gemeldet.
    ///
    /// So macht es ein Schuss wirklich: sichtbar ist nicht die Bahn, sondern
    /// ein kurzes helles Stueck, das die Bahn entlangfliegt. Deshalb jetzt:
    ///  - ein kurzes Segment (<see cref="_segment"/> Meter), das vom Lauf zum
    ///    Treffer wandert,
    ///  - additiv gemischt, also Licht statt Plastik - vor einer hellen Wand
    ///    verschwindet es fast, im Dunkeln leuchtet es,
    ///  - vorn breit, hinten duenn, und ueber die Flugzeit ausblendend.
    ///
    /// Wichtig gegen Ruckeln: feste kleine Anzahl wiederverwendeter
    /// LineRenderer statt pro Schuss ein neues GameObject.
    /// NICHT pruefbar: wie es aussieht.
    /// </summary>
    [RequireComponent(typeof(NetworkWeapon))]
    public sealed class TracerEffect : MonoBehaviour
    {
        [Tooltip("Sichtbare Fluggeschwindigkeit in m/s. Nicht die echte Kugel - die trifft sofort.")]
        [SerializeField] float _tempo = 340f;
        [Tooltip("Laenge des leuchtenden Stuecks in Metern.")]
        [SerializeField] float _segment = 6.5f;
        [Tooltip("Nachleuchten am Einschlag, nachdem das Stueck angekommen ist.")]
        [SerializeField] float _nachglut = 0.05f;
        [SerializeField] float _width = 0.045f;
        [SerializeField] Color _color = new(1f, 0.86f, 0.52f, 1f);
        [SerializeField] int _poolSize = 8;

        NetworkWeapon _weapon;
        Material _material;
        LineRenderer[] _lines;
        Vector3[] _von;
        Vector3[] _bis;
        float[] _alter;       // Sekunden seit dem Schuss, < 0 = Steckplatz frei
        float[] _flugzeit;
        int _next;

        // ---- Test-Haken (die Optik selbst ist nicht pruefbar) ----
        public int AktiveSpurenForTests
        {
            get
            {
                int n = 0;
                if (_lines == null) return 0;
                for (int i = 0; i < _lines.Length; i++)
                    if (_lines[i] != null && _lines[i].enabled) n++;
                return n;
            }
        }

        /// <summary>Laenge der sichtbaren Spur im Steckplatz - zum Pruefen, dass
        /// nie die ganze Strecke auf einmal gezeichnet wird.</summary>
        public float SpurLaengeForTests(int i)
        {
            if (_lines == null || i < 0 || i >= _lines.Length) return 0f;
            var lr = _lines[i];
            if (lr == null || !lr.enabled) return 0f;
            return Vector3.Distance(lr.GetPosition(0), lr.GetPosition(1));
        }

        public float SegmentForTests => _segment;

        void Awake()
        {
            _weapon = GetComponent<NetworkWeapon>();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _material = UrpMaterial.NeuFx(additiv: true, "TracerMat");
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", _color);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", _color);
            // Additiv: die Spur ist Licht. Vor heller Wand kaum zu sehen, im
            // Dunkeln hell - genau umgekehrt zur alten undurchsichtigen Linie.
            if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1f);
            if (_material.HasProperty("_Blend")) _material.SetFloat("_Blend", 1f);
            if (_material.HasProperty("_SrcBlend")) _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_material.HasProperty("_DstBlend")) _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (_material.HasProperty("_ZWrite")) _material.SetInt("_ZWrite", 0);
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            // Erst dieser Aufruf setzt das Schluesselwort, das die
            // Durchsichtigkeit im Shader wirklich einschaltet.
            UrpMaterial.Leuchtend(_material);
            // Ohne Textur ist die Linie ein hartes Rechteck mit sichtbaren Kanten.
            SoftParticleTexture.Anwenden(_material);

            _lines = new LineRenderer[_poolSize];
            _von = new Vector3[_poolSize];
            _bis = new Vector3[_poolSize];
            _alter = new float[_poolSize];
            _flugzeit = new float[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Tracer_{i}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = _material;
                lr.positionCount = 2;
                lr.numCapVertices = 3;
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.View;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.useWorldSpace = true;
                // hinten duenn, vorn voll - das Stueck hat eine Spitze
                lr.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.25f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 1f));
                lr.widthMultiplier = _width;
                lr.enabled = false;
                _lines[i] = lr;
                _alter[i] = -1f;
            }
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void OnEnable() => _weapon.FireVisual += Show;
        void OnDisable() => _weapon.FireVisual -= Show;

        void Show(ShotFx fx)
        {
            int i = _next;
            _von[i] = fx.Origin;
            _bis[i] = fx.End;
            _alter[i] = 0f;
            _flugzeit[i] = Mathf.Max(0.02f, Vector3.Distance(fx.Origin, fx.End) / Mathf.Max(1f, _tempo));

            var lr = _lines[i];
            lr.enabled = true;
            Zeichne(i, 0f);

            _next = (_next + 1) % _poolSize;
        }

        void Update()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                if (_alter[i] < 0f) continue;
                _alter[i] += Time.deltaTime;

                if (_alter[i] >= _flugzeit[i] + _nachglut)
                {
                    _lines[i].enabled = false;
                    _alter[i] = -1f;
                    continue;
                }
                Zeichne(i, _alter[i]);
            }
        }

        /// <summary>Setzt das leuchtende Stueck auf seinen Platz in der Bahn.</summary>
        void Zeichne(int i, float t)
        {
            var lr = _lines[i];
            Vector3 von = _von[i];
            Vector3 bis = _bis[i];
            float strecke = Vector3.Distance(von, bis);
            if (strecke < 0.01f) { lr.enabled = false; _alter[i] = -1f; return; }

            Vector3 richtung = (bis - von) / strecke;

            // Kopf laeuft von 0 bis zur vollen Strecke, dann bleibt er am Einschlag.
            float kopf = Mathf.Min(strecke, t * _tempo);
            float schwanz = Mathf.Max(0f, kopf - _segment);

            lr.SetPosition(0, von + richtung * schwanz);
            lr.SetPosition(1, von + richtung * kopf);

            // Nach dem Einschlag noch kurz verglimmen.
            float rest = _flugzeit[i] + _nachglut - t;
            float hell = _nachglut > 0f ? Mathf.Clamp01(rest / _nachglut) : 1f;
            var c = _color * hell;
            c.a = 1f;
            lr.startColor = c * 0.35f;   // hinten schwaecher
            lr.endColor = c;
            lr.widthMultiplier = _width * Mathf.Lerp(0.5f, 1f, hell);
        }
    }
}
