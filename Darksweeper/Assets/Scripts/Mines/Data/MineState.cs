namespace Mines.Data
{
    /// <summary>
    /// Per-mine lifecycle state.
    /// Hidden: cell not yet lit by the Sweeper.
    /// Revealed: cell is lit, player can see there's a mine, awaiting interaction.
    /// Resolved: interaction completed — cannot be replayed.
    /// </summary>
    public enum MineState
    {
        Hidden,
        Revealed,
        Resolved
    }
}
