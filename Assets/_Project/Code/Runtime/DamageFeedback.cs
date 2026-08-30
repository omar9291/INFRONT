using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Bild-Rueckmeldung fuer den lokalen Spieler:
    ///  - roter Bildrand in der Richtung, aus der ein Treffer kam
    ///  - Fadenkreuz, das kurz aufleuchtet, wenn man selbst trifft
    ///  - Todes-Blende (Bild wird schwarz) mit Respawn-Countdown
    ///
    /// Laeuft nur beim Besitzer. Platzhalter-IMGUI wie das restliche HUD.
    /// </summary>
    public sealed class DamageFeedback : NetworkBehaviour
    {
        struct Marker { public float Bearing; public float Strength; }

        [SerializeField] float _markerFade = 1.3f;
        [SerializeField] float _fadeToBlack = 0.7f;
        [SerializeField] float _fadeFromBlack = 0.5f;

        Health _health;
        NetworkWeapon _weapon;
        PlayerLifecycle _lifecycle;
        NetworkPlayerController _controller;
        Transform _camera;

        readonly List<Marker> _markers = new();
        float _black;
        bool _dead;
        float _deathTime;
        float _hitFlash;
        GUIStyle _big;
        GUIStyle _small;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _health = GetComponent<Health>();
            _weapon = GetComponent<NetworkWeapon>();
            _lifecycle = GetComponent<PlayerLifecycle>();
            _controller = GetComponent<NetworkPlayerController>();
            if (Camera.main != null) _camera = Camera.main.transform;

            _health.LocalDamageFrom += OnDamageFrom;
            _health.Died += OnDied;
            _health.Revived += OnRevived;
            if (_weapon != null) _weapon.LocalHitConfirmed += OnHitConfirmed;
        }

        public override void OnNetworkDespawn()
        {
            if (_health != null)
            {
                _health.LocalDamageFrom -= OnDamageFrom;
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }
            if (_weapon != null) _weapon.LocalHitConfirmed -= OnHitConfirmed;
        }

        void OnDamageFrom(Vector3 attackerPos)
        {
            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
            if (_camera == null) return;

            Vector3 local = _camera.InverseTransformDirection((attackerPos - transform.position).normalized);
            float bearing = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg; // 0 vorne, 90 rechts, 180 hinten
            _markers.Add(new Marker { Bearing = bearing, Strength = 1f });
        }

        void OnDied() { _dead = true; _deathTime = Time.time; }
        void OnRevived() { _dead = false; }
        void OnHitConfirmed() { _hitFlash = 1f; }

        void Update()
        {
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                m.Strength -= Time.deltaTime * _markerFade;
                if (m.Strength <= 0f) _markers.RemoveAt(i);
                else _markers[i] = m;
            }

            if (_hitFlash > 0f) _hitFlash = Mathf.Max(0f, _hitFlash - Time.deltaTime * 3f);

            // Beim Sterben kurz schwarz blinzeln, dann wieder aufblenden
            // (damit man zuschauen kann). Beim Wiederbeleben ebenfalls klar.
            float sinceDeath = Time.time - _deathTime;
            float target;
            if (_dead && sinceDeath < _fadeToBlack) target = 1f;          // reinblenden
            else if (_dead) target = 0f;                                  // wieder aufblenden
            else target = 0f;
            float speed = 1f / (_dead && sinceDeath < _fadeToBlack ? _fadeToBlack : _fadeFromBlack);
            _black = Mathf.MoveTowards(_black, target, speed * Time.deltaTime);
        }

        void OnGUI()
        {
            Color prev = GUI.color;

            // Rote Raender in Trefferrichtung
            foreach (var m in _markers)
            {
                GUI.color = new Color(0.75f, 0f, 0f, Mathf.Clamp01(m.Strength) * 0.5f);
                GUI.DrawTexture(EdgeRect(m.Bearing), Texture2D.whiteTexture);
            }
            GUI.color = prev;

            // Fadenkreuz
            if (!_dead && !PauseMenu.IsPaused && _black < 0.05f)
            {
                float cx = Screen.width / 2f, cy = Screen.height / 2f;
                float gap = 4f + _hitFlash * 7f;
                float len = 9f;
                GUI.color = _hitFlash > 0f ? new Color(1f, 0.55f, 0.25f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
                GUI.DrawTexture(new Rect(cx - 1, cy - gap - len, 2, len), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy + gap, 2, len), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - gap - len, cy - 1, len, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + gap, cy - 1, len, 2), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // Todes-Blende (kurzes Blinzeln)
            if (_black > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, _black);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // Zuschau-Anzeige, solange man tot ist
            if (_dead)
            {
                if (_big == null)
                    _big = new GUIStyle(GUI.skin.label)
                    { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true };
                if (_small == null)
                    _small = new GUIStyle(GUI.skin.label)
                    { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };

                string who = _controller != null ? _controller.SpectatingName : null;

                GUI.color = new Color(1f, 0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(0, 22f, Screen.width, 28f), "Ausgeschaltet", _big);
                GUI.color = prev;

                if (who != null)
                {
                    GUI.color = new Color(0.7f, 0.85f, 1f, 0.95f);
                    GUI.Label(new Rect(0, Screen.height - 90f, Screen.width, 30f),
                        $"Zuschauen bei  {who}", _small);
                    GUI.color = new Color(1f, 1f, 1f, 0.6f);
                    GUI.Label(new Rect(0, Screen.height - 62f, Screen.width, 22f),
                        "Linksklick / Rechtsklick  wechselt", _big);
                    GUI.color = prev;
                }
            }
        }

        static Rect EdgeRect(float bearing)
        {
            bearing = Mathf.Repeat(bearing + 180f, 360f) - 180f;
            float w = Screen.width, h = Screen.height;
            float tx = w * 0.15f, ty = h * 0.16f;

            if (bearing > -45f && bearing <= 45f) return new Rect(0, 0, w, ty);            // oben
            if (bearing > 45f && bearing <= 135f) return new Rect(w - tx, 0, tx, h);       // rechts
            if (bearing > 135f || bearing <= -135f) return new Rect(0, h - ty, w, ty);     // unten
            return new Rect(0, 0, tx, h);                                                  // links
        }
    }
}
