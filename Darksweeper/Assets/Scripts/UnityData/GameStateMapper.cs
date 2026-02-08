using System.Collections.Generic;

/// <summary>
/// Maps between GameStateSO (Unity asset) and GameStateModel (pure C# runtime).
/// This is the only code that reads/writes SO fields.
/// </summary>
public static class GameStateMapper
{
    /// <summary>
    /// Reads a ScriptableObject and produces a runtime model.
    /// </summary>
    public static GameStateModel FromSO(GameStateSO so)
    {
        return new GameStateModel
        {
            Hp = so.hp,
            Energy = so.energy,
            Objective = so.objective,
            Flags = new List<string>(so.flags)
        };
    }

    /// <summary>
    /// Writes a runtime model back into a ScriptableObject (in-memory only).
    /// </summary>
    public static void ApplyToSO(GameStateModel model, GameStateSO so)
    {
        so.hp = model.Hp;
        so.energy = model.Energy;
        so.objective = model.Objective;
        so.flags = new List<string>(model.Flags);
    }
}
