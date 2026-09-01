using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Meldet einen besonderen Moment ("DOPPELKILL", "ACE!", "CLUTCH!") an den
    /// <see cref="HudController"/>, der ihn gross in der Bildmitte einblendet,
    /// spielt den Ton und schreibt Ace / Matchergebnis in die Laufbahn-Statistik
    /// (<see cref="CareerStats"/>). Haengt am Arena-HUD.
    /// </summary>
    public sealed class HighlightBanner : MonoBehaviour
    {
        MatchManager _hooked;

        /// <summary>Nur fuer Tests: der zuletzt gezeigte Banner-Text.</summary>
        public string LastBannerForTests { get; private set; }

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm != _hooked)
            {
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
        }

        void OnDestroy()
        {
            if (_hooked != null)
            {
                _hooked.HighlightReported -= OnHighlight;
                _hooked.MatchEnded -= OnMatchEnded;
            }
        }

        static ulong LocalPlayerId()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return 0;
            return nm.LocalClient.PlayerObject.NetworkObjectId;
        }

        static int LocalTeam()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return Team.None;
            var tm = nm.LocalClient.PlayerObject.GetComponent<TeamMember>();
            return tm != null ? tm.TeamId : Team.None;
        }

        void OnHighlight(int kindInt, ulong playerId)
        {
            var kind = (HighlightKind)kindInt;
            bool mine = playerId != 0 && playerId == LocalPlayerId();

            if (mine && kind == HighlightKind.Ace)
                CareerStats.RecordAce();

            // Eigene Momente immer; Ace/Clutch von anderen auch (mit Namen).
            bool show = mine || kind == HighlightKind.Ace || kind == HighlightKind.Clutch;
            if (!show) return;

            string who = mine ? "" : NameOf(playerId);
            string text = string.IsNullOrEmpty(who)
                ? HighlightTracker.Title(kind)
                : $"{who}: {HighlightTracker.Title(kind)}";
            LastBannerForTests = text;
            if (HudController.Instance != null) HudController.Instance.ShowBanner(text);

            if (AudioService.Instance != null)
            {
                var s = kind == HighlightKind.Ace || kind == HighlightKind.Clutch
                    ? SoundId.RundeSieg : SoundId.Abschuss;
                AudioService.Instance.Play2D(s, 0.9f);
            }
        }

        void OnMatchEnded(int winner)
        {
            int myTeam = LocalTeam();
            if (myTeam != Team.None)
                CareerStats.RecordMatch(winner == myTeam);
        }

        static string NameOf(ulong objectId)
        {
            foreach (var m in Combatants.Everyone)
                if (m != null && m.NetworkObject != null && m.NetworkObject.NetworkObjectId == objectId)
                    return m.DisplayName;
            return "Jemand";
        }
    }
}
