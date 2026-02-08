namespace Sweeper.Data
{
    /// <summary>
    /// Tags for level design — what role each cell plays.
    /// Stored in LevelDataSO for the editor painter tool.
    ///
    /// Keyboard shortcuts in the Painter map to these values (1-based):
    ///   1=Empty, 2=Entry, 3=Inactive, 4=Safe, 5=Mine, 6=Combat, 7=Chest, 8=Dialogue, 9=Shrine
    ///
    /// Safe = playable cell where mines/encounters can NEVER be placed (e.g. around Entry).
    /// Mine = generic encounter (random type at runtime).
    /// Combat/Chest/Dialogue/Shrine = forced encounter types.
    /// </summary>
    public enum CellTag
    {
        Empty = 0,
        Entry = 1,
        Inactive = 2,
        Safe = 3,
        Mine = 4,
        Combat = 5,
        Chest = 6,
        Dialogue = 7,
        Shrine = 8
    }

    /// <summary>
    /// Extension methods for CellTag to classify mine/encounter types.
    /// </summary>
    public static class CellTagExtensions
    {
        /// <summary>True if the tag places a mine cell (generic or specific encounter type).</summary>
        public static bool IsMineOrEncounter(this CellTag tag)
            => tag == CellTag.Mine || tag == CellTag.Combat || tag == CellTag.Chest
            || tag == CellTag.Dialogue || tag == CellTag.Shrine;

        /// <summary>True if the tag forces a specific encounter type (not generic Mine).</summary>
        public static bool IsSpecificEncounter(this CellTag tag)
            => tag == CellTag.Combat || tag == CellTag.Chest
            || tag == CellTag.Dialogue || tag == CellTag.Shrine;

        /// <summary>True if the cell is protected from mine placement (Safe or Entry).</summary>
        public static bool IsProtectedFromMines(this CellTag tag)
            => tag == CellTag.Safe || tag == CellTag.Entry;
    }
}
