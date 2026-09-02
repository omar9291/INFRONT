using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Kamerafahrt hinter dem Hauptmenü: schwenkt gemächlich im Bogen um die
    /// Kulisse, wiegt leicht auf und ab - und folgt zusätzlich der Maus, sodass
    /// die Kulisse spürbar mitkippt (Parallaxe). Das ist der Effekt, der wirklich
    /// nach 3D-Tiefe aussieht, wie das Hintergrundbild bei CS2 oder Valorant.
    /// Rein optisch, keine Spiel-Logik.
    ///
    /// NICHT prüfbar: wie es aussieht. Prüfbar ist nur, dass die Kamera in
    /// Bewegung bleibt und immer die Kulisse anschaut.
    /// </summary>
    public sealed class MenuCameraRig : MonoBehaviour
    {
        [SerializeField] Vector3 _center = new Vector3(0f, 1.9f, 4f);
        [SerializeField] float _radius = 11f;
        [SerializeField] float _height = 3.6f;
        [SerializeField] float _arcDegrees = 34f;    // Schwenkbreite
        [SerializeField] float _speed = 0.16f;       // Schwenk-Tempo
        [SerializeField] float _bob = 0.24f;         // Auf-/Ab-Wiegen
        [SerializeField] float _mouseParallax = 2.6f; // wie weit die Kamera der Maus folgt
        [SerializeField] float _parallaxEase = 2.6f;  // wie träge sie nachzieht

        float _t;
        Vector2 _look;   // geglättete Mausablage, Bildmitte = 0, Rand = ±1

        void OnEnable()
        {
            _t = 0f;
            _look = Vector2.zero;
        }

        void Update()
        {
            _t += Time.deltaTime;

            // Maus-Parallaxe: Zielablage aus der Mausposition, dann weich nachziehen.
            Vector2 target = Vector2.zero;
            var mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = mouse.position.ReadValue();
                target = new Vector2(
                    Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f),
                    Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            _look = Vector2.Lerp(_look, target, Time.deltaTime * _parallaxEase);

            // Grundrichtung: von -Z auf die Kulisse schauen, plus langsamer Bogen.
            float ang = (-90f + _arcDegrees * Mathf.Sin(_t * _speed)) * Mathf.Deg2Rad;
            float bob = Mathf.Sin(_t * 0.35f) * _bob;

            Vector3 pos = _center + new Vector3(
                Mathf.Cos(ang) * _radius + _look.x * _mouseParallax,
                _height + bob + _look.y * _mouseParallax * 0.45f,
                Mathf.Sin(ang) * _radius);

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(
                (_center + Vector3.up * 0.4f) - pos, Vector3.up);
        }
    }
}
