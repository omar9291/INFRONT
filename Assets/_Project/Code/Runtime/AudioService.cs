using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Die eine Stelle, die Töne abspielt. Lebt auf dem GameFlow-Objekt und
    /// übersteht Szenenwechsel (wie der Ladebildschirm).
    ///
    /// Ablauf:
    ///  - Spiel-Code ruft <see cref="PlayAt"/> (3D, am Ort) oder
    ///    <see cref="Play2D"/> (direkt am Ohr) mit einer <see cref="SoundId"/>.
    ///  - Beim ersten Mal wird der Clip geladen: liegt eine echte Datei in
    ///    <c>Assets/_Project/Audio/Resources/&lt;name&gt;</c>, wird die genommen;
    ///    sonst baut <see cref="ProceduralSfx"/> einen Platzhalter. Danach ist
    ///    der Clip zwischengespeichert.
    ///  - Für 3D-Töne gibt es einen kleinen Ring wiederverwendeter
    ///    AudioSources, damit nicht ständig Objekte entstehen und vergehen.
    ///
    /// Kein AudioListener hier - der sitzt an der Kamera.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        const int PoolSize = 16;

        readonly Dictionary<SoundId, AudioClip> _clips = new();
        AudioSource[] _pool;
        int _next;
        AudioSource _flat;   // 2D, für den lokalen Spieler

        // ---- Test-Schnittstelle ----
        public SoundId? LastPlayedForTests { get; private set; }
        public int PlayCountForTests { get; private set; }
        /// <summary>Endgültige Lautstärke der letzten Anfrage (nach Gesamtlautstärke).</summary>
        public float LastVolumeForTests { get; private set; }
        public void ResetTestState()
        {
            LastPlayedForTests = null;
            PlayCountForTests = 0;
            LastVolumeForTests = -1f;
        }
        public bool IsCachedForTests(SoundId id) => _clips.ContainsKey(id);

        /// <summary>Sorgt dafür, dass es den Dienst gibt (für Tests, die keine
        /// Szene laden - normal erledigt das GameFlow.Bootstrap).</summary>
        public static AudioService EnsureForTests()
        {
            if (Instance == null)
            {
                var go = new GameObject("AudioService (Test)");
                Instance = go.AddComponent<AudioService>();
            }
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
                _pool[i] = MakeSource($"Sfx3D_{i}", spatial: true);

            _flat = MakeSource("Sfx2D", spatial: false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource MakeSource(string label, bool spatial)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = spatial ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 3f;
            src.maxDistance = 65f;
            src.dopplerLevel = 0f;
            return src;
        }

        float Master => Mathf.Clamp01(GameSettings.SfxVolume);

        AudioClip Clip(SoundId id)
        {
            if (_clips.TryGetValue(id, out var cached) && cached != null)
                return cached;

            AudioClip clip = Resources.Load<AudioClip>(FileName(id));   // echte Datei?
            if (clip == null) clip = ProceduralSfx.Build(id);           // sonst Platzhalter
            _clips[id] = clip;
            return clip;
        }

        /// <summary>Ton an einer Weltposition (3D, mit Entfernung und Richtung).</summary>
        public void PlayAt(SoundId id, Vector3 position, float volume = 1f, float pitchJitter = 0f)
        {
            float v = Mathf.Clamp01(volume * Master);
            LastPlayedForTests = id;
            PlayCountForTests++;
            LastVolumeForTests = v;

            if (v <= 0.0001f) return;

            if (_pool == null) return;

            var src = _pool[_next];
            _next = (_next + 1) % PoolSize;

            src.transform.position = position;
            src.clip = Clip(id);
            src.volume = v;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
        }

        /// <summary>Ton direkt am Ohr (2D), z.B. Trefferbestätigung, Rundenmeldung.</summary>
        public void Play2D(SoundId id, float volume = 1f)
        {
            float v = Mathf.Clamp01(volume * Master);
            LastPlayedForTests = id;
            PlayCountForTests++;
            LastVolumeForTests = v;

            if (v <= 0.0001f || _flat == null) return;

            _flat.pitch = 1f;
            _flat.PlayOneShot(Clip(id), v);
        }

        /// <summary>Enum-Name in den Dateinamen zum Austauschen: SchussGewehr -> schuss_gewehr.</summary>
        public static string FileName(SoundId id)
        {
            string s = id.ToString();
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
