namespace Sweeper.Data
{
    /// <summary>
    /// Per-cell state for the DarkSweeper grid.
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public class CellData
    {
        public bool hasMine;
        public int adjacentMines;
        public bool isRevealed;
        public bool isFlagged;
        public float light;
        public bool isActive;

        public CellData()
        {
            hasMine = false;
            adjacentMines = 0;
            isRevealed = false;
            isFlagged = false;
            light = 0f;
            isActive = true;
        }
    }
}
