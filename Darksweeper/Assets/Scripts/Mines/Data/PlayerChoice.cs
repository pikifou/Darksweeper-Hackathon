namespace Mines.Data
{
    /// <summary>
    /// All possible player actions across event types.
    /// Unified enum so the RunLog can store any choice.
    /// </summary>
    public enum PlayerChoice
    {
        // Combat
        Engage,

        // Chest
        Open,
        Ignore,

        // Dialogue
        Help,
        Harm,
        // Ignore is shared with Chest

        // Shrine
        Sacrifice,
        Refuse
    }
}
