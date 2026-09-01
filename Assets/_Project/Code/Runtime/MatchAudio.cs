using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Töne für den Rundenablauf (2D, für den lokalen Spieler):
    ///  - Rundenstart
    ///  - Rundensieg / Rundenniederlage (je nach eigenem Team)
    ///  - Ende der Kaufzeit
    ///  - Bombe gelegt / entschärft
    ///
    /// Sitzt auf dem HUD-Objekt der Arena. Hängt sich - wie der KillFeedHud -
    /// an den MatchManager, sobald es ihn gibt.
    /// </summary>
    public sealed class MatchAudio : MonoBehaviour
    {
        MatchManager _hooked;

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == _hooked) return;
            Unhook();
            _hooked = mm;
            if (_hooked != null)
            {
                _hooked.RoundStarted += OnRoundStarted;
                _hooked.RoundEnded += OnRoundEnded;
                _hooked.BuyTimeEnded += OnBuyTimeEnded;
                _hooked.BombEventReported += OnBombEvent;
            }
        }

        void OnDestroy() => Unhook();

        void Unhook()
        {
            if (_hooked == null) return;
            _hooked.RoundStarted -= OnRoundStarted;
            _hooked.RoundEnded -= OnRoundEnded;
            _hooked.BuyTimeEnded -= OnBuyTimeEnded;
            _hooked.BombEventReported -= OnBombEvent;
            _hooked = null;
        }

        void OnRoundStarted() => AudioService.Instance?.Play2D(SoundId.RundeStart, 0.55f);

        void OnRoundEnded(int winner)
        {
            int myTeam = LocalTeam();
            if (winner == Team.None || myTeam == Team.None) return;
            AudioService.Instance?.Play2D(
                winner == myTeam ? SoundId.RundeSieg : SoundId.RundeNiederlage, 0.6f);
        }

        void OnBuyTimeEnded() => AudioService.Instance?.Play2D(SoundId.KaufzeitVorbei, 0.4f);

        void OnBombEvent(int kind, ulong actorId)
        {
            // Explosion hat ihren eigenen 3D-Ton in BombExplosionFx.
            if (kind == (int)MatchManager.BombEvent.Gelegt)
                AudioService.Instance?.Play2D(SoundId.BombeGelegt, 0.6f);
            else if (kind == (int)MatchManager.BombEvent.Entschaerft)
                AudioService.Instance?.Play2D(SoundId.BombeEntschaerft, 0.6f);
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
