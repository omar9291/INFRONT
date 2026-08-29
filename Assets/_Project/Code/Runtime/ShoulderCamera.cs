using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Schulterkamera fuer Phase 2: folgt dem lokalen Charakter, uebernimmt
    /// dessen Yaw (Koerperdrehung) und den Aim-Pitch (hoch/runter zielen).
    ///
    /// In einer spaeteren Phase fuehrt die Maus die Kamera direkt und der
    /// Charakter dreht sich zur Kamera. Fuer jetzt reicht "Kamera folgt".
    /// </summary>
    public sealed class ShoulderCamera : MonoBehaviour
    {
        [SerializeField] float _distance = 4f;
        [SerializeField] float _height = 1.7f;
        [SerializeField] float _shoulder = 0.6f;
        [SerializeField] float _followLerp = 14f;

        Transform _target;
        NetworkPlayerController _controller;

        public void SetTarget(Transform target, NetworkPlayerController controller)
        {
            _target = target;
            _controller = controller;
        }

        void LateUpdate()
        {
            if (_target == null)
                return;

            float yaw = _target.eulerAngles.y;
            float pitch = _controller != null ? _controller.AimPitch : 0f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

            Vector3 focus = _target.position + Vector3.up * _height + rotation * (Vector3.right * _shoulder);
            Vector3 desired = focus - rotation * (Vector3.forward * _distance);

            transform.position = Vector3.Lerp(transform.position, desired, _followLerp * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, _followLerp * Time.deltaTime);
        }
    }
}
