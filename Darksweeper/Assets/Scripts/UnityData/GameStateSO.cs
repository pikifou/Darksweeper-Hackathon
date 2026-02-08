using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject holding the game state.
/// Used as default/initial values and as runtime in-memory state.
/// </summary>
[CreateAssetMenu(fileName = "GameState_Runtime", menuName = "LLM Demo/Game State")]
public class GameStateSO : ScriptableObject
{
    public int hp = 100;
    public int energy = 50;
    public string objective = "Explore the area";
    public List<string> flags = new List<string>();
}
