using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Saves and loads GameStateModel as JSON in Application.persistentDataPath.
/// </summary>
public static class SaveManager
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    /// <summary>
    /// Saves the model to disk.
    /// </summary>
    public static void Save(GameStateModel model)
    {
        string json = JsonConvert.SerializeObject(model, Formatting.Indented);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[SaveManager] Saved to {SavePath}");
    }

    /// <summary>
    /// Tries to load from disk. Returns null if no save file exists.
    /// </summary>
    public static GameStateModel Load()
    {
        if (!File.Exists(SavePath))
            return null;

        try
        {
            string json = File.ReadAllText(SavePath);
            var model = JsonConvert.DeserializeObject<GameStateModel>(json);
            Debug.Log($"[SaveManager] Loaded save from {SavePath}");
            return model;
        }
        catch (System.Exception ex)
        {
            Debug.Log($"[SaveManager] Failed to load save: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deletes the save file.
    /// </summary>
    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log($"[SaveManager] Save deleted.");
        }
    }
}
