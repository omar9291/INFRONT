using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Einfache Schulterkamera fuer Phase 1: folgt dem lokalen Charakter mit
    /// festem Versatz und blickt auf ihn.
    ///
    /// Bewusst simpel. In einer spaeteren Phase steuert die Maus die Kamera
    /// direkt (Zielen), und der Charakter dreht sich zur Kamera statt umgekehrt.
    /// </summary>
    public sealed class ShoulderCamera : MonoBehaviour
    {
        [SerializeField] Vector3 _offset = new Vector3(0.7f, 1.8f, -3.5f);
        [SerializeField] float _followLerp = 12f;
        [SerializeField] float _lookHeight = 1.4f;

        Transform _target;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        void LateUpdate()
        {
            if (_target == null)
                return;

            Quaternion yaw = Quaternion.Euler(0f, _target.eulerAngles.y, 0f);
            Vector3 desired = _target.position + yaw * _offset;

            transform.position = Vector3.Lerp(transform.position, desired, _followLerp * Time.deltaTime);
            transform.LookAt(_target.position + Vector3.up * _lookHeight);
        }
    }
}
