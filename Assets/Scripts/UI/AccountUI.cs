using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif

/// <summary>
/// Account panels on the main menu. The panels are scene objects under "MainMenu Canvas/Panels"
/// (created once by Tools > Scam Sense > Build Menu UI In Scene) and every button's OnClick is a
/// persistent event pointing at the public methods below, so everything can be edited in the Inspector.
///
/// Flow: PLAY (or any level) -> AuthenticationPanel [PLAY AS GUEST | PLAYER SIGN IN | PLAY OFFLINE | BACK]
///       PLAYER SIGN IN -> PlayerSignInPanel [SIGN IN | CREATE ACCOUNT | BACK]
///       CREATE ACCOUNT -> CreateAccountPanel [CREATE ACCOUNT | BACK]
/// A returning player is signed back in silently, so PLAY starts straight away. The status chip (top-right)
/// opens the AccountPanel (create account keeping guest progress, switch account, sync, sign out).
/// All logic lives in CloudAccountService; this class only shows panels and messages.
/// </summary>
public class AccountUI : MonoBehaviour
{
    /// <summary>True while an account panel covers the menu (MainMenuUI ignores Esc then).</summary>
    public static bool IsBlocking { get; private set; }

    public enum Screen { None, Authentication, SignIn, CreateAccount, Account, Busy }

    [Header("Scene UI (built in the scene by Tools > Scam Sense > Build Menu UI In Scene)")]
    [SerializeField] private bool builtInScene;
    [SerializeField] private GameObject authDim;
    [SerializeField] private RectTransform authenticationPanel, playerSignInPanel, createAccountPanel, accountPanel, busyPanel, accountChip;

    [Header("Authentication panel")]
    [SerializeField] private TMP_Text authStatusText;

    [Header("Player sign-in panel")]
    [SerializeField] private TMP_InputField signInUsername;
    [SerializeField] private TMP_InputField signInPassword;
    [SerializeField] private TMP_Text signInSubtitle, signInError;

    [Header("Create account panel")]
    [SerializeField] private TMP_InputField createUsername;
    [SerializeField] private TMP_InputField createPassword, createConfirmPassword;
    [SerializeField] private TMP_Text createSubtitle, createError;

    [Header("Account panel")]
    [SerializeField] private TMP_Text accountInfo;
    [SerializeField] private TMP_Text accountDetail;
    [SerializeField] private GameObject accountCreateButton, accountSignInOtherButton, accountSyncButton, accountTryConnectButton, accountSignOutButton;

    [Header("Busy panel & status chip")]
    [SerializeField] private TMP_Text busyText;
    [SerializeField] private TMP_Text chipText;

    MainMenuUI menu;
    CloudAccountService svc;
    Screen screen = Screen.None;
    Screen backFromSignIn = Screen.Authentication, backFromCreate = Screen.SignIn;
    Action pendingAction;
    string busyLabel = "";
    float busyDots;

    /// <summary>True once the player chose how to play (guest, account or offline).</summary>
    public bool ReadyToPlay => svc != null && svc.CurrentMode != CloudAccountService.Mode.None;

    // ---------- Startup ----------

    public void Init(MainMenuUI owner)
    {
        menu = owner;
        svc = CloudAccountService.Instance;
        if (!builtInScene || authenticationPanel == null) Build(menu.PanelHost, menu.Root, menu);   // fallback
        HideAll();
        svc.StateChanged += OnServiceState;
        _ = RestoreSilentlyAsync();
    }

    void OnDestroy()
    {
        IsBlocking = false;
        if (CloudAccountService.Exists) CloudAccountService.Instance.StateChanged -= OnServiceState;
    }

    /// <summary>A returning player is signed back in without any panel; otherwise PLAY will ask.</summary>
    async Task RestoreSilentlyAsync()
    {
        if (svc.CurrentMode != CloudAccountService.Mode.None) { RefreshChip(); return; }
        try
        {
            await svc.EnsureInitializedAsync();
            if (svc.CanRestore && await svc.TryRestoreAsync() && this != null) menu.RefreshProgress();
        }
        catch { /* offline: PLAY offers offline play */ }
        if (this != null) RefreshChip();
    }

    /// <summary>Called by MainMenuUI when a level should start but no account was chosen yet.</summary>
    public void RequestAccount(Action thenContinue)
    {
        pendingAction = thenContinue;
        Show(Screen.Authentication);
        SetMessage(authStatusText, svc.ServicesReady ? "" :
            "Online services aren't reachable right now. You can still play offline - progress is saved on this PC.", false);
    }

    void Update()
    {
        if (screen == Screen.Busy)
        {
            busyDots += Time.unscaledDeltaTime * 3f;
            busyText.text = busyLabel + new string('.', 1 + (int)busyDots % 3);
        }

        var kb = Keyboard.current;
        if (kb == null || screen == Screen.None || screen == Screen.Busy) return;
        if (kb.escapeKey.wasPressedThisFrame) Back();
        if (screen == Screen.SignIn || screen == Screen.CreateAccount)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (screen == Screen.SignIn) SubmitSignIn(); else SubmitCreateAccount();
            }
            if (kb.tabKey.wasPressedThisFrame) FocusNextField();
        }
    }

    // ---------- Button actions (public: they are the persistent OnClick targets) ----------

    public async void PlayAsGuest()
    {
        Show(Screen.Busy, "SIGNING IN AS GUEST");
        bool ok = await svc.PlayAsGuestAsync();
        if (this == null) return;
        if (ok) Finish();
        else { Show(Screen.Authentication); SetMessage(authStatusText, svc.LastError, true); }
    }

    public void PlayOffline()
    {
        svc.PlayOffline();
        Finish();
    }

    public void OpenSignIn()
    {
        backFromSignIn = screen == Screen.Account ? Screen.Account : Screen.Authentication;
        Show(Screen.SignIn);
    }

    public void OpenCreateAccount()
    {
        backFromCreate = screen == Screen.Account ? Screen.Account : Screen.SignIn;
        Show(Screen.CreateAccount);
    }

    public void OpenAccountPanel() => Show(Screen.Account);

    /// <summary>BACK on any panel: returns to the panel it came from (progress is never touched).</summary>
    public void Back()
    {
        switch (screen)
        {
            case Screen.SignIn: Show(backFromSignIn); break;
            case Screen.CreateAccount: Show(backFromCreate); break;
            case Screen.Authentication: pendingAction = null; Show(Screen.None); break;
            default: Show(Screen.None); break;
        }
    }

    public async void SubmitSignIn()
    {
        string u = signInUsername.text, p = signInPassword.text;
        string problem = CloudAccountService.ValidateUsername(u) ?? (string.IsNullOrEmpty(p) ? "Enter your password." : null);
        if (problem != null) { SetMessage(signInError, problem, true); return; }

        Show(Screen.Busy, "SIGNING IN");
        bool ok = await svc.SignInAsync(u, p);
        if (this == null) return;
        signInPassword.text = "";   // never keep the password around
        if (ok) Finish();
        else { Show(Screen.SignIn, keepFields: true); SetMessage(signInError, svc.LastError, true); }
    }

    public async void SubmitCreateAccount()
    {
        string u = createUsername.text, p = createPassword.text, c = createConfirmPassword.text;
        string problem = CloudAccountService.ValidateUsername(u) ?? CloudAccountService.ValidatePassword(p) ??
                         (p != c ? "The passwords don't match." : null);
        if (problem != null) { SetMessage(createError, problem, true); return; }

        Show(Screen.Busy, "CREATING ACCOUNT");
        bool ok = await svc.CreateAccountAsync(u, p);
        if (this == null) return;
        createPassword.text = createConfirmPassword.text = "";
        if (ok) Finish();
        else { Show(Screen.CreateAccount, keepFields: true); SetMessage(createError, svc.LastError, true); }
    }

    public void SignOut()
    {
        svc.SignOut();
        menu.RefreshProgress();
        pendingAction = null;
        Show(Screen.Authentication);
        SetMessage(authStatusText, "Signed out. Your progress is kept. Choose how to play next time you press PLAY.", false);
    }

    public async void SyncNow()
    {
        await svc.SyncNowAsync();
        if (this != null && screen == Screen.Account) RefreshAccountPanel();
    }

    public async void TryConnect()
    {
        Show(Screen.Busy, "CONNECTING");
        bool ok = await svc.TryRestoreAsync() && svc.SignedIn;
        if (this == null) return;
        if (ok) { menu.RefreshProgress(); Show(Screen.None); }
        else { Show(Screen.Authentication); SetMessage(authStatusText, svc.LastError ?? "Couldn't connect. Choose how to continue.", true); }
    }

    void Finish()
    {
        menu.RefreshProgress();
        Show(Screen.None);
        var next = pendingAction;
        pendingAction = null;
        next?.Invoke();   // e.g. start the level the player clicked
    }

    // ---------- Screens ----------

    void HideAll()
    {
        foreach (var p in new[] { authenticationPanel, playerSignInPanel, createAccountPanel, accountPanel, busyPanel })
            if (p != null) p.gameObject.SetActive(false);
        if (authDim != null) authDim.SetActive(false);
        screen = Screen.None;
        IsBlocking = false;
        RefreshChip();
    }

    void Show(Screen s, string label = null, bool keepFields = false)
    {
        screen = s;
        authenticationPanel.gameObject.SetActive(s == Screen.Authentication);
        playerSignInPanel.gameObject.SetActive(s == Screen.SignIn);
        createAccountPanel.gameObject.SetActive(s == Screen.CreateAccount);
        accountPanel.gameObject.SetActive(s == Screen.Account);
        busyPanel.gameObject.SetActive(s == Screen.Busy);
        authDim.SetActive(s != Screen.None);   // inactive panels and dim never block the menu
        IsBlocking = s != Screen.None;

        bool guest = svc.SignedIn && svc.CurrentMode == CloudAccountService.Mode.Guest;
        if (s == Screen.Authentication) SetMessage(authStatusText, "", false);
        if (s == Screen.SignIn)
        {
            signInSubtitle.text = guest ? "Signing in to an existing account moves this guest's progress into it." : "Welcome back, investigator.";
            if (!keepFields) { signInPassword.text = ""; SetMessage(signInError, "", false); }
            signInUsername.ActivateInputField();
        }
        if (s == Screen.CreateAccount)
        {
            createSubtitle.text = guest ? "Your guest progress stays with your new account - same player, same saves."
                                        : "Create a username and password to keep your progress safe on any device.";
            if (!keepFields) { createPassword.text = createConfirmPassword.text = ""; SetMessage(createError, "", false); }
            createUsername.ActivateInputField();
        }
        if (s == Screen.Account) RefreshAccountPanel();
        if (s == Screen.Busy) { busyLabel = label ?? "PLEASE WAIT"; busyDots = 0f; }

        foreach (var p in new[] { authenticationPanel, playerSignInPanel, createAccountPanel, accountPanel, busyPanel })
            if (p.gameObject.activeSelf) StartCoroutine(PanelIn(p));
        RefreshChip();
    }

    void FocusNextField()
    {
        if (screen == Screen.SignIn) (signInUsername.isFocused ? signInPassword : signInUsername).ActivateInputField();
        else if (createUsername.isFocused) createPassword.ActivateInputField();
        else if (createPassword.isFocused) createConfirmPassword.ActivateInputField();
        else createUsername.ActivateInputField();
    }

    void RefreshAccountPanel()
    {
        var mode = svc.CurrentMode;
        accountInfo.text = $"<b>{svc.DisplayName}</b>   <color={StatusHex()}>{svc.StatusText}</color>";
        accountDetail.text =
            mode == CloudAccountService.Mode.Guest
                ? "You're playing as a guest. Progress is saved to the cloud, but it's tied to this PC: if Unity's saved login is cleared or you switch devices, it can be lost. Create an account to keep it - your progress stays exactly as it is."
            : mode == CloudAccountService.Mode.Player
                ? "Your progress is saved to your account and syncs automatically after every level."
                : "You're offline. Results are saved on this PC and upload automatically when you're back online.";
        bool guest = mode == CloudAccountService.Mode.Guest, player = mode == CloudAccountService.Mode.Player, offline = mode == CloudAccountService.Mode.Offline;
        accountCreateButton.SetActive(guest);
        accountSignInOtherButton.SetActive(guest || player);
        accountSyncButton.SetActive(player);
        accountTryConnectButton.SetActive(offline);
        accountSignOutButton.SetActive(guest || player);
    }

    void OnServiceState()
    {
        if (this == null) return;
        RefreshChip();
        if (screen == Screen.Account) accountInfo.text = $"<b>{svc.DisplayName}</b>   <color={StatusHex()}>{svc.StatusText}</color>";
    }

    void RefreshChip()
    {
        if (accountChip == null || svc == null) return;
        bool show = screen == Screen.None && svc.CurrentMode != CloudAccountService.Mode.None;
        accountChip.gameObject.SetActive(show);
        if (show) chipText.text = $"{svc.DisplayName}  <color={UIKit.HexSecondary}>·</color>  <color={StatusHex()}>{svc.StatusText}</color>";
    }

    string StatusHex()
    {
        string s = svc.StatusText;
        return s == "ONLINE" ? UIKit.HexSafe : s == "OFFLINE" ? UIKit.HexSecondary : UIKit.HexWarning;
    }

    static void SetMessage(TMP_Text t, string msg, bool error)
    {
        if (t == null) return;
        t.text = msg ?? "";
        t.color = error ? UIKit.Danger : UIKit.TextSecondary;
        t.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    System.Collections.IEnumerator PanelIn(RectTransform p)
    {
        var g = p.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.18f);
            g.alpha = k;
            p.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k);
            yield return null;
        }
        g.alpha = 1f; p.localScale = Vector3.one;
    }

    // ---------- Build (editor: into the scene; runtime: only as a fallback) ----------

#if UNITY_EDITOR
    /// <summary>Editor only: creates the account panels under the menu's Canvas and connects every button.</summary>
    public void BuildIntoScene(MainMenuUI owner)
    {
        Build(owner.PanelHost, owner.Root, owner);
        builtInScene = true;
    }
#endif

    void Build(RectTransform panelHost, RectTransform canvasRoot, MainMenuUI owner)
    {
        // Dim behind the account panels (blocks clicks on the menu only while a panel is open)
        authDim = UIKit.Dimmer(panelHost, 0.86f).gameObject;
        authDim.name = "AuthDim";

        // ----- AuthenticationPanel -----
        authenticationPanel = Panel(panelHost, "AuthenticationPanel", "HOW DO YOU WANT TO PLAY?", 760);
        UIKit.Text(authenticationPanel, "PanelBody", "Guests start right away. An account keeps your progress safe on any device.", 25, UIKit.TextSecondary).lineSpacing = 6f;
        Hud(authenticationPanel, "PlayAsGuestButton", "PLAY AS GUEST", PlayAsGuest, UIKit.HudVariant.Primary, 76, 30, owner);
        Hud(authenticationPanel, "PlayerSignInButton", "PLAYER SIGN IN", OpenSignIn, UIKit.HudVariant.Secondary, 68, 26, owner);
        Hud(authenticationPanel, "PlayOfflineButton", "PLAY OFFLINE", PlayOffline, UIKit.HudVariant.Secondary, 56, 22, owner);
        Hud(authenticationPanel, "BackButton", "BACK", Back, UIKit.HudVariant.Secondary, 56, 22, owner);
        var note = UIKit.Text(authenticationPanel, "GuestNote",
            "Guest progress is tied to this PC. If Unity's saved login is cleared or you change devices, it can be lost unless you create an account.",
            19, new Color(1, 1, 1, 0.55f));
        note.lineSpacing = 4f;
        authStatusText = UIKit.Text(authenticationPanel, "StatusText", "", 21, UIKit.Danger);

        // ----- PlayerSignInPanel -----
        playerSignInPanel = Panel(panelHost, "PlayerSignInPanel", "PLAYER SIGN IN", 760);
        signInSubtitle = UIKit.Text(playerSignInPanel, "PanelSubtitle", "", 23, UIKit.TextSecondary);
        UIKit.Header(playerSignInPanel, "Username");
        signInUsername = Field(playerSignInPanel, "UsernameInput", "3-20 characters", false);
        UIKit.Header(playerSignInPanel, "Password");
        signInPassword = Field(playerSignInPanel, "PasswordInput", "Password", true);
        signInError = UIKit.Text(playerSignInPanel, "ErrorMessageText", "", 21, UIKit.Danger);
        var signRow = Row(playerSignInPanel);
        signRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;   // widths follow the weights below
        Weight(Hud(signRow, "SignInButton", "SIGN IN", SubmitSignIn, UIKit.HudVariant.Primary, -1, 26, owner), 1f);
        Weight(Hud(signRow, "CreateAccountButton", "CREATE ACCOUNT", OpenCreateAccount, UIKit.HudVariant.Secondary, -1, 24, owner), 1.55f);
        Weight(Hud(signRow, "BackButton", "BACK", Back, UIKit.HudVariant.Secondary, -1, 26, owner), 0.8f);

        // ----- CreateAccountPanel -----
        createAccountPanel = Panel(panelHost, "CreateAccountPanel", "CREATE ACCOUNT", 760);
        createSubtitle = UIKit.Text(createAccountPanel, "PanelSubtitle", "", 23, UIKit.TextSecondary);
        UIKit.Header(createAccountPanel, "Username");
        createUsername = Field(createAccountPanel, "UsernameInput", "3-20 characters", false);
        UIKit.Header(createAccountPanel, "Password");
        createPassword = Field(createAccountPanel, "PasswordInput", "Password", true);
        UIKit.Header(createAccountPanel, "Confirm password");
        createConfirmPassword = Field(createAccountPanel, "ConfirmPasswordInput", "Type the password again", true);
        var rules = UIKit.Text(createAccountPanel, "Rules",
            "Username: 3-20 characters - letters, numbers and . - @ _\nPassword: 8-30 characters with an upper-case letter, a lower-case letter, a number and a symbol.",
            19, new Color(1, 1, 1, 0.6f));
        rules.lineSpacing = 4f;
        createError = UIKit.Text(createAccountPanel, "ErrorMessageText", "", 21, UIKit.Danger);
        var createRow = Row(createAccountPanel);
        Hud(createRow, "CreateAccountButton", "CREATE ACCOUNT", SubmitCreateAccount, UIKit.HudVariant.Primary, -1, 26, owner);
        Hud(createRow, "BackButton", "BACK", Back, UIKit.HudVariant.Secondary, -1, 26, owner);

        // ----- AccountPanel (opened from the status chip) -----
        accountPanel = Panel(panelHost, "AccountPanel", "ACCOUNT", 760);
        accountInfo = UIKit.Text(accountPanel, "AccountInfo", "", 28, UIKit.TextPrimary);
        accountDetail = UIKit.Text(accountPanel, "AccountDetail", "", 21, UIKit.TextSecondary);
        accountDetail.lineSpacing = 5f;
        accountCreateButton = Hud(accountPanel, "CreateAccountKeepProgressButton", "CREATE ACCOUNT  (KEEP PROGRESS)", OpenCreateAccount, UIKit.HudVariant.Primary, 62, 24, owner).gameObject;
        accountSignInOtherButton = Hud(accountPanel, "SignInOtherAccountButton", "SIGN IN TO ANOTHER ACCOUNT", OpenSignIn, UIKit.HudVariant.Secondary, 62, 24, owner).gameObject;
        accountSyncButton = Hud(accountPanel, "SyncNowButton", "SYNC NOW", SyncNow, UIKit.HudVariant.Primary, 62, 24, owner).gameObject;
        accountTryConnectButton = Hud(accountPanel, "TryConnectButton", "TRY TO CONNECT", TryConnect, UIKit.HudVariant.Primary, 62, 24, owner).gameObject;
        accountSignOutButton = Hud(accountPanel, "SignOutButton", "SIGN OUT", SignOut, UIKit.HudVariant.Secondary, 62, 24, owner).gameObject;
        Hud(accountPanel, "CloseButton", "CLOSE", Back, UIKit.HudVariant.Secondary, 62, 24, owner);

        // ----- BusyPanel -----
        busyPanel = UIKit.Rect("BusyPanel", panelHost);
        UIKit.Place(busyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 120));
        UIKit.PanelImage(busyPanel);
        UIKit.AccentStrip(busyPanel, null, 8f);
        busyPanel.gameObject.AddComponent<CanvasGroup>();
        busyText = UIKit.Text(busyPanel, "BusyText", "CONNECTING", 28, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        busyText.characterSpacing = 4f;
        UIKit.Stretch(busyText.rectTransform);

        // ----- AccountChip (top-right of the menu) -----
        accountChip = UIKit.Rect("AccountChip", canvasRoot);
        UIKit.Place(accountChip, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(460, 54));
        var chipBg = UIKit.PanelImage(accountChip, new Color(UIKit.Panel.r, UIKit.Panel.g, UIKit.Panel.b, 0.92f));
        UIKit.AccentStrip(accountChip);
        var chipBtn = accountChip.gameObject.AddComponent<Button>();
        chipBtn.targetGraphic = chipBg;
        Wire(chipBtn, OpenAccountPanel);
        chipText = UIKit.Text(accountChip, "ChipText", "", 20, UIKit.TextPrimary, TextAlignmentOptions.MidlineRight, bold: true);
        chipText.characterSpacing = 1.5f;
        UIKit.Stretch(chipText.rectTransform);
        chipText.margin = new Vector4(22, 0, 22, 0);

        foreach (var g in new GameObject[] { authenticationPanel.gameObject, playerSignInPanel.gameObject, createAccountPanel.gameObject,
                                             accountPanel.gameObject, busyPanel.gameObject, accountChip.gameObject, authDim })
            g.SetActive(false);
        foreach (var t in new[] { authStatusText, signInError, createError }) t.gameObject.SetActive(false);
    }

    // ---------- Build helpers ----------

    void Wire(Button b, UnityEngine.Events.UnityAction call)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEventTools.AddPersistentListener(b.onClick, call); return; }
#endif
        b.onClick.AddListener(call);
    }

    /// <summary>A menu-style HUD button (same template as the menu's PLAY/SETTINGS buttons), renamed and wired.</summary>
    Button Hud(RectTransform parent, string objectName, string label, UnityEngine.Events.UnityAction call,
               UIKit.HudVariant v, float height, float font, MainMenuUI owner)
    {
        var b = UIKit.HudButton(parent, label, null, v, font);
        b.name = objectName;
        if (height > 0f) UIKit.Size(b, -1, height);
        Wire(b, call);
        if (owner != null) owner.AddFx(b);   // same click sound + hover sound as the rest of the menu
        return b;
    }

    /// <summary>Share of a button row's width (so long labels like CREATE ACCOUNT aren't squeezed).</summary>
    static void Weight(Button b, float flexible)
    {
        var le = b.GetComponent<LayoutElement>();
        if (le == null) le = b.gameObject.AddComponent<LayoutElement>();   // (no ?? : Unity's fake-null)
        le.preferredWidth = 0f;
        le.flexibleWidth = flexible;
    }

    static RectTransform Row(RectTransform parent)
    {
        var row = UIKit.Rect("Buttons", parent);
        UIKit.Horizontal(row, 16);
        UIKit.Size(row, -1, 72);
        return row;
    }

    static RectTransform Panel(RectTransform parent, string name, string title, float width)
    {
        var p = UIKit.Rect(name, parent);
        UIKit.Place(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0));
        UIKit.PanelImage(p);
        var v = UIKit.Vertical(p, 40, 16);
        v.padding.left = v.padding.right = 52;
        UIKit.FitHeight(p);
        UIKit.AccentStrip(p, null, 8f);
        p.gameObject.AddComponent<CanvasGroup>();
        var head = UIKit.Rect("Header", p);
        UIKit.Size(head, -1, 56);
        var t = UIKit.Text(head, "PanelTitle", title, UIKit.SizeH1, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        t.characterSpacing = 6f;
        UIKit.Stretch(t.rectTransform);
        return p;
    }

    /// <summary>A TextMeshPro input field in the menu style (password fields hide the text).</summary>
    static TMP_InputField Field(RectTransform parent, string objectName, string placeholder, bool password)
    {
        var r = UIKit.Rect(objectName, parent);
        r.gameObject.SetActive(false);   // wire everything before the field's OnEnable runs
        UIKit.Size(r, -1, 64);
        var bg = UIKit.AddImage(r, UIKit.Background, rounded: true);
        var outline = r.gameObject.AddComponent<Outline>();
        outline.effectColor = UIKit.Border;
        outline.effectDistance = new Vector2(2, -2);

        var area = UIKit.Rect("TextArea", r);
        UIKit.Stretch(area);
        area.offsetMin = new Vector2(20, 6);
        area.offsetMax = new Vector2(-20, -6);
        area.gameObject.AddComponent<RectMask2D>();

        var ph = UIKit.Text(area, "Placeholder", placeholder, 24, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.MidlineLeft);
        ph.fontStyle = FontStyles.Italic;
        UIKit.Stretch(ph.rectTransform);
        var txt = UIKit.Text(area, "Text", "", 24, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft);
        txt.richText = false;
        UIKit.Stretch(txt.rectTransform);

        var f = r.gameObject.AddComponent<TMP_InputField>();
        f.textViewport = area;
        f.textComponent = txt;
        f.placeholder = ph;
        f.targetGraphic = bg;
        f.fontAsset = txt.font;
        f.pointSize = 24;
        f.lineType = TMP_InputField.LineType.SingleLine;
        f.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        f.characterLimit = password ? 30 : 20;
        f.richText = false;
        f.customCaretColor = true;
        f.caretColor = UIKit.Accent;
        f.selectionColor = new Color(0.22f, 0.74f, 0.97f, 0.35f);
        r.gameObject.SetActive(true);
        return f;
    }
}
