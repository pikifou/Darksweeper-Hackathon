namespace Mines.Data
{
    /// <summary>
    /// What the UI panel receives to render an interaction.
    /// No game logic leaks into presentation — the panel is "dumb."
    /// </summary>
    public struct InteractionDescriptor
    {
        public MineEventType eventType;
        public string title;
        public string description;
        public ChoiceOption[] choices;
        public bool isResolved;

        // Combat-specific display info
        public string creatureName;
        public int creatureForce;
        public bool isElite;
    }

    /// <summary>
    /// A single choice button for the UI panel.
    /// </summary>
    public struct ChoiceOption
    {
        public PlayerChoice choice;
        public string label;
        public string riskHint;     // e.g. "dangerous", "uncertain", ""
    }
}
