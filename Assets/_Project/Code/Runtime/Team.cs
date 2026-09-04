namespace Infront
{
    /// <summary>
    /// Team-Zugehoerigkeit. Bewusst eine einfache Zahl (kein enum), damit sie
    /// problemlos in einer NetworkVariable liegt.
    /// </summary>
    public static class Team
    {
        public const int None = 0;
        public const int Alpha = 1;
        public const int Bravo = 2;

        public static int Opponent(int team) => team switch
        {
            Alpha => Bravo,
            Bravo => Alpha,
            _ => None,
        };

        public static string Name(int team) => team switch
        {
            Alpha => "Team Alpha",
            Bravo => "Team Bravo",
            _ => "No team",
        };

        /// <summary>Zwei Kaempfer sind Verbuendete, wenn sie dasselbe echte Team haben.</summary>
        public static bool AreFriendly(int a, int b) => a != None && a == b;
    }
}
