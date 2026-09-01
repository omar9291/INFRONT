using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kurze Zeitlupe bei grossen Momenten (Ace, Clutch, Matchgewinn) - der
    /// klassische "Wumms" am Rundenende. Nur solo (Host ohne weitere Spieler),
    /// damit im Online-Spiel niemand mit eingefroren wird. Setzt sich selbst
    /// immer zurueck, auch bei Fehlern oder Szenenwechsel.
    ///
    /// Haengt am Arena-HUD.
    /// </summary>
    public sealed class CinematicMoments : MonoBehaviour
    {
        MatchManager _hooked;
        Coroutine _running;
        float _restoreTimeScale = 1f;

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == _hooked) return;
            if (_hooked != null)
            {
                _hooked.HighlightReported -= OnHighlight;
                _hooked.MatchEnded -= OnMatchEnded;
            }
            _hooked = mm;
            if (_hooked != null)
            {
                _hooked.HighlightReported += OnHighlight;
                _hooked.MatchEnded += OnMatchEnded;
            }
        }

        void OnDestroy()
        {
            if (_hooked != null)
            {
                _hooked.HighlightReported -= OnHighlight;
                _hooked.MatchEnded -= OnMatchEnded;
            }
            Restore();
        }

        void OnDisable() => Restore();

        static bool Solo()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening && nm.ConnectedClients.Count <= 1;
        }

        void OnHighlight(int kindInt, ulong playerId)
        {
            var kind = (HighlightKind)kindInt;
            if (kind == HighlightKind.Ace || kind == HighlightKind.Clutch)
                Play(0.35f, 0.34f);
        }

        void OnMatchEnded(int winner) => Play(0.30f, 0.45f);

        void Play(float scale, float realSeconds)
        {
            var mm = MatchManager.Instance;
            if (mm != null && (mm.SuspendedForTests || mm.SkipFreezeForTests)) return;
            if (!Solo() || PauseMenu.IsPaused) return;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(SlowMo(scale, realSeconds));
        }

        IEnumerator SlowMo(float scale, float realSeconds)
        {
            _restoreTimeScale = 1f;   // Zielwert ist immer die normale Geschwindigkeit
            Time.timeScale = scale;

            float t = 0f;
            while (t < realSeconds)
            {
                // waehrend der Zeitlupe darf eine echte Pause dazwischenkommen
                if (PauseMenu.IsPaused) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // sanft zurueck auf Normaltempo
            float back = 0f;
            while (back < 0.25f && !PauseMenu.IsPaused)
            {
                back += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(scale, _restoreTimeScale, back / 0.25f);
                yield return null;
            }
            Restore();
            _running = null;
        }

        void Restore()
        {
            // Nie die echte Solo-Pause ueberschreiben (die haelt bewusst auf 0).
            if (PauseMenu.IsPaused) return;
            Time.timeScale = 1f;
        }
    }
}
