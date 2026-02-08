using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Data Transfer Object matching the JSON contract exactly.
/// Used only for deserialization — never passed around as game state.
/// </summary>
public class GameStateDTO
{
    [JsonProperty("type")]
    public string Type;

    [JsonProperty("schema_version")]
    public string SchemaVersion;

    [JsonProperty("game_state")]
    public GameStateData GameState;

    // Error fields (present when type == "error")
    [JsonProperty("message")]
    public string Message;

    [JsonProperty("code")]
    public string Code;

    public class GameStateData
    {
        [JsonProperty("hp")]
        public int Hp;

        [JsonProperty("energy")]
        public int Energy;

        [JsonProperty("objective")]
        public string Objective;

        [JsonProperty("flags")]
        public List<string> Flags;
    }
}
