namespace Sweeper.Data
{
    /// <summary>
    /// 2D grid container holding all cell data.
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public class GridModel
    {
        private readonly CellData[,] cells;

        public int Width { get; }
        public int Height { get; }
        public int MineCount { get; set; }
        public int FlagCount { get; set; }
        public int RevealedCount { get; set; }
        public int InactiveCount { get; set; }

        public GridModel(int width, int height)
        {
            Width = width;
            Height = height;
            MineCount = 0;
            FlagCount = 0;
            RevealedCount = 0;

            cells = new CellData[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    cells[x, y] = new CellData();
                }
            }
        }

        public CellData GetCell(int x, int y)
        {
            if (!IsInBounds(x, y))
                return null;
            return cells[x, y];
        }

        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
    }
}
