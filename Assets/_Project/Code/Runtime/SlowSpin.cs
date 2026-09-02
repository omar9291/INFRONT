using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Dreht ein Objekt langsam um eine Achse - für den kreisenden Suchscheinwerfer
    /// in der Menü-Kulisse. Rein optisch.
    /// </summary>
    public sealed class SlowSpin : MonoBehaviour
    {
        [SerializeField] Vector3 _axis = Vector3.up;
        [SerializeField] float _degreesPerSecond = 12f;

        void Update()
        {
            transform.Rotate(_axis, _degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
