using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Alles, was Schaden nehmen kann: Spieler, Trainings-Dummy, Bots.
    /// Schaden wird immer nur auf dem Server zugefuegt.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>
        /// Fuegt Schaden zu. Nur auf dem Server aufrufen.
        /// </summary>
        /// <param name="amount">Schadenshoehe (positiv).</param>
        /// <param name="sourceClientId">Wer hat den Schaden verursacht (Client-Id).</param>
        void ApplyDamage(int amount, ulong sourceClientId);

        /// <summary>
        /// Fuegt Schaden zu und merkt sich den Verursacher als GameObject
        /// (fuer die Abschuss-Wertung). Nur auf dem Server aufrufen.
        /// </summary>
        void ApplyDamage(int amount, GameObject instigator);
    }
}
