using System;
using System.IO;
using System.Text.Json;

namespace FpsRange.Core;

/// <summary>Persists the NPC Battle high score (kill count) to a small JSON file next to the exe.</summary>
public class HighScoreStore
{
    private class SaveData { public int BestKills { get; set; } }

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "Save", "npcbattle_highscore.json");

    /// <summary>0 if the file is missing/corrupt - same defensive-load spirit as the HUD's SpriteFont load.</summary>
    public int TryGetBest()
    {
        try
        {
            if (!File.Exists(FilePath)) return 0;
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath));
            return data?.BestKills ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public void SaveIfBetter(int kills)
    {
        try
        {
            if (kills <= TryGetBest()) return;
            string dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new SaveData { BestKills = kills }));
        }
        catch
        {
            // Best-effort persistence - a failed save shouldn't crash the game.
        }
    }
}
