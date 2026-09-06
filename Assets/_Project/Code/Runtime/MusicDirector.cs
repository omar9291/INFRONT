using UnityEngine;
using UnityEngine.SceneManagement;

namespace Infront
{
    /// <summary>Eigene Musiklautstärke; Menü, Rundenauftakt und die letzten
    /// 30 Sekunden. Keine Spielregeln und keine Änderung der Bot-Wahrnehmung.</summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        public static MusicDirector Instance { get; private set; }
        AudioSource _menu, _tension, _sting;
        MatchManager _hooked;
        public bool MenuPlayingForTests => _menu != null && _menu.isPlaying;
        public float MenuVolumeForTests => _menu != null ? _menu.volume : 0f;
        public float TensionVolumeForTests => _tension != null ? _tension.volume : 0f;

        void Awake() => Instance = this;

        void Start()
        {
            _menu = MakeSource("MenuMusic", SoundId.MusikMenue, true);
            _tension = MakeSource("TensionMusic", SoundId.MusikSpannung, true);
            _sting = MakeSource("RoundMusic", SoundId.MusikRundenstart, false);
        }

        AudioSource MakeSource(string name, SoundId id, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            source.volume = 0f;
            source.clip = AudioService.Instance.Resolve(id);
            return source;
        }

        void Update()
        {
            var match = MatchManager.Instance;
            if (_hooked != match)
            {
                if (_hooked != null) _hooked.RoundStarted -= RoundStarted;
                _hooked = match;
                if (_hooked != null)
                {
                    _hooked.RoundStarted += RoundStarted;
                    // Beim ersten Match kann StartRound bereits vor dem ersten
                    // Update ausgelöst worden sein. Der Kaufauftakt ist noch frisch.
                    if (_hooked.IsSpawned && _hooked.IsBuyTime) RoundStarted();
                }
            }
            bool inMenu = SceneManager.GetActiveScene().name == GameFlow.MenuScene;
            bool ready = match != null && match.IsSpawned;
            bool playing = ready && match.CurrentPhase == MatchManager.Phase.Playing && match.MatchWinner == Team.None;
            bool planted = ready && Bomb.Instance != null && Bomb.Instance.IsPlanted;
            double seconds = ready ? (planted ? Bomb.Instance.FuseSecondsLeft : match.SecondsRemaining) : 0;
            float tension = TensionFor(inMenu, playing, ready && match.IsBuyTime, PauseMenu.IsPaused, seconds);
            float volume = Mathf.Clamp01(GameSettings.MusicVolume);
            Fade(_menu, inMenu && !PauseMenu.IsPaused ? volume * .16f : 0f, volume == 0f);
            Fade(_tension, volume * .08f * tension, volume == 0f);
            if (_sting != null)
            {
                _sting.volume = !inMenu && playing && !PauseMenu.IsPaused ? volume * .18f : 0f;
                if (!playing || inMenu || PauseMenu.IsPaused || volume == 0f) _sting.Stop();
            }
        }

        public static float TensionFor(bool inMenu, bool playing, bool buying, bool paused, double seconds)
            => inMenu || !playing || buying || paused || seconds <= 0 || seconds > 30 ? 0f
                : Mathf.Lerp(.35f, 1f, 1f - (float)seconds / 30f);

        static void Fade(AudioSource source, float target, bool mute)
        {
            if (source == null) return;
            source.volume = mute ? 0f : Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * .3f);
            if (target > 0f && !source.isPlaying) source.Play();
            if (target == 0f && source.volume <= 0f) source.Stop();
        }

        void RoundStarted()
        {
            if (_sting == null || GameSettings.MusicVolume <= 0f) return;
            _sting.volume = Mathf.Clamp01(GameSettings.MusicVolume) * .18f;
            _sting.Play();
        }

        void OnDestroy()
        {
            if (_hooked != null) _hooked.RoundStarted -= RoundStarted;
            if (Instance == this) Instance = null;
        }
    }
}
