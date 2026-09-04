using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Zeichnet ueber jedem lebenden Teamkameraden ein Schild (Name + duenner
    /// Lebensbalken). Weil das im HUD liegt, ist es durch Waende sichtbar.
    /// Nur beim Besitzer, nur Verbuendete, nicht man selbst.
    /// </summary>
    public sealed class FriendlyNameplates : NetworkBehaviour
    {
        [SerializeField] float _headHeight = 2.15f;
        [SerializeField] float _maxDistance = 80f;

        Transform _camera;
        TeamMember _me;
        GUIStyle _name;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            _me = GetComponent<TeamMember>();
            if (Camera.main != null) _camera = Camera.main.transform;
        }

        void OnGUI()
        {
            if (_me == null || _me.TeamId == Team.None) return;
            // Nicht nur den gemerkten Transform pruefen, sondern die Kamera
            // selbst. Der gemerkte Transform bleibt naemlich gueltig, wenn die
            // Kamera nur ABGESCHALTET wird - dann kommt aus Camera.main null,
            // der Waechter unten greift trotzdem, und WorldToScreenPoint fliegt
            // auf die Nase. Gemessen: 2838 NullReferenceExceptions in einem
            // einzigen Durchlauf, eine pro Bild. Passiert im Spiel zwischen Tod
            // und Wiedereinstieg.
            var cam = Camera.main;
            if (cam == null) return;
            if (_camera == null) _camera = cam.transform;

            if (_name == null)
                _name = new GUIStyle(GUI.skin.label)
                { fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            Color prev = GUI.color;

            foreach (var mate in Combatants.Everyone)
            {
                if (mate == null || mate == _me) continue;
                if (mate.TeamId != _me.TeamId) continue;
                if (mate.Health == null || !mate.Health.IsAlive) continue;

                Vector3 head = mate.transform.position + Vector3.up * _headHeight;
                if ((head - _camera.position).sqrMagnitude > _maxDistance * _maxDistance) continue;

                Vector3 sp = cam.WorldToScreenPoint(head);
                if (sp.z <= 0.3f) continue; // hinter der Kamera

                float x = sp.x;
                float y = Screen.height - sp.y;

                // Name
                GUI.color = new Color(0.6f, 0.8f, 1f, 0.95f);
                GUI.Label(new Rect(x - 60f, y - 22f, 120f, 18f), mate.DisplayName, _name);

                // Lebensbalken
                float f = mate.Health.Max > 0 ? (float)mate.Health.Current / mate.Health.Max : 0f;
                var bg = new Rect(x - 26f, y - 4f, 52f, 5f);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(bg, Texture2D.whiteTexture);
                GUI.color = new Color(0.4f, 0.75f, 1f, 0.95f);
                GUI.DrawTexture(new Rect(bg.x + 1f, bg.y + 1f, (bg.width - 2f) * f, bg.height - 2f), Texture2D.whiteTexture);
            }

            GUI.color = prev;
        }
    }
}
