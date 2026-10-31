using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Why a Supabase request failed. <see cref="IsNetwork"/> = couldn't reach the server (play offline).</summary>
public class SupabaseException : Exception
{
    public readonly long Status;
    public readonly string Code;
    public readonly bool IsNetwork;
    public SupabaseException(string message, long status = 0, string code = null, bool isNetwork = false) : base(message)
    {
        Status = status;
        Code = code;
        IsNetwork = isNetwork;
    }
    public bool IsAuth => Status == 401 || Status == 403 || Code == "bad_jwt" || Code == "session_not_found" || Code == "refresh_token_not_found";
}

/// <summary>A signed-in Supabase Auth session (account or anonymous guest).</summary>
[Serializable]
public class SupabaseSession
{
    public string access_token;
    public string refresh_token;
    public long expires_at;            // unix seconds
    public SupabaseUser user;

    public bool HasTokens => !string.IsNullOrEmpty(access_token) && !string.IsNullOrEmpty(refresh_token) && user != null && !string.IsNullOrEmpty(user.id);
    public bool ExpiresSoon => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires_at - 60;
}

[Serializable]
public class SupabaseUser
{
    public string id;
    public string email;
    public string new_email;           // set while an email change waits for confirmation
    public bool is_anonymous;
    public SupabaseUserMetadata user_metadata;
}

[Serializable]
public class SupabaseUserMetadata
{
    public string display_name;
}

/// <summary>
/// Minimal Supabase client over the official HTTP APIs (Auth = GoTrue /auth/v1, Database = PostgREST /rest/v1),
/// using UnityWebRequest only - no extra packages, works in the Editor and Windows builds.
/// Requests are awaited on Unity's main thread without blocking it, and time out after <see cref="TimeoutSeconds"/>.
/// Two different credentials are involved:
///   - The publishable (anon) key goes in the "apikey" header of every request. It only identifies the project;
///     on its own it runs as the Postgres role "anon", which can't read or write any game table.
///   - The player's access token goes in "Authorization: Bearer". It comes from signing in - including PLAY AS
///     GUEST, which is a Supabase anonymous user: a real user with role "authenticated" and the claim
///     is_anonymous = true. Database calls always use this token, so Row Level Security applies to that player
///     (see supabase/migrations/20261030231844_003_anonymous_user_hardening.sql for what guests may do).
/// The key alone is only sent as the bearer to the Auth endpoints that create or restore a session.
/// </summary>
public class SupabaseClient
{
    public const int TimeoutSeconds = 15;

    readonly string url, key;
    public SupabaseSession Session { get; private set; }
    public bool SignedIn => Session != null && Session.HasTokens;
    public string UserId => SignedIn ? Session.user.id : null;

    /// <summary>Raised when the session's tokens change (sign in, refresh, sign out) so they can be stored.</summary>
    public event Action<SupabaseSession> SessionChanged;

    public SupabaseClient(SupabaseSettings settings)
    {
        url = settings.url;
        key = settings.publishableKey;
    }

    // ---------- Auth ----------

    [Serializable] class Credentials { public string email; public string password; public MetaWrap data; }
    [Serializable] class MetaWrap { public string display_name; }
    [Serializable] class RefreshBody { public string refresh_token; }
    [Serializable] class UpdateUserBody { public string email; public string password; public MetaWrap data; }

    /// <summary>Email + password sign up. Returns false when the project requires email confirmation first (no session yet).</summary>
    public async Task<bool> SignUpAsync(string email, string password, string displayName)
    {
        var body = JsonUtility.ToJson(new Credentials { email = email, password = password, data = new MetaWrap { display_name = displayName } });
        string json = await SendAsync("POST", "/auth/v1/signup", body, auth: false);
        var s = JsonUtility.FromJson<SupabaseSession>(json);
        if (s == null || string.IsNullOrEmpty(s.access_token)) return false;   // confirmation email sent
        SetSession(s);
        return true;
    }

    public async Task SignInWithPasswordAsync(string email, string password)
    {
        var body = JsonUtility.ToJson(new Credentials { email = email, password = password });
        SetSession(JsonUtility.FromJson<SupabaseSession>(await SendAsync("POST", "/auth/v1/token?grant_type=password", body, auth: false)));
    }

    /// <summary>
    /// Guest: creates an anonymous Supabase user (Authentication > Sign In / Providers > Allow anonymous sign-ins)
    /// and signs in as it. The result is an authenticated identity of its own, not the public key.
    /// </summary>
    public async Task SignInAnonymouslyAsync()
    {
        var s = JsonUtility.FromJson<SupabaseSession>(await SendAsync("POST", "/auth/v1/signup", "{\"data\":{}}", auth: false));
        if (s != null && s.user != null && !s.user.is_anonymous)
            throw new SupabaseException("The server didn't return a guest session.");
        SetSession(s);
    }

    /// <summary>Restores a stored session with its refresh token (also used before the access token expires).</summary>
    public async Task RefreshAsync(string refreshToken)
    {
        var body = JsonUtility.ToJson(new RefreshBody { refresh_token = refreshToken });
        SetSession(JsonUtility.FromJson<SupabaseSession>(await SendAsync("POST", "/auth/v1/token?grant_type=refresh_token", body, auth: false)));
    }

    /// <summary>Adds email + password to the signed-in user (guest -> account; same user id and data).</summary>
    public async Task<SupabaseUser> UpdateUserAsync(string email, string password, string displayName)
    {
        var body = JsonUtility.ToJson(new UpdateUserBody { email = email, password = password, data = new MetaWrap { display_name = displayName } });
        var user = JsonUtility.FromJson<SupabaseUser>(await SendAsync("PUT", "/auth/v1/user", body));
        if (user != null && !string.IsNullOrEmpty(user.id)) { Session.user = user; SessionChanged?.Invoke(Session); }
        return user;
    }

    /// <summary>Ends the session on the server (best effort) and forgets it locally.</summary>
    public async Task SignOutAsync()
    {
        if (SignedIn)
        {
            try { await SendAsync("POST", "/auth/v1/logout?scope=local", "{}"); }
            catch (Exception e) { Debug.Log("SupabaseClient: server sign-out skipped (" + e.Message + ")."); }
        }
        ForgetSession();
    }

    /// <summary>Forgets the local session without contacting the server (e.g. keeping a guest's tokens for later).</summary>
    public void ForgetSession()
    {
        Session = null;
        SessionChanged?.Invoke(null);
    }

    void SetSession(SupabaseSession s)
    {
        if (s == null || !s.HasTokens) throw new SupabaseException("The server didn't return a session.");
        if (s.expires_at <= 0) s.expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
        Session = s;
        SessionChanged?.Invoke(s);
    }

    async Task EnsureFreshAsync()
    {
        if (!SignedIn) throw new SupabaseException("Not signed in.", 401, "session_not_found");
        if (Session.ExpiresSoon) await RefreshAsync(Session.refresh_token);
    }

    // ---------- Database (PostgREST) ----------

    /// <summary>GET /rest/v1/{pathAndQuery}. Returns the JSON array text.</summary>
    public Task<string> SelectAsync(string pathAndQuery) => SendAsync("GET", "/rest/v1/" + pathAndQuery, null);

    /// <summary>Insert or update rows (JSON array) by primary key / <paramref name="onConflict"/>.</summary>
    public Task UpsertAsync(string table, string jsonArray, string onConflict, bool ignoreDuplicates = false) =>
        SendAsync("POST", $"/rest/v1/{table}?on_conflict={onConflict}", jsonArray,
                  prefer: (ignoreDuplicates ? "resolution=ignore-duplicates" : "resolution=merge-duplicates") + ",return=minimal");

    public Task UpdateAsync(string pathAndQuery, string jsonObject) =>
        SendAsync("PATCH", "/rest/v1/" + pathAndQuery, jsonObject, prefer: "return=minimal");

    public Task DeleteAsync(string pathAndQuery) => SendAsync("DELETE", "/rest/v1/" + pathAndQuery, null, prefer: "return=minimal");

    /// <summary>Reads a JSON array into a list (JsonUtility needs an object at the top level).</summary>
    public static List<T> ParseArray<T>(string jsonArray)
    {
        if (string.IsNullOrWhiteSpace(jsonArray)) return new List<T>();
        var w = JsonUtility.FromJson<Wrapper<T>>("{\"items\":" + jsonArray + "}");
        return w?.items ?? new List<T>();
    }

    public static string ToJsonArray<T>(List<T> items)
    {
        string wrapped = JsonUtility.ToJson(new Wrapper<T> { items = items });
        int start = wrapped.IndexOf('[');
        return start < 0 ? "[]" : wrapped.Substring(start, wrapped.LastIndexOf(']') - start + 1);
    }

    [Serializable] class Wrapper<T> { public List<T> items; }

    // ---------- Transport ----------

    static string Field(string json, string name)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + name + "\"\\s*:\\s*(\"((?:[^\"\\\\]|\\\\.)*)\"|(-?\\d+))");
        return !m.Success ? null : m.Groups[2].Success ? System.Text.RegularExpressions.Regex.Unescape(m.Groups[2].Value) : m.Groups[3].Value;
    }

    async Task<string> SendAsync(string method, string path, string body, bool auth = true, string prefer = null)
    {
        if (auth) await EnsureFreshAsync();

        using (var req = new UnityWebRequest(url + path, method))
        {
            req.timeout = TimeoutSeconds;
            req.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.SetRequestHeader("apikey", key);
            req.SetRequestHeader("Authorization", "Bearer " + (auth ? Session.access_token : key));
            if (prefer != null) req.SetRequestHeader("Prefer", prefer);

            var op = req.SendWebRequest();
            var tcs = new TaskCompletionSource<bool>();
            op.completed += _ => tcs.TrySetResult(true);
            if (op.isDone) tcs.TrySetResult(true);
            await tcs.Task;

            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result == UnityWebRequest.Result.ConnectionError)
                throw new SupabaseException("Can't reach the server (" + req.error + ").", 0, null, isNetwork: true);
            if (req.result == UnityWebRequest.Result.ProtocolError || req.responseCode >= 400)
            {
                // Read fields by name: Auth and Database errors use different shapes (and "code" may be a number).
                string msg = FirstNonEmpty(Field(text, "msg"), Field(text, "error_description"), Field(text, "message"), Field(text, "error"), req.error);
                string code = FirstNonEmpty(Field(text, "error_code"), Field(text, "code"), Field(text, "error"));
                bool server = req.responseCode >= 500 || req.responseCode == 0;
                throw new SupabaseException(msg, req.responseCode, code, isNetwork: server);
            }
            if (req.result != UnityWebRequest.Result.Success)
                throw new SupabaseException("Request failed (" + req.error + ").", req.responseCode, null, isNetwork: true);
            return text;
        }
    }

    static string FirstNonEmpty(params string[] values)
    {
        foreach (var v in values) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
