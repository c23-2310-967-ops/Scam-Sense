using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The single place that decides whether the player can move/look and whether the cursor is free.
///
/// Any UI that needs the mouse (briefing, message, computer, pause menu, results) calls
/// Acquire(itself) and later Release(itself). The player is frozen while at least one owner holds
/// the gate, and unfrozen only when the last owner releases - so the pause menu opening over the
/// computer and then closing does not hand control back too early.
///
/// It also arbitrates the E key: the first script to use E in a frame calls ConsumeInteract(),
/// and every other E handler checks InteractBlocked first.
///
/// Esc handling: panels that close with Esc register a handler; PauseMenu asks them first.
/// </summary>
public static class InteractionGate
{
    // Player scripts (matched by class name) frozen while the gate is held.
    static readonly string[] LockedScriptNames =
        { "PlayerMotor", "InputManager", "MouseLook", "SeatedHeadLook", "PlayerInteractor" };

    static readonly HashSet<object> owners = new HashSet<object>();
    static readonly List<Behaviour> lockedBehaviours = new List<Behaviour>();
    static readonly List<KeyValuePair<object, Func<bool>>> escapeHandlers = new List<KeyValuePair<object, Func<bool>>>();
    static int consumedFrame = -1;

    /// <summary>True while any UI panel holds the player.</summary>
    public static bool UIOpen => owners.Count > 0;

    /// <summary>True if E has already been used this frame, or a panel is open.</summary>
    public static bool InteractBlocked => UIOpen || consumedFrame == Time.frameCount;

    public static bool IsHeldBy(object owner) => owner != null && owners.Contains(owner);

    public static void ConsumeInteract() => consumedFrame = Time.frameCount;

    public static void Acquire(object owner)
    {
        if (owner == null || !owners.Add(owner)) return;
        if (owners.Count == 1) FreezePlayer();
        SetCursorFree(true);
    }

    public static void Release(object owner)
    {
        if (owner == null || !owners.Remove(owner)) return;
        if (owners.Count == 0)
        {
            UnfreezePlayer();
            SetCursorFree(false);
        }
    }

    // ---------- Esc routing ----------

    /// <summary>Registers "close me on Esc". The handler returns true if it handled the key.</summary>
    public static void PushEscapeHandler(object owner, Func<bool> handler)
    {
        RemoveEscapeHandler(owner);
        escapeHandlers.Add(new KeyValuePair<object, Func<bool>>(owner, handler));
    }

    public static void RemoveEscapeHandler(object owner) => escapeHandlers.RemoveAll(p => p.Key == owner);

    /// <summary>Gives the most recent panel a chance to handle Esc. Returns true if one did.</summary>
    public static bool TryHandleEscape()
    {
        for (int i = escapeHandlers.Count - 1; i >= 0; i--)
        {
            var h = escapeHandlers[i];
            if (h.Key is UnityEngine.Object uo && uo == null) { escapeHandlers.RemoveAt(i); continue; }
            if (h.Value != null && h.Value()) return true;
        }
        return false;
    }

    // ---------- Internals ----------

    static void FreezePlayer()
    {
        lockedBehaviours.Clear();
        GameObject player = FindPlayer();
        if (player == null) return;

        foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
        {
            if (b == null || !b.enabled) continue;
            string typeName = b.GetType().Name;
            foreach (var wanted in LockedScriptNames)
            {
                if (typeName == wanted)
                {
                    b.enabled = false;
                    lockedBehaviours.Add(b);
                    break;
                }
            }
        }
    }

    static void UnfreezePlayer()
    {
        // Only re-enable what we switched off (e.g. movement stays off while seated in a chair).
        foreach (var b in lockedBehaviours)
            if (b != null) b.enabled = true;
        lockedBehaviours.Clear();
    }

    static void SetCursorFree(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    static GameObject FindPlayer()
    {
        GameObject tagged = null;
        try { tagged = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }
        if (tagged != null) return tagged;
        var cc = UnityEngine.Object.FindFirstObjectByType<CharacterController>();
        return cc != null ? cc.gameObject : null;
    }

    /// <summary>Forgets every owner without touching scripts (used when the scene is being left).</summary>
    public static void ResetAll()
    {
        owners.Clear();
        lockedBehaviours.Clear();
        escapeHandlers.Clear();
        consumedFrame = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        ResetAll();
        // Never carry a frozen/paused state into the next scene.
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    static void OnSceneUnloaded(Scene s)
    {
        // Drop owners that belonged to the unloaded scene (or are destroyed). Owners from a
        // newly loaded scene are kept, whatever order Unity runs unload/Start in.
        owners.RemoveWhere(o => o is UnityEngine.Object uo &&
                                (uo == null || (uo is Component c && c.gameObject.scene == s)));
        lockedBehaviours.RemoveAll(b => b == null);
        escapeHandlers.RemoveAll(p => p.Key is UnityEngine.Object uo && uo == null);
        if (owners.Count == 0) Time.timeScale = 1f;
    }
}
