using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Supabase is the content source: this downloads the question bank teachers and admins manage
/// (public.get_content_bundle()) when the player signs in (guest or account) and again whenever a scene loads
/// (at most once per <see cref="MinSecondsBetweenChecks"/>), and keeps it as the cloud cache
/// (QuestionBankRepository.CloudCachePath). The next level that loads plays it; a level in progress never changes.
/// Offline, or when the server has no content yet, the game keeps using the last download or the bundled file -
/// nothing here can stop the game from starting.
/// </summary>
public class CloudContentSync : MonoBehaviour
{
    public const string RpcName = "get_content_bundle";
    public const float MinSecondsBetweenChecks = 60f;

    static CloudContentSync instance;
    /// <summary>True after at least one successful check this session.</summary>
    public static bool Synced { get; private set; }
    /// <summary>Why the last attempt didn't update the cache ("" after a successful download).</summary>
    public static string LastProblem { get; private set; } = "";

    bool subscribed, running, unavailable;
    float lastCheck = float.NegativeInfinity;

    void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => TrySync();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null || !SupabaseSettings.Configured) return;
        var go = new GameObject("CloudContentSync") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CloudContentSync>();
    }

    void Update()
    {
        // Wait for the account service to exist (the menu creates it), then follow its sign-in state.
        if (subscribed || !CloudAccountService.Exists) return;
        CloudAccountService.Instance.StateChanged += OnAccountChanged;
        subscribed = true;
        TrySync();
    }

    void OnDestroy()
    {
        if (subscribed && CloudAccountService.Exists) CloudAccountService.Instance.StateChanged -= OnAccountChanged;
    }

    // Signing in (or switching account) checks right away; otherwise scene loads check, throttled.
    void OnAccountChanged()
    {
        if (!Synced) lastCheck = float.NegativeInfinity;
        TrySync();
    }

    /// <summary>Downloads now, ignoring the once-a-minute limit (after a teacher saved a question).</summary>
    public static void RequestSync()
    {
        if (instance == null) return;
        instance.lastCheck = float.NegativeInfinity;
        instance.TrySync();
    }

    void TrySync()
    {
        if (running || unavailable || !CloudAccountService.Exists) return;
        if (Time.realtimeSinceStartup - lastCheck < MinSecondsBetweenChecks) return;
        var svc = CloudAccountService.Instance;
        if (!svc.SignedIn || svc.Client == null) return;
        lastCheck = Time.realtimeSinceStartup;
        _ = SyncAsync(svc.Client);
    }

    async Task SyncAsync(SupabaseClient client)
    {
        running = true;
        try
        {
            string json = await client.RpcAsync(RpcName);
            var bank = QuestionBankRepository.Parse(json, ContentSource.CloudCache, "supabase:" + RpcName);
            if (!bank.Usable)
            {
                LastProblem = "the server's content didn't pass the checks: " + string.Join("; ", bank.issues.FindAll(i => i.IsError && i.where.Length == 0));
                Debug.LogWarning("Cloud content: kept the current questions (" + LastProblem + ").");
                return;
            }
            if (bank.data.questions.Count == 0)
            {
                LastProblem = "the server has no questions yet";
                Debug.Log("Cloud content: " + LastProblem + "; using the bundled questions.");
                Synced = true;
                return;
            }
            var current = QuestionBankRepository.Current;
            bool same = current != null && current.source == ContentSource.CloudCache && current.data.contentVersion == bank.data.contentVersion;
            if (!same)
            {
                QuestionBankRepository.WriteCloudCache(JsonUtility.ToJson(bank.data));
                QuestionBankRepository.Reload();
            }
            Synced = true;
            LastProblem = "";
            Debug.Log($"Cloud content: version {bank.data.contentVersion} ({bank.data.questions.Count} questions){(same ? ", already up to date" : ", used from the next level")}.");
        }
        catch (SupabaseException e) when (e.Status == 404 || e.Code == "PGRST202")
        {
            unavailable = true;   // the server doesn't have the question bank migration: don't ask again this session
            LastProblem = "the server has no question bank";
            Debug.Log("Cloud content: " + LastProblem + " (apply the question bank migration to enable it).");
        }
        catch (Exception e)
        {
            LastProblem = e.Message;
            Debug.Log("Cloud content: not updated (" + e.Message + "); using the cached or bundled questions.");
        }
        finally { running = false; }
    }
}
