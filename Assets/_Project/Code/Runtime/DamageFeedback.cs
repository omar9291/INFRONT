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
        Transform _camera;

        readonly List<Marker> _markers = new();
        float _black;
        bool _dead;
        float _deathTime;
        float _hitFlash;
        GUIStyle _big;

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

            float target = _dead ? 1f : 0f;
            float speed = _dead ? 1f / _fadeToBlack : 1f / _fadeFromBlack;
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

            // Todes-Blende
            if (_black > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, _black);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;

                if (_dead && _black > 0.55f)
                {
                    if (_big == null)
                        _big = new GUIStyle(GUI.skin.label)
                        { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                    float delay = _lifecycle != null ? _lifecycle.RespawnDelay : 3f;
                    int rest = Mathf.Max(0, Mathf.CeilToInt(delay - (Time.time - _deathTime)));
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((_black - 0.55f) / 0.4f));
                    GUI.Label(new Rect(0, Screen.height / 2f - 50, Screen.width, 100),
                        $"Ausgeschaltet\nRespawn in {rest}", _big);
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
