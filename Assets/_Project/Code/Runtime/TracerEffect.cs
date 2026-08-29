using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Zeichnet fuer jeden Schuss der Waffe auf demselben Objekt kurz eine Linie
    /// vom Lauf zum Trefferpunkt. Reine Optik, damit man selbst testen kann.
    /// </summary>
    [RequireComponent(typeof(NetworkWeapon))]
    public sealed class TracerEffect : MonoBehaviour
    {
        [SerializeField] float _lifetime = 0.05f;
        [SerializeField] Color _color = new(1f, 0.85f, 0.4f);

        NetworkWeapon _weapon;
        Material _material;

        void Awake()
        {
            _weapon = GetComponent<NetworkWeapon>();
            _material = new Material(Shader.Find("Sprites/Default"));
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        void OnEnable() => _weapon.FireVisual += Draw;
        void OnDisable() => _weapon.FireVisual -= Draw;

        void Draw(Vector3 origin, Vector3 end)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            line.widthMultiplier = 0.03f;
            line.sharedMaterial = _material;
            line.startColor = line.endColor = _color;
            line.numCapVertices = 2;
            Destroy(go, _lifetime);
        }
    }
}
