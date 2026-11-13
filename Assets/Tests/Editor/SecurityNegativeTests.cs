using System.Collections;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Negative security tests: what the game does with bad input, a damaged save, no network and no Supabase settings.
/// They never contact the real Supabase project: the network test uses an address on this PC that refuses connections.
/// Saves are written only under test-only profile names in the editor's PlayerPrefs and are deleted afterwards;
/// the player's own saves, the active profile and the editor's legacy-migration flag are restored.
/// </summary>
public class SecurityNegativeTests
{
    const string LegacyMigratedKey = "scamsense_legacy_migrated";
    const string ProgressKeyPrefix = "scamsense_progress_v1_";
    const string PendingKeyPrefix = "scamsense_pending_results_";
    const string CorruptProfile = "securitytest-corrupt-save";
    const string NetworkProfile = "securitytest-network-failure";

    string previousProfile;
    bool hadMigratedFlag;
    int previousMigratedFlag;
    GameObject go;

    [SetUp]
    public void SetUp()
    {
        previousProfile = LocalDataManager.ProfileId;
        hadMigratedFlag = PlayerPrefs.HasKey(LegacyMigratedKey);
        previousMigratedFlag = PlayerPrefs.GetInt(LegacyMigratedKey, 0);
        PlayerPrefs.SetInt(LegacyMigratedKey, 1);   // keeps the real offline save out of the test profiles
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null) Object.DestroyImmediate(go);
        foreach (var p in new[] { CorruptProfile, NetworkProfile })
        {
            PlayerPrefs.DeleteKey(ProgressKeyPrefix + p);
            PlayerPrefs.DeleteKey(PendingKeyPrefix + p);
        }
        if (hadMigratedFlag) PlayerPrefs.SetInt(LegacyMigratedKey, previousMigratedFlag);
        else PlayerPrefs.DeleteKey(LegacyMigratedKey);
        PlayerPrefs.Save();
        LocalDataManager.SetProfile(previousProfile);
    }

    CloudAccountService NewService()
    {
        go = new GameObject("SecurityTestAccountService");
        return go.AddComponent<CloudAccountService>();   // Awake/OnEnable don't run in edit mode: no session, no sync
    }

    static object Private(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    // ---------- Invalid sign-up input ----------

    [Test]
    public void SignUpValidation_RejectsBadEmailShortPasswordAndAngleBracketName()
    {
        Assert.IsNotNull(CloudAccountService.ValidateEmail("player.example.com"), "bad email accepted");
        Assert.IsNotNull(CloudAccountService.ValidatePassword("Abc12!x"), "7-character password accepted");
        Assert.IsNotNull(CloudAccountService.ValidateDisplayName("Bad<Name"), "display name with < accepted");

        // The same rules accept good input, so the rejections above are about the bad values.
        Assert.IsNull(CloudAccountService.ValidateEmail("player@example.com"));
        Assert.IsNull(CloudAccountService.ValidatePassword("Abcdef1!"));
        Assert.IsNull(CloudAccountService.ValidateDisplayName("Investigator"));
    }

    [TestCase("player.example.com", "Abcdef1!", "Investigator")]
    [TestCase("player@example.com", "Abc12!x", "Investigator")]
    [TestCase("player@example.com", "Abcdef1!", "Bad<Name")]
    public void CreateAccount_WithInvalidInput_StopsBeforeAnyRequest(string email, string password, string name)
    {
        var svc = NewService();
        var task = svc.CreateAccountAsync(email, password, name);

        Assert.IsTrue(task.IsCompleted, "the check should finish without waiting for the network");
        Assert.IsFalse(task.Result);
        Assert.IsFalse(string.IsNullOrEmpty(svc.LastError), "the player should see why");
        Assert.IsNull(Private(svc, "client"), "a Supabase client was created, so a request could have been sent");
    }

    // ---------- Corrupt save ----------

    [TestCase("{this is not valid json")]
    [TestCase("\u0000\u0001 garbage ÿ")]
    public void CorruptSave_StartsWithEmptyProgressAndCanSaveAgain(string corrupt)
    {
        PlayerPrefs.SetString(ProgressKeyPrefix + CorruptProfile, corrupt);
        LogAssert.ignoreFailingMessages = true;   // the save reader logs a warning about the unreadable save
        try
        {
            Assert.DoesNotThrow(() => LocalDataManager.SetProfile(CorruptProfile));
            Assert.AreEqual(0, LocalDataManager.Levels.Count, "progress should start empty");
            Assert.IsFalse(LocalDataManager.IsUnlocked("level_2"), "nothing should be unlocked by a damaged save");

            // Play continues: a finished run is saved over the damaged data and reads back correctly.
            LocalDataManager.RecordResult("level_1", 300, 1, passed: false);
            LocalDataManager.SetProfile(null);
            LocalDataManager.SetProfile(CorruptProfile);
            Assert.AreEqual(300, LocalDataManager.Peek("level_1")?.bestScore);
        }
        finally { LogAssert.ignoreFailingMessages = false; }
    }

    // ---------- Network failure ----------

    [UnityTest]
    public IEnumerator NetworkFailureDuringSync_KeepsProgressAndQueuedRunForLater()
    {
        // A signed-in player whose server can't be reached (127.0.0.1:9 refuses connections).
        LocalDataManager.SetProfile(NetworkProfile);
        LocalDataManager.RecordResult("level_1", 300, 1, passed: false);
        GameResultLog.Add("level_1", 300, 425, 1, 3, 5, false, 40f, new System.Collections.Generic.List<GameResultLog.Answer>());

        var client = new SupabaseClient(new SupabaseSettings { url = "https://127.0.0.1:9", publishableKey = "sb_publishable_test" });
        typeof(SupabaseClient).GetProperty("Session").SetValue(client, new SupabaseSession
        {
            access_token = "test", refresh_token = "test", expires_at = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
            user = new SupabaseUser { id = NetworkProfile, is_anonymous = true }
        });
        var svc = NewService();
        typeof(CloudAccountService).GetField("client", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(svc, client);

        LogAssert.ignoreFailingMessages = true;   // the failed sync logs a warning
        var task = svc.SyncNowAsync();
        var clock = Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed.TotalSeconds < 40) yield return null;
        LogAssert.ignoreFailingMessages = false;

        Assert.IsTrue(task.IsCompleted, "sync never finished");
        Assert.IsFalse(task.IsFaulted, "the failure escaped as an exception instead of being handled");
        StringAssert.Contains("Can't reach the server", svc.LastSyncError ?? "", "the player should get the offline message");
        Assert.IsTrue(svc.SignedIn, "a network problem must not sign the player out");
        Assert.IsTrue(LocalDataManager.PendingSync, "local progress should still be marked for upload");
        Assert.AreEqual(300, LocalDataManager.Peek("level_1")?.bestScore, "local progress should be untouched");
        Assert.AreEqual(1, GameResultLog.PendingFor(NetworkProfile).Count, "the finished run should stay queued for the next try");
        Assert.AreEqual("SYNC PENDING", svc.StatusText);
    }

    // ---------- Missing settings file ----------

    /// <summary>
    /// Run with Assets/Resources/SupabaseSettings.json set aside (Explicit, so normal runs skip it):
    /// the game must stay playable offline and explain why accounts are unavailable.
    /// </summary>
    [Test, Explicit("Needs Assets/Resources/SupabaseSettings.json moved out of the project first")]
    public void MissingSettingsFile_AccountsOffAndExplained_OfflinePlayStillWorks()
    {
        var reset = typeof(SupabaseSettings).GetMethod("ResetCache", BindingFlags.NonPublic | BindingFlags.Static);
        reset.Invoke(null, null);
        try
        {
            Assert.IsNull(Resources.Load<TextAsset>(SupabaseSettings.ResourceName), "the settings file is still in the project");
            Assert.IsFalse(SupabaseSettings.Configured);

            var svc = NewService();
            Assert.IsFalse(svc.ServicesReady, "the account screen decides its message from this");
            var task = svc.PlayAsGuestAsync();
            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.Result);
            StringAssert.Contains("aren't set up", svc.LastError ?? "");

            LocalDataManager.SetProfile(CorruptProfile);   // offline save still works
            LocalDataManager.RecordResult("level_1", 300, 1, passed: false);
            Assert.AreEqual(300, LocalDataManager.Peek("level_1")?.bestScore);
        }
        finally { reset.Invoke(null, null); }
    }
}
