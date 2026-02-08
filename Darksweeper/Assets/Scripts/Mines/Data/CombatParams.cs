namespace Mines.Data
{
    /// <summary>
    /// Configuration for a Combat mine event.
    /// creatureForce = the creature's Force (and initial HP).
    /// Combat is deterministic: player attacks first with playerForce,
    /// creature retaliates with remaining force until dead.
    /// </summary>
    public class CombatParams
    {
        public string monsterName;
        public int creatureForce;   // Fc — creature's force = its HP
        public bool isElite;
        public RewardType reward;
        public int rewardValue;
    }
}
