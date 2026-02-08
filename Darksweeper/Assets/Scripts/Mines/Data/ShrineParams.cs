namespace Mines.Data
{
    /// <summary>
    /// Configuration for a Shrine mine event.
    /// A sacrifice: "your values cost something."
    /// </summary>
    public class ShrineParams
    {
        public string shrineId;
        public string description;      // what the shrine offers
        public int sacrificeCost;       // HP lost if sacrifice is accepted
        public RewardType reward;
        public int rewardValue;
    }
}
