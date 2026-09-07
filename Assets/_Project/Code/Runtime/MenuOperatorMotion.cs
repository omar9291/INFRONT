using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kleine, ruhige Bewegung für die Figur in der Menü-Kulisse. Sie bleibt
    /// reine Deko und bewegt weder Kollision noch Spielzustand.
    /// </summary>
    public sealed class MenuOperatorMotion : MonoBehaviour
    {
        [SerializeField] float _breathHeight = 0.016f;
        [SerializeField] float _turnDegrees = 1.25f;

        Vector3 _startPosition;
        Quaternion _startRotation;
        float _time;

        void Awake()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
        }

        void Update()
        {
            _time += Time.deltaTime;
            transform.localPosition = _startPosition + Vector3.up * (Mathf.Sin(_time * 1.1f) * _breathHeight);
            transform.localRotation = _startRotation * Quaternion.Euler(0f, Mathf.Sin(_time * 0.37f) * _turnDegrees, 0f);
        }
    }
}
