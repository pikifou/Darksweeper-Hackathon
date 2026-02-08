namespace Mines.Data
{
    /// <summary>
    /// Configuration for a Chest mine event.
    /// A chest is never a no-brainer: it may be trapped.
    /// </summary>
    public class ChestParams
    {
        public string description;
        public bool isTrapped;
        public int trapDamage;      // HP lost if trapped and opened
        public RewardType reward;
        public int rewardValue;
    }
}
