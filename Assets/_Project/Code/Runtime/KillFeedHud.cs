using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Datenquelle fuer den Kill-Feed oben rechts ("Alpha-2  ›  Bravo-1") und
    /// die Sondermeldungen (Bombe, Bot-Ansagen). Der Server schickt nur zwei
    /// Objekt-Nummern; den Namen baut jeder Client selbst daraus.
    ///
    /// Gezeichnet wird das im <see cref="HudController"/> (UI Toolkit) - diese
    /// Klasse haelt nur die Liste der Eintraege und raeumt alte weg.
    /// </summary>
    public sealed class KillFeedHud : MonoBehaviour
    {
        public struct Entry
        {
            public string Killer; public int KillerTeam; public string Victim; public int VictimTeam;
            public string Note; public Color NoteColor;   // gesetzt = Sondermeldung statt Abschuss
            public float Time;
        }

        public static KillFeedHud Instance { get; private set; }

        readonly List<Entry> _entries = new();
        MatchManager _hooked;

        /// <summary>Fuer das HUD: die aktuellen Feed-Eintraege (aeltester zuerst).</summary>
        public IReadOnlyList<Entry> EntriesForHud => _entries;

        /// <summary>Nur fuer Tests: die zuletzt eingegangene Sondermeldung.</summary>
        public string LastNoteForTests { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm != _hooked)
            {
                if (_hooked != null)
                {
                    _hooked.KillReported -= OnKill; _hooked.BombEventReported -= OnBombEvent;
                    _hooked.CalloutReported -= OnCallout;
                }
                _hooked = mm;
                if (_hooked != null)
                {
                    _hooked.KillReported += OnKill; _hooked.BombEventReported += OnBombEvent;
                    _hooked.CalloutReported += OnCallout;
                }
            }

            float now = Time.time;
            _entries.RemoveAll(e => now - e.Time > 5f);
        }

        void OnDestroy()
        {
            if (_hooked != null)
            {
                _hooked.KillReported -= OnKill; _hooked.BombEventReported -= OnBombEvent;
                _hooked.CalloutReported -= OnCallout;
            }
            if (Instance == this) Instance = null;
        }

        void OnCallout(string text, int team)
        {
            var c = ColorFor(team, LocalTeam());
            _entries.Add(new Entry { Note = text, NoteColor = c, Time = Time.time });
            if (_entries.Count > 6) _entries.RemoveAt(0);
            LastNoteForTests = text;
        }

        void OnBombEvent(int kind, ulong actorId)
        {
            string name = null; int team = Team.None;
            if (actorId != 0) Resolve(actorId, out name, out team);

            string txt = kind switch
            {
                (int)MatchManager.BombEvent.Gelegt =>
                    name != null ? $"{name} hat die Bombe gelegt" : "Die Bombe wurde gelegt",
                (int)MatchManager.BombEvent.Entschaerft =>
                    name != null ? $"{name} hat die Bombe entschaerft" : "Die Bombe wurde entschaerft",
                _ => "Die Bombe ist explodiert!"
            };
            Color c = kind switch
            {
                (int)MatchManager.BombEvent.Gelegt => new Color(1f, 0.6f, 0.1f),
                (int)MatchManager.BombEvent.Entschaerft => new Color(0.3f, 0.7f, 1f),
                _ => new Color(1f, 0.35f, 0.25f)
            };

            _entries.Add(new Entry { Note = txt, NoteColor = c, Time = Time.time });
            if (_entries.Count > 6) _entries.RemoveAt(0);
            LastNoteForTests = txt;
        }

        void OnKill(ulong killerId, ulong victimId)
        {
            Resolve(victimId, out string vName, out int vTeam);
            string kName = "?"; int kTeam = Team.None;
            if (killerId != 0) Resolve(killerId, out kName, out kTeam);

            _entries.Add(new Entry
            {
                Killer = killerId != 0 ? kName : null,
                KillerTeam = kTeam,
                Victim = vName,
                VictimTeam = vTeam,
                Time = Time.time
            });
            if (_entries.Count > 6) _entries.RemoveAt(0);
        }

        static void Resolve(ulong id, out string name, out int team)
        {
            name = "?"; team = Team.None;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null
                && nm.SpawnManager.SpawnedObjects.TryGetValue(id, out var no) && no != null)
            {
                var tm = no.GetComponent<TeamMember>();
                if (tm != null) { name = tm.DisplayName; team = tm.TeamId; }
            }
        }

        static Color ColorFor(int team, int myTeam)
        {
            if (team == Team.None) return new Color(0.8f, 0.8f, 0.8f);
            return team == myTeam ? new Color(0.45f, 0.65f, 1f) : new Color(1f, 0.5f, 0.45f);
        }

        static int LocalTeam()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return Team.None;
            var tm = nm.LocalClient.PlayerObject.GetComponent<TeamMember>();
            return tm != null ? tm.TeamId : Team.None;
        }
    }
}
