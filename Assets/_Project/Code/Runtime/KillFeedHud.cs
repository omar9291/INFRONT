using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kill-Feed oben rechts: "Alpha-2  &gt;  Bravo-1". Der Server schickt nur
    /// zwei Objekt-Nummern; den Namen baut jeder Client selbst daraus.
    /// </summary>
    public sealed class KillFeedHud : MonoBehaviour
    {
        struct Entry { public string Killer; public int KillerTeam; public string Victim; public int VictimTeam; public float Time; }

        readonly List<Entry> _entries = new();
        MatchManager _hooked;
        GUIStyle _style;

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm != _hooked)
            {
                if (_hooked != null) _hooked.KillReported -= OnKill;
                _hooked = mm;
                if (_hooked != null) _hooked.KillReported += OnKill;
            }

            float now = Time.time;
            _entries.RemoveAll(e => now - e.Time > 5f);
        }

        void OnDestroy()
        {
            if (_hooked != null) _hooked.KillReported -= OnKill;
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

        void OnGUI()
        {
            if (_entries.Count == 0) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

            int myTeam = LocalTeam();
            float y = 46f;
            Color prev = GUI.color;
            foreach (var e in _entries)
            {
                float age = Time.time - e.Time;
                float a = age > 4f ? Mathf.Clamp01(5f - age) : 1f;

                Color kc = ColorFor(e.KillerTeam, myTeam);
                Color vc = ColorFor(e.VictimTeam, myTeam);
                kc.a = vc.a = a;

                var r = new Rect(0, y, Screen.width - 16f, 20f);
                string txt = e.Killer != null ? $"{e.Killer}   ⇒   {e.Victim}" : $"☠  {e.Victim}";
                GUI.color = e.Killer != null ? Color.Lerp(kc, vc, 0.5f) : vc;
                GUI.Label(r, txt, _style);
                y += 20f;
            }
            GUI.color = prev;
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
