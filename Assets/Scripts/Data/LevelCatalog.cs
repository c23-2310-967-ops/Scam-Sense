using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rooms/cases in play order. The main menu reads it for Play, Continue and Level Selection;
/// unlocking itself stays in LocalDataManager / GameManager (score-based), so there is one source of truth.
/// Add a new room by creating its LevelDefinition and appending it here.
/// </summary>
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Scam Sense/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public List<LevelDefinition> levels = new List<LevelDefinition>();

    public int Count => levels != null ? levels.Count : 0;

    /// <summary>The first room is always playable; later rooms need their saved unlock.</summary>
    public bool IsUnlocked(int index)
    {
        if (index < 0 || index >= Count || levels[index] == null) return false;
        return index == 0 || LocalDataManager.IsUnlocked(levels[index].levelId);
    }

    public static bool SceneAvailable(LevelDefinition level) =>
        level != null && !string.IsNullOrEmpty(level.sceneName) && Application.CanStreamedLevelBeLoaded(level.sceneName);

    /// <summary>True if the player has finished at least one room (real saved progress).</summary>
    public bool HasProgress()
    {
        foreach (var l in levels)
        {
            var p = l != null ? LocalDataManager.Peek(l.levelId) : null;
            if (p != null && p.completed) return true;
        }
        return false;
    }

    /// <summary>Furthest unlocked room whose scene can be loaded (what Continue opens).</summary>
    public int ContinueIndex()
    {
        for (int i = Count - 1; i >= 0; i--)
            if (IsUnlocked(i) && SceneAvailable(levels[i])) return i;
        return 0;
    }
}
