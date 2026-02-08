namespace Mines.Data
{
    /// <summary>
    /// The outcome of resolving a mine event.
    /// Returned by all Resolve methods in logic layer.
    /// </summary>
    public struct ResolutionResult
    {
        public int hpDelta;
        public RewardType reward;
        public int rewardValue;
        public string resultText;       // narrative feedback to display
        public bool playerDied;         // true if HP would reach 0

        // Combat-specific
        public CombatExchange[] exchanges;  // per-exchange breakdown (null for non-combat)
    }

    /// <summary>
    /// A single exchange in an auto-resolved combat.
    /// For UI display of the combat flow.
    /// </summary>
    public struct CombatExchange
    {
        public int exchangeNumber;
        public int playerDamageDealt;   // F (always the same)
        public int creatureHpAfter;     // Fc remaining after player attack
        public int creatureDamageDealt; // remaining Fc (0 if creature died)
    }
}
