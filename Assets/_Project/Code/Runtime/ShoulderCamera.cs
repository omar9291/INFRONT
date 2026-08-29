using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Third-Person-Schulterkamera. Die MAUS fuehrt die Kamera: Yaw und Pitch
    /// kommen direkt vom lokalen Spieler (SetView), nicht vom Koerper. Dadurch
    /// gibt es keine Rueckkopplung "Koerper dreht -> Kamera folgt -> Bild
    /// schwenkt weg" mehr, und die Rotation hat keine Verzoegerung.
    ///
    /// Die Position wird bildratenunabhaengig leicht geglaettet, damit die
    /// 50-Hz-Schritte der Server-Bewegung nicht ruckeln.
    /// </summary>
    public sealed class ShoulderCamera : MonoBehaviour
    {
        [SerializeField] float _distance = 4.2f;
        [SerializeField] float _height = 1.6f;
        [SerializeField] float _shoulder = 0.6f;
        [SerializeField] float _positionSmooth = 12f;
        [SerializeField] LayerMask _wallMask = ~0;

        Transform _anchor;
        float _yaw;
        float _pitch;
        bool _hasView;

        public void SetTarget(Transform anchor)
        {
            _anchor = anchor;
            if (anchor != null)
            {
                transform.position = anchor.position - anchor.forward * _distance + Vector3.up * _height;
            }
        }

        public void SetView(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = pitch;
            _hasView = true;
        }

        void LateUpdate()
        {
            if (_anchor == null || !_hasView)
                return;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focus = _anchor.position + Vector3.up * _height + rotation * (Vector3.right * _shoulder);
            Vector3 desired = focus - rotation * (Vector3.forward * _distance);

            // Kamera nicht durch Waende schieben
            Vector3 dir = desired - focus;
            float dist = dir.magnitude;
            if (dist > 0.01f && Physics.Raycast(focus, dir / dist, out RaycastHit hit, dist, _wallMask, QueryTriggerInteraction.Ignore))
                desired = hit.point + hit.normal * 0.2f;

            float t = 1f - Mathf.Exp(-_positionSmooth * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, desired, t), rotation);
        }
    }
}
