using System.Collections.Generic;

/// <summary>
/// Pure C# runtime model of the game state.
/// No Unity dependencies. This is the single source of truth at runtime.
/// </summary>
public class GameStateModel
{
    public int Hp;
    public int Energy;
    public string Objective;
    public List<string> Flags;

    public GameStateModel()
    {
        Hp = 100;
        Energy = 50;
        Objective = "Explore the area";
        Flags = new List<string>();
    }

    public override string ToString()
    {
        return $"HP: {Hp} | Energy: {Energy} | Objective: {Objective} | Flags: [{string.Join(", ", Flags)}]";
    }
}
