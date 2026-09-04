using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Laufbahn-Statistik des lokalen Spielers, dauerhaft in PlayerPrefs:
    /// Matches, Siege, Aces, laengste Siegesserie. Beim Menuestart sichtbar.
    ///
    /// Bewusst klein gehalten - kein Konto, keine Cloud, nur dieser Rechner.
    /// </summary>
    public static class CareerStats
    {
        const string KeyMatches = "infront.career.matches";
        const string KeyWins    = "infront.career.wins";
        const string KeyAces    = "infront.career.aces";
        const string KeyStreak  = "infront.career.streak";     // aktuelle Serie
        const string KeyBest    = "infront.career.beststreak";

        public static int Matches => PlayerPrefs.GetInt(KeyMatches, 0);
        public static int Wins    => PlayerPrefs.GetInt(KeyWins, 0);
        public static int Aces    => PlayerPrefs.GetInt(KeyAces, 0);
        public static int Streak  => PlayerPrefs.GetInt(KeyStreak, 0);
        public static int BestStreak => PlayerPrefs.GetInt(KeyBest, 0);

        public static void RecordAce()
        {
            PlayerPrefs.SetInt(KeyAces, Aces + 1);
            PlayerPrefs.Save();
        }

        public static void RecordMatch(bool won)
        {
            PlayerPrefs.SetInt(KeyMatches, Matches + 1);
            if (won)
            {
                PlayerPrefs.SetInt(KeyWins, Wins + 1);
                int s = Streak + 1;
                PlayerPrefs.SetInt(KeyStreak, s);
                if (s > BestStreak)
                {
                    PlayerPrefs.SetInt(KeyBest, s);
                    // Eine Bestleistung, die niemand mitbekommt, ist keine.
                    if (s > 1) Meldungen.Zeige($"Neue Bestleistung: {s} Siege in Folge",
                                               Meldungen.Art.Gut);
                }
            }
            else
            {
                PlayerPrefs.SetInt(KeyStreak, 0);
            }
            PlayerPrefs.Save();
        }

        /// <summary>Nur fuer Tests: alles auf 0.</summary>
        /// <summary>
        /// Alle Laufbahn-Werte loeschen. Wird sowohl von der Kontoloeschung im
        /// Spiel benutzt (siehe PlayerProfile.DeleteEverything) als auch von
        /// den Tests - deshalb hier ein Name, der beides abdeckt.
        /// </summary>
        public static void DeleteAll() => ResetForTests();

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(KeyMatches);
            PlayerPrefs.DeleteKey(KeyWins);
            PlayerPrefs.DeleteKey(KeyAces);
            PlayerPrefs.DeleteKey(KeyStreak);
            PlayerPrefs.DeleteKey(KeyBest);
            PlayerPrefs.Save();
        }
    }
}
