namespace Mines.Data
{
    /// <summary>
    /// Flat log entry for the PlayerProfile / narrative system.
    /// One per resolved mine event.
    /// </summary>
    public class RunEvent
    {
        public MineEventType eventType;
        public string eventId;
        public int tileX, tileY;
        public string levelId;
        public int hpBefore;
        public int hpAfter;
        public PlayerChoice choice;
        public RewardType reward;
        public int eventIndex;          // sequential counter in the run
        public bool wasLeftClickPenalty; // true if triggered by accidental left-click
    }
}
