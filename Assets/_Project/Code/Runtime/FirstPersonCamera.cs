using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Ego-Kamera. Sitzt auf Augenhoehe des lokalen Spielers und gehorcht nur
    /// der Maus: Position folgt dem Kopf (leicht geglaettet gegen die 50-Hz-
    /// Schritte der Server-Bewegung), Drehung kommt sofort und ungefiltert vom
    /// Eingang - die Drehung ist fuers Gefuehl entscheidend.
    ///
    /// Zeigt fuer den lokalen Spieler eine Platzhalter-Waffe unten rechts.
    /// </summary>
    public sealed class FirstPersonCamera : MonoBehaviour
    {
        [SerializeField] float _positionSmooth = 20f;

        Transform _anchor;
        float _yaw;
        float _pitch;
        bool _hasView;
        GameObject _viewModel;
        bool _externalViewModel;   // ein echtes ViewModel-Bauteil hat uebernommen

        bool _spectating;
        Vector3 _specPos;
        Vector3 _specDir = Vector3.forward;

        // Kamera-Wackeln (Explosion). Klingt ueber die Dauer linear ab.
        float _shakeAmp;
        float _shakeDecay;

        // Kurzer Blickfeld-Stoss (z.B. auf einen Abschuss). Geht von selbst zurueck.
        Camera _cam;
        float _baseFov;
        float _fovOffset;
        float _fovRecover;

        // Ziel-Blickfeld: 0 = normal, sonst der Wert beim Zielen ueber Kimme/Korn
        // bzw. im Zielfernrohr. Wird weich angefahren.
        float _zoomTargetFov;
        float _zoomFov;
        float _zoomLerp = 12f;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null) _baseFov = _cam.fieldOfView;
            _zoomFov = _baseFov;
        }

        /// <summary>Das normale (ungezoomte) Blickfeld dieser Kamera.</summary>
        public float BaseFov => _baseFov;

        /// <summary>Ziel-Blickfeld beim Zielen setzen. 0 = wieder normal.
        /// lerp = wie schnell es angefahren wird (groesser = schneller).</summary>
        public void SetAimZoom(float targetFov, float lerp)
        {
            _zoomTargetFov = targetFov;
            _zoomLerp = Mathf.Max(1f, lerp);
        }

        /// <summary>Blickfeld kurz um delta Grad verstellen (negativ = zoomt rein),
        /// dann mit recoverPerSec Grad/Sekunde zurueck.</summary>
        public void AddFovKick(float delta, float recoverPerSec)
        {
            _fovOffset += delta;
            _fovRecover = Mathf.Max(1f, recoverPerSec);
        }

        /// <summary>Kamera kurz wackeln lassen (z.B. Bomben-Explosion).
        /// amplitude ~0..1, duration in Sekunden.</summary>
        public void Shake(float amplitude, float duration)
        {
            _shakeAmp = Mathf.Max(_shakeAmp, amplitude);
            _shakeDecay = _shakeAmp / Mathf.Max(0.05f, duration);
        }

        public void SetTarget(Transform eyeAnchor)
        {
            _anchor = eyeAnchor;
            if (eyeAnchor != null)
                transform.position = eyeAnchor.position;
            EnsureViewModel();
        }

        public void SetView(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = pitch;
            _hasView = true;
        }

        /// <summary>Zuschauen: Kamera an fremde Augen setzen.</summary>
        public void SetSpectate(Vector3 eyePos, Vector3 lookDir)
        {
            _spectating = true;
            _hasView = true;
            _specPos = eyePos;
            if (lookDir.sqrMagnitude > 0.0001f) _specDir = lookDir.normalized;
            if (_viewModel != null) _viewModel.SetActive(false);
        }

        public void StopSpectate()
        {
            _spectating = false;
            if (_viewModel != null) _viewModel.SetActive(true);
        }

        /// <summary>Schaut die Kamera gerade fremden Augen zu? (Das View Model
        /// blendet sich dann aus.)</summary>
        public bool IsSpectating => _spectating;

        /// <summary>Nur fuer Tests: schaut die Kamera gerade fremden Augen zu?</summary>
        public bool IsSpectatingForTests => _spectating;

        /// <summary>Ein echtes <see cref="ViewModel"/>-Bauteil uebernimmt die Waffe
        /// in der Hand - den Platzhalter-Wuerfel dann nicht bauen / wieder entfernen.</summary>
        public void HandOffViewModel()
        {
            _externalViewModel = true;
            if (_viewModel != null)
            {
                Destroy(_viewModel);
                _viewModel = null;
            }
        }

        /// <summary>Nur fuer Tests: das Ziel, dem gerade zugeschaut wird.</summary>
        public Vector3 SpectateTargetForTests => _specPos;

        void EnsureViewModel()
        {
            if (_externalViewModel || _viewModel != null) return;

            _viewModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _viewModel.name = "ViewModel_Weapon";
            Object.DestroyImmediate(_viewModel.GetComponent<Collider>());
            _viewModel.transform.SetParent(transform, false);
            _viewModel.transform.localPosition = new Vector3(0.32f, -0.28f, 0.65f);
            _viewModel.transform.localRotation = Quaternion.Euler(0f, -4f, 0f);
            _viewModel.transform.localScale = new Vector3(0.12f, 0.14f, 0.5f);

            var r = _viewModel.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (!_hasView) return;

            if (_spectating)
            {
                float ts = 1f - Mathf.Exp(-_positionSmooth * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, _specPos, ts);
                var look = Quaternion.LookRotation(_specDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, ts * 1.5f);
            }
            else if (_anchor != null)
            {
                float t = 1f - Mathf.Exp(-_positionSmooth * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, _anchor.position, t);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            ApplyShake();
            ApplyFovKick();
        }

        void ApplyFovKick()
        {
            if (_cam == null) return;
            if (Mathf.Abs(_fovOffset) > 0.01f)
                _fovOffset = Mathf.MoveTowards(_fovOffset, 0f, _fovRecover * Time.deltaTime);
            else
                _fovOffset = 0f;

            float baseTarget = _zoomTargetFov > 0.01f ? _zoomTargetFov : _baseFov;
            _zoomFov = Mathf.Lerp(_zoomFov, baseTarget, 1f - Mathf.Exp(-_zoomLerp * Time.deltaTime));

            _cam.fieldOfView = _zoomFov + _fovOffset;
        }

        void ApplyShake()
        {
            if (_shakeAmp <= 0f) return;

            _shakeAmp = Mathf.Max(0f, _shakeAmp - _shakeDecay * Time.deltaTime);

            float a = _shakeAmp;
            float px = (Mathf.PerlinNoise(Time.time * 31f, 0.7f) - 0.5f) * 8f * a;
            float py = (Mathf.PerlinNoise(0.3f, Time.time * 29f) - 0.5f) * 8f * a;
            float pz = (Mathf.PerlinNoise(Time.time * 23f, Time.time * 19f) - 0.5f) * 6f * a;
            transform.rotation *= Quaternion.Euler(px, py, pz);
        }
    }
}
