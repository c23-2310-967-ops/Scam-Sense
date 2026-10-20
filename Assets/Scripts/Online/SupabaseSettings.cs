using System;
using UnityEngine;

/// <summary>
/// Supabase connection settings, read from Assets/Resources/SupabaseSettings.json (git-ignored):
///   { "url": "https://YOUR-PROJECT.supabase.co", "publishableKey": "sb_publishable_..." }
/// Only the project URL and the public (publishable / anon) key belong here. They are meant to ship in the
/// game; Row Level Security protects the data. Never put the service_role / secret key or the database
/// password in this file or anywhere in the Unity project.
/// Without the file the game still runs: accounts are unavailable and progress stays on this PC.
/// </summary>
[Serializable]
public class SupabaseSettings
{
    public const string ResourceName = "SupabaseSettings";

    public string url = "";
    public string publishableKey = "";

    static SupabaseSettings cached;
    static bool loaded;

    /// <summary>The settings, or null when the file is missing or incomplete.</summary>
    public static SupabaseSettings Current
    {
        get
        {
            if (loaded) return cached;
            loaded = true;
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return null;
            try
            {
                var s = JsonUtility.FromJson<SupabaseSettings>(asset.text);
                string problem = "file unreadable";   // stays this when s is null (IsValid isn't called then)
                if (s == null || !s.IsValid(out problem))
                {
                    Debug.LogWarning("SupabaseSettings: " + problem + " - online accounts are off.");
                    return null;
                }
                s.url = s.url.Trim().TrimEnd('/');
                s.publishableKey = s.publishableKey.Trim();
                cached = s;
            }
            catch (Exception e) { Debug.LogWarning("SupabaseSettings: file unreadable - online accounts are off. " + e.Message); }
            return cached;
        }
    }

    public static bool Configured => Current != null;

    bool IsValid(out string problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(url) || !url.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            problem = "\"url\" must be your project's https://...supabase.co address";
        else if (string.IsNullOrWhiteSpace(publishableKey) || publishableKey.Contains("YOUR"))
            problem = "\"publishableKey\" is missing";
        else if (publishableKey.StartsWith("sb_secret_", StringComparison.Ordinal) || LooksLikeServiceRole(publishableKey))
            problem = "that is a SECRET / service_role key. Use the publishable (anon) key - secret keys must never be in the game";
        return problem == null;
    }

    /// <summary>Legacy JWT keys carry their role in the payload; refuse a service_role key.</summary>
    static bool LooksLikeServiceRole(string key)
    {
        var parts = key.Split('.');
        if (parts.Length != 3) return false;
        try
        {
            string p = parts[1].Replace('-', '+').Replace('_', '/');
            p = p.PadRight(p.Length + (4 - p.Length % 4) % 4, '=');
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p)).Contains("service_role");
        }
        catch { return false; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() { cached = null; loaded = false; }
}
