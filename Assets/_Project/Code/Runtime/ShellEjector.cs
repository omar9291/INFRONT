using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Wirft bei jedem Schuss eine kleine Patronenhülse aus der Waffe. Rein
    /// Optik: ein Ring aus wiederverwendeten Würfeln, einfache Flugbahn per
    /// Hand (keine Physik-Engine, keine Collider).
    ///
    /// Hängt am Spieler- und am Bot-Prefab.
    /// </summary>
    [RequireComponent(typeof(NetworkWeapon))]
    public sealed class ShellEjector : MonoBehaviour
    {
        const int PoolSize = 10;
        const float Life = 1.4f;
        const float Gravity = 12f;

        NetworkWeapon _weapon;
        Transform[] _shells;
        Vector3[] _vel;
        Vector3[] _spin;
        float[] _timer;
        int _next;
        Material _mat;

        void Awake()
        {
            _weapon = GetComponent<NetworkWeapon>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _mat = new Material(shader) { name = "ShellMat" };
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", new Color(0.85f, 0.65f, 0.25f));
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", new Color(0.85f, 0.65f, 0.25f));

            _shells = new Transform[PoolSize];
            _vel = new Vector3[PoolSize];
            _spin = new Vector3[PoolSize];
            _timer = new float[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Shell_{i}";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.localScale = new Vector3(0.03f, 0.03f, 0.08f);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _shells[i] = go.transform;
                _timer[i] = -1f;
            }
        }

        void OnEnable() => _weapon.FireVisual += OnFire;
        void OnDisable() => _weapon.FireVisual -= OnFire;

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        void OnFire(ShotFx fx)
        {
            int i = _next;
            _next = (_next + 1) % PoolSize;

            var t = _shells[i];
            t.position = fx.Origin - transform.forward * 0.15f + transform.up * 0.05f;
            t.rotation = Random.rotation;
            t.gameObject.SetActive(true);

            // nach rechts oben aus der Waffe, mit etwas Streuung
            _vel[i] = transform.right * Random.Range(1.4f, 2.2f)
                      + transform.up * Random.Range(1.6f, 2.4f)
                      - transform.forward * Random.Range(0f, 0.4f);
            _spin[i] = new Vector3(Random.Range(-720f, 720f), Random.Range(-720f, 720f), Random.Range(-720f, 720f));
            _timer[i] = Life;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_timer[i] < 0f) continue;
                _timer[i] -= dt;
                if (_timer[i] <= 0f) { _shells[i].gameObject.SetActive(false); continue; }

                _vel[i] += Vector3.down * Gravity * dt;
                _shells[i].position += _vel[i] * dt;
                _shells[i].Rotate(_spin[i] * dt, Space.Self);

                // Auf dem Boden zur Ruhe kommen: Sinken bremsen, Drehen stoppen.
                if (_vel[i].y < 0f && _shells[i].position.y < transform.position.y - 0.4f)
                {
                    _vel[i] = Vector3.Lerp(_vel[i], Vector3.zero, 10f * dt);
                    _spin[i] = Vector3.Lerp(_spin[i], Vector3.zero, 10f * dt);
                }
            }
        }
    }
}
