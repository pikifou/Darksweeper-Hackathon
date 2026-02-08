using UnityEngine.Video;

namespace Mines.Data
{
    /// <summary>
    /// Per-mine-cell event payload.
    /// Created at level init, one per mine cell.
    /// Stored in MineEventController's dictionary keyed by (x, y).
    /// </summary>
    public class MineEventData
    {
        // Identity
        public int x, y;                            // grid coordinates
        public MineEventType eventType;
        public MineState state = MineState.Hidden;

        // Config (set at generation — only one is non-null)
        public string eventId;                      // unique id for logging
        public CombatParams combatParams;
        public ChestParams chestParams;
        public DialogueParams dialogueParams;
        public ShrineParams shrineParams;

        // Visuals
        public VideoClip videoClip;                 // optional — played in toast (right-click)
        public VideoClip penaltyVideoClip;          // optional — played in toast (left-click penalty)

        // Resolution (set after interaction)
        public PlayerChoice? choiceMade;
        public int hpDelta;
        public RewardType rewardGiven;
    }
}
