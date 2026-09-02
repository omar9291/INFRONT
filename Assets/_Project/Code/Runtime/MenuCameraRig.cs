using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Langsame Kamerafahrt hinter dem Hauptmenü: schwenkt gemächlich im Bogen
    /// um die Kulisse und wiegt dabei leicht auf und ab - wie das ruhige
    /// Hintergrundbild bei CS2 oder Valorant. Rein optisch, keine Spiel-Logik.
    ///
    /// NICHT prüfbar: wie es aussieht. Prüfbar ist nur, dass die Kamera in
    /// Bewegung bleibt und immer die Kulisse anschaut.
    /// </summary>
    public sealed class MenuCameraRig : MonoBehaviour
    {
        [SerializeField] Vector3 _center = new Vector3(0f, 1.9f, 4f);
        [SerializeField] float _radius = 11f;
        [SerializeField] float _height = 3.6f;
        [SerializeField] float _arcDegrees = 24f;   // Schwenkbreite
        [SerializeField] float _speed = 0.12f;      // Schwenk-Tempo
        [SerializeField] float _bob = 0.22f;        // Auf-/Ab-Wiegen

        float _t;

        void OnEnable() => _t = 0f;

        void Update()
        {
            _t += Time.deltaTime;

            // Grundrichtung: von -Z auf die Kulisse schauen, plus langsamer Bogen.
            float ang = (-90f + _arcDegrees * Mathf.Sin(_t * _speed)) * Mathf.Deg2Rad;
            float bob = Mathf.Sin(_t * 0.35f) * _bob;

            Vector3 pos = _center + new Vector3(
                Mathf.Cos(ang) * _radius,
                _height + bob,
                Mathf.Sin(ang) * _radius);

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(
                (_center + Vector3.up * 0.4f) - pos, Vector3.up);
        }
    }
}
