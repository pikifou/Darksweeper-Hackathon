namespace Mines.Data
{
    /// <summary>
    /// Types of rewards that mine events can grant.
    /// Limited to 3 for hackathon scope.
    /// </summary>
    public enum RewardType
    {
        None,
        HpGain,       // immediate +PV
        VisionGain,   // increase light radius
        Buff          // reduce damage for next N combats
    }
}
