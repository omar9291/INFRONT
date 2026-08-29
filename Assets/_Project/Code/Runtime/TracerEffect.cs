using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Zeigt fuer jeden Schuss kurz eine Linie vom Lauf zum Trefferpunkt.
    ///
    /// Wichtig gegen Ruckeln und unsichtbare Schuesse:
    ///  - URP-tauglicher Shader (nicht das alte "Sprites/Default").
    ///  - Feste kleine Anzahl wiederverwendeter LineRenderer statt pro Schuss
    ///    ein neues GameObject zu erzeugen und zu zerstoeren.
    /// </summary>
    [RequireComponent(typeof(NetworkWeapon))]
    public sealed class TracerEffect : MonoBehaviour
    {
        [SerializeField] float _lifetime = 0.06f;
        [SerializeField] float _width = 0.04f;
        [SerializeField] Color _color = new(1f, 0.9f, 0.5f, 1f);
        [SerializeField] int _poolSize = 4;

        NetworkWeapon _weapon;
        Material _material;
        LineRenderer[] _lines;
        float[] _timers;
        int _next;

        void Awake()
        {
            _weapon = GetComponent<NetworkWeapon>();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _material = new Material(shader) { name = "TracerMat" };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", _color);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", _color);

            _lines = new LineRenderer[_poolSize];
            _timers = new float[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Tracer_{i}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = _material;
                lr.widthMultiplier = _width;
                lr.numCapVertices = 2;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startColor = lr.endColor = _color;
                lr.enabled = false;
                _lines[i] = lr;
                _timers[i] = -1f;
            }
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void OnEnable() => _weapon.FireVisual += Show;
        void OnDisable() => _weapon.FireVisual -= Show;

        void Show(Vector3 origin, Vector3 end)
        {
            var lr = _lines[_next];
            lr.SetPosition(0, origin);
            lr.SetPosition(1, end);
            lr.enabled = true;
            _timers[_next] = _lifetime;
            _next = (_next + 1) % _poolSize;
        }

        void Update()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                if (_timers[i] <= 0f) continue;
                _timers[i] -= Time.deltaTime;
                if (_timers[i] <= 0f)
                    _lines[i].enabled = false;
            }
        }
    }
}
