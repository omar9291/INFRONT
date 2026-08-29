namespace Infront
{
    /// <summary>
    /// Alles, was Schaden nehmen kann: Spieler, Trainings-Dummy, spaeter Bots.
    /// Schaden wird immer nur auf dem Server zugefuegt.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>
        /// Fuegt Schaden zu. Nur auf dem Server aufrufen.
        /// </summary>
        /// <param name="amount">Schadenshoehe (positiv).</param>
        /// <param name="sourceClientId">Wer hat den Schaden verursacht.</param>
        void ApplyDamage(int amount, ulong sourceClientId);
    }
}
