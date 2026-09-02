using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Lässt ein Punktlicht unruhig flackern - für die Notlampen in den Tunneln
    /// und an kaputten Leuchten. Rein optisch, keine Spiel-Logik.
    ///
    /// NICHT prüfbar: wie es aussieht. Prüfbar ist nur, dass die Intensität
    /// im erwarteten Band bleibt (siehe <see cref="IntensityForTests"/>).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LampFlicker : MonoBehaviour
    {
        [SerializeField] float _baseIntensity = 6f;
        [SerializeField] float _amount = 0.35f;      // wie stark es schwankt (0..1)
        [SerializeField] float _speed = 11f;         // wie schnell
        [SerializeField] float _dropoutChance = 0.015f;  // kurze Aussetzer pro Frame

        Light _light;
        float _seed;

        public float IntensityForTests => _light != null ? _light.intensity : 0f;
        public float BaseIntensityForTests => _baseIntensity;

        void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.value * 100f;
            if (_baseIntensity <= 0f) _baseIntensity = _light.intensity;
        }

        void Update()
        {
            if (_light == null) return;
            float noise = Mathf.PerlinNoise(_seed, Time.time * _speed);
            float dropout = Random.value < _dropoutChance ? 0.35f : 1f;
            _light.intensity = _baseIntensity * dropout * (1f - _amount + _amount * noise);
        }
    }
}
