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

        bool _spectating;
        Vector3 _specPos;
        Vector3 _specDir = Vector3.forward;

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

        void EnsureViewModel()
        {
            if (_viewModel != null) return;

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
                return;
            }

            if (_anchor == null)
                return;

            float t = 1f - Mathf.Exp(-_positionSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _anchor.position, t);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
