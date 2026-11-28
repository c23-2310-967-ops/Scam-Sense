using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif
using M = ClassroomModels;
using CA = ClassroomAnalytics;

/// <summary>
/// CLASSROOM on the main menu: one panel, two portals.
///   Student: join a class with a class code, play, and see completed levels, scores, every attempt with feedback,
///            topics to review and achievements.
///   Teacher: dashboard, students (roster, remove / re-admit, student detail), analytics charts, reports with
///            filters and CSV export, and class management (create classes with a generated code, archive).
/// The role comes from the server (ClassroomService.LoadRoleAsync); every number comes from recorded rows
/// (ClassroomAnalytics) - nothing is estimated. The panel shell (frame, title, tab bar, scroll area, CLOSE /
/// REFRESH) is a scene object built by Tools > Scam Sense > Build Menu UI In Scene; its contents are rebuilt at
/// runtime from the data, like the Career panel.
/// </summary>
public partial class ClassroomUI : MonoBehaviour   // QUESTIONS tab: ClassroomUI.Questions.cs
{
    public static bool IsOpen { get; private set; }

    [Header("Scene UI (built by Tools > Scam Sense > Build Menu UI In Scene)")]
    [SerializeField] private bool builtInScene;
    [SerializeField] private GameObject dim;
    [SerializeField] private RectTransform panel, tabBar, content;
    [SerializeField] private TMP_Text titleText, subtitleText, statusText;
    [SerializeField] private ScrollRect scroll;

    enum Tab { Overview, Results, Topics, Achievements, Dashboard, Students, Analytics, Reports, Classes, Questions, Requests }

    MainMenuUI menu;
    AccountUI account;
    CloudAccountService svc;
    int version;
    Tab tab = Tab.Overview;
    string lastUser;

    // Student state
    M.MyClass myClass;
    StudentRecords records;
    bool confirmLeave;
    TMP_InputField joinField, teacherCodeField;
    TMP_Text joinError, teacherError;

    // Teacher state
    List<M.ClassInfo> classes = new List<M.ClassInfo>();
    string currentClassId;
    ClassData data;
    string selectedStudent, confirmRemove, confirmArchive, confirmDeleteClass;
    bool showArchived;   // CLASSES: archived classes are hidden until SHOW ARCHIVED
    List<M.ProgressRow> detailProgress;
    List<M.AchievementRow> detailAchievements;
    TMP_InputField newClassField;
    TMP_Text newClassError;
    int reportStudent = -1, reportLevel = -1;
    bool reportAnswers;
    string lastExportFolder;

    const float Body = 22f, Small = 19f;
    static readonly Color Muted = new Color32(0x94, 0xA3, 0xB8, 255);

    // ---------- Startup ----------

    public void Init(MainMenuUI owner)
    {
        menu = owner;
        account = owner.GetComponent<AccountUI>();
        svc = CloudAccountService.Instance;
        if (!builtInScene || panel == null) Build(owner.PanelHost, owner);   // fallback for scenes built before the classroom
        Hide();
        svc.StateChanged += OnServiceState;
        svc.ResultsUploaded += OnResultsUploaded;
    }

    void OnDestroy()
    {
        IsOpen = false;
        if (CloudAccountService.Exists)
        {
            CloudAccountService.Instance.StateChanged -= OnServiceState;
            CloudAccountService.Instance.ResultsUploaded -= OnResultsUploaded;
        }
    }

    void OnServiceState()
    {
        if (this == null || !IsOpen || svc.Busy) return;
        string user = svc.SignedIn ? svc.UserId : svc.CurrentMode.ToString();
        if (user != lastUser) Reload();   // signed in / out / went offline while the panel is open
    }

    void OnResultsUploaded()
    {
        if (this != null && IsOpen && ClassroomService.CurrentRole != ClassroomService.Role.Teacher) Reload();
    }

    void Update()
    {
        if (!IsOpen || AccountUI.IsBlocking) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (selectedStudent != null && tab == Tab.Students) { selectedStudent = null; Render(); }
            else if (tab == Tab.Questions && CloseQuestionForm()) { }
            else if (tab == Tab.Requests && openRequestId != null) { openRequestId = null; Render(); }
            else Close();
        }
    }

    // ---------- Open / close (public: persistent OnClick targets) ----------

    public void Open()
    {
        if (panel == null) return;
        var root = menu != null ? menu.Root : null;
        float w = root != null && root.rect.width > 0 ? Mathf.Min(1760f, root.rect.width - 80f) : 1760f;
        panel.sizeDelta = new Vector2(w, panel.sizeDelta.y);
        dim.SetActive(true);
        panel.gameObject.SetActive(true);
        IsOpen = true;
        selectedStudent = null;
        confirmLeave = false; confirmRemove = confirmArchive = confirmDeleteClass = null;
        StartCoroutine(PanelIn());
        Reload();
    }

    public void Close()
    {
        Hide();
        if (menu != null) menu.RefreshProgress();
    }

    void Hide()
    {
        IsOpen = false;
        if (panel != null) panel.gameObject.SetActive(false);
        if (dim != null) dim.SetActive(false);
    }

    /// <summary>REFRESH: reads everything again from the server.</summary>
    public void Refresh() => Reload();

    System.Collections.IEnumerator PanelIn()
    {
        var g = panel.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.18f);
            if (g != null) g.alpha = k;
            panel.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k);
            yield return null;
        }
        if (g != null) g.alpha = 1f;
        panel.localScale = Vector3.one;
    }

    // ---------- Loading ----------

    async void Reload()
    {
        int v = ++version;
#if UNITY_EDITOR
        previewTeacher = false;
#endif
        lastUser = svc.SignedIn ? svc.UserId : svc.CurrentMode.ToString();
        qbank = null; qbankError = null;   // REFRESH reads the question bank again too (an open edit is kept)
        adminRequests = null; requestsError = null; portalAdmin = false;   // read again from the server below
        ClearAll();
        SetStatus("", false);

        if (!svc.ServicesReady) { RenderMessage("CLASSROOM", "Online accounts aren't set up in this build, so classes aren't available. You can still play and your progress is saved on this PC."); return; }
        if (svc.CurrentMode == CloudAccountService.Mode.None) { RenderSignedOut(); return; }
        if (svc.CurrentMode == CloudAccountService.Mode.Offline || !svc.SignedIn) { RenderOffline(); return; }

        titleText.text = "CLASSROOM";
        subtitleText.text = svc.DisplayName + "  ·  loading…";
        Loading();
        try
        {
            var role = await ClassroomService.LoadRoleAsync();
            if (!Current(v)) return;
            portalAdmin = await QuestionBankService.IsAdminAsync();   // content admin (server-side table); false on any error
            qAdmin = portalAdmin;   // one answer for every tab: QUESTIONS must never fall back to the teacher (request) mode for an admin
            if (!Current(v)) return;
            if (role == ClassroomService.Role.Teacher)
            {
                await LoadTeacherAsync(v);
                if (!Current(v)) return;
                if (!IsTeacherTab(tab)) tab = Tab.Dashboard;
            }
            else if (portalAdmin)
            {
                // An admin who isn't a teacher: the question management tabs only.
                if (tab != Tab.Questions && tab != Tab.Requests) tab = Tab.Requests;
            }
            else
            {
                myClass = await ClassroomService.MyClassAsync();
                if (!Current(v)) return;
                records = await ClassroomService.LoadMyRecordsAsync();
                if (!Current(v)) return;
                if (IsTeacherTab(tab)) tab = Tab.Overview;
            }
            Render();
        }
        catch (Exception e)
        {
            if (!Current(v)) return;
            Debug.LogWarning("ClassroomUI: " + e.Message);
            RenderError(ClassroomService.Message(e));
        }
    }

    bool Current(int v) => this != null && IsOpen && v == version;
    static bool IsTeacherTab(Tab t) => t >= Tab.Dashboard;

    async Task LoadTeacherAsync(int v)
    {
        classes = await ClassroomService.ListClassesAsync();
        if (!Current(v)) return;
        var cls = classes.FirstOrDefault(c => c.id == currentClassId) ?? classes.FirstOrDefault(c => !c.archived) ?? classes.FirstOrDefault();
        currentClassId = cls?.id;
        data = null;
        if (cls != null) data = await ClassroomService.LoadClassAsync(cls);
    }

    async void SwitchClass(int step)
    {
        if (classes.Count == 0) return;
        int i = classes.FindIndex(c => c.id == currentClassId);
        i = ((i < 0 ? 0 : i) + step + classes.Count) % classes.Count;
        await OpenClass(classes[i].id, tab);
    }

    async Task OpenClass(string classId, Tab show)
    {
        int v = ++version;
        currentClassId = classId;
        selectedStudent = null; confirmRemove = null;
        reportStudent = reportLevel = -1;
        tab = show;
        ClearAll();
        Loading();
        try
        {
            var cls = classes.First(c => c.id == classId);
            data = await ClassroomService.LoadClassAsync(cls);
            if (!Current(v)) return;
            Render();
        }
        catch (Exception e)
        {
            if (!Current(v)) return;
            RenderError(ClassroomService.Message(e));
        }
    }

    // ---------- Rendering ----------

#if UNITY_EDITOR
    bool previewTeacher;
    /// <summary>Editor previews: show the teacher tabs (false = an admin who isn't a teacher).</summary>
    public bool previewAsTeacher = true;

    /// <summary>
    /// Editor only (layout checks in Play Mode): shows the portal with rows passed in by the caller instead of server data.
    /// Never compiled into a build.
    /// </summary>
    public void EditorPreview(bool teacher, ClassData classData, List<M.ClassInfo> classList, StudentRecords studentRecords, M.MyClass cls, string showTab)
    {
        version++;
        previewTeacher = teacher;
        data = classData; classes = classList ?? new List<M.ClassInfo>(); currentClassId = classData?.info?.id;
        records = studentRecords; myClass = cls;
        if (Enum.TryParse(showTab, out Tab t)) tab = t;
        selectedStudent = null;
        if (!IsOpen) { dim.SetActive(true); panel.gameObject.SetActive(true); IsOpen = true; }
        Render();
    }

    /// <summary>Editor only: opens a student's detail page in the preview.</summary>
    public void EditorPreviewStudent(string studentId) { tab = Tab.Students; selectedStudent = studentId; Render(); }
#endif

    /// <summary>A teacher (classes, students...). An admin who isn't a teacher only gets the question tabs.</summary>
    bool IsTeacherRole
    {
        get
        {
#if UNITY_EDITOR
            if (previewTeacher && previewAsTeacher) return true;
#endif
            return ClassroomService.CurrentRole == ClassroomService.Role.Teacher;
        }
    }

    bool TeacherView
    {
        get
        {
#if UNITY_EDITOR
            if (previewTeacher) return true;
#endif
            return ClassroomService.CurrentRole == ClassroomService.Role.Teacher || portalAdmin;
        }
    }

    void Render()
    {
        ClearAll();
        if (TeacherView) RenderTeacher();
        else RenderStudent();
        content.anchoredPosition = Vector2.zero;
    }

    void ClearAll()
    {
        foreach (Transform c in tabBar) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        foreach (Transform c in content) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        joinField = teacherCodeField = newClassField = null;
    }

    void Loading()
    {
        var t = Line(content, "Loading your classroom…", Body, Muted);
        UIKit.Size(t, -1, 80);
    }

    void RenderMessage(string title, string message)
    {
        titleText.text = title;
        subtitleText.text = "";
        Line(content, message, Body, UIKit.TextSecondary);
    }

    void RenderError(string message)
    {
        ClearAll();
        titleText.text = "CLASSROOM";
        subtitleText.text = svc.DisplayName;
        var card = UICharts.Card(content, "Couldn't load the classroom");
        Line(card, message, Body, UIKit.Danger);
        var row = RowH(card, 62);
        Btn(row, "TRY AGAIN", Reload, UIKit.HudVariant.Primary, 260);
        SetStatus(message, true);
    }

    void RenderSignedOut()
    {
        titleText.text = "CLASSROOM";
        subtitleText.text = "Sign in to join your class";
        var card = UICharts.Card(content, "Students and teachers");
        Line(card, "Students: sign in or create an account, then join your class with the code your teacher gives you. Every level you finish is added to your class record automatically.", Body, UIKit.TextSecondary);
        Line(card, "Teachers: sign in with your account, then enter the teacher code from your school to open the teacher portal.", Body, UIKit.TextSecondary);
        var row = RowH(card, 64);
        Btn(row, "SIGN IN", () => AccountFlow(false), UIKit.HudVariant.Primary, 260);
        Btn(row, "CREATE ACCOUNT", () => AccountFlow(true), UIKit.HudVariant.Secondary, 320);
        Line(card, "Just want to play? Close this and press PLAY - guest and offline play work as before.", Small, Muted);
    }

    void RenderOffline()
    {
        titleText.text = "CLASSROOM";
        subtitleText.text = svc.DisplayName + "  ·  offline";
        var card = UICharts.Card(content, "You're offline");
        Line(card, "Your class and its records need a connection. Keep playing: results are saved on this PC and added to your class record automatically when you're back online.", Body, UIKit.TextSecondary);
        int pending = GameResultLog.PendingFor(LocalDataManager.ProfileId).Count;
        if (pending > 0) Line(card, $"<b>{pending}</b> finished level{(pending == 1 ? "" : "s")} waiting to upload.", Body, UIKit.Warning);
        var row = RowH(card, 64);
        Btn(row, "TRY TO CONNECT", TryConnect, UIKit.HudVariant.Primary, 320);
        Btn(row, "PLAY", PlayNext, UIKit.HudVariant.Secondary, 200);
        RenderLocalLevels(content);
    }

    async void TryConnect()
    {
        SetStatus("Connecting…", false);
        bool ok = await svc.TryRestoreAsync() && svc.SignedIn;
        if (this == null || !IsOpen) return;
        if (ok) Reload(); else SetStatus(svc.LastError ?? "Still offline. Try again later.", true);
    }

    void AccountFlow(bool create)
    {
        Close();
        if (account != null) account.OpenAccountFlow(create, Open);
    }

    void PlayNext()
    {
        Close();
        menu.PlayNextCase();
    }

    // ---------- Student portal ----------

    void RenderStudent()
    {
        titleText.text = "CLASSROOM";
        bool guest = svc.CurrentMode == CloudAccountService.Mode.Guest;
        subtitleText.text = $"{svc.DisplayName}  ·  {(guest ? "Guest" : "Student")}  ·  " +
                            (myClass != null ? $"<color={UIKit.HexAccent}>{Esc(myClass.class_name)}</color>" : "no class yet");
        TabButton("OVERVIEW", Tab.Overview);
        TabButton("MY RESULTS", Tab.Results);
        TabButton("TOPICS TO REVIEW", Tab.Topics);
        TabButton("ACHIEVEMENTS", Tab.Achievements);

        switch (tab)
        {
            case Tab.Results: StudentResults(); break;
            case Tab.Topics: StudentTopics(); break;
            case Tab.Achievements: StudentAchievements(); break;
            default: StudentOverview(guest); break;
        }
    }

    void StudentOverview(bool guest)
    {
        // 1) Class
        if (myClass != null)
        {
            var card = UICharts.Card(content, "Your class");
            Line(card, $"<b>{Esc(myClass.class_name)}</b>   <color={UIKit.HexSecondary}>·  Teacher: {Esc(myClass.teacher_name)}  ·  joined {M.ShortDate(myClass.joined_at)}</color>", 26f, UIKit.TextPrimary);
            Line(card, "Every level you finish is added to your class record automatically - your score, your answers and your attempts.", Small, Muted);
            var row = RowH(card, 62);
            Btn(row, "PLAY", PlayNext, UIKit.HudVariant.Primary, 240);
            Btn(row, confirmLeave ? "CONFIRM LEAVE CLASS" : "LEAVE CLASS", LeaveClass, confirmLeave ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, 360);
            if (confirmLeave) Line(card, "Leaving keeps your progress and your results. Your teacher will no longer see new results until you join again with the code.", Small, UIKit.Warning);
        }
        else if (guest)
        {
            var card = UICharts.Card(content, "Join a class");
            Line(card, "Guests can play, but joining a class needs an account so your teacher knows who you are. Create one now - your guest progress stays with it.", Body, UIKit.TextSecondary);
            var row = RowH(card, 62);
            Btn(row, "CREATE ACCOUNT  (KEEP PROGRESS)", () => AccountFlow(true), UIKit.HudVariant.Primary, 520);
            Btn(row, "SIGN IN", () => AccountFlow(false), UIKit.HudVariant.Secondary, 220);
            Btn(row, "PLAY", PlayNext, UIKit.HudVariant.Secondary, 200);
        }
        else
        {
            var card = UICharts.Card(content, "Join a class");
            Line(card, "Type the 6-character class code from your teacher (for example AB3-K7P).", Body, UIKit.TextSecondary);
            var row = RowH(card, 64);
            joinField = Input(row, "ClassCodeInput", "CLASS CODE", 9, 320);
            joinField.onSubmit.AddListener(_ => JoinClass());
            Btn(row, "JOIN CLASS", JoinClass, UIKit.HudVariant.Primary, 260);
            Btn(row, "PLAY", PlayNext, UIKit.HudVariant.Secondary, 200);
            joinError = Line(card, "", Small, UIKit.Danger);
            joinError.gameObject.SetActive(false);
        }

        // 2) Numbers
        var levels = MergedLevels();
        int done = levels.Count(l => l.completed);
        var best = levels.Where(l => l.bestScore >= 0 && l.maxScore > 0).Select(l => (float)l.bestScore / l.maxScore).ToList();
        int pending = GameResultLog.PendingFor(LocalDataManager.ProfileId).Count;
        var tiles = RowH(content, 110);
        UICharts.StatTile(tiles, $"{done} / {levels.Count}", "Levels completed");
        UICharts.StatTile(tiles, (records != null ? records.attempts.Count : 0) + pending + "", "Attempts");
        float avg = best.Count > 0 ? best.Average() : -1f;
        UICharts.StatTile(tiles, UICharts.Percent(avg), "Average best score", UICharts.ScoreColor(avg));
        UICharts.StatTile(tiles, $"{LocalDataManager.Achievements.Count} / {CareerSystem.Config.achievements.Count}", "Achievements");

        // 3) Levels
        var lvCard = UICharts.Card(content, "Your levels");
        if (pending > 0) Line(lvCard, $"<b>{pending}</b> finished level{(pending == 1 ? " is" : "s are")} still uploading - they appear in your results shortly.", Small, UIKit.Warning);
        var lvRow = RowH(lvCard, 196);
        foreach (var l in levels) LevelCard(lvRow, l);

        // 4) Teacher access (accounts only)
        if (!guest)
        {
            var tCard = UICharts.Card(content, "Are you a teacher?");
            Line(tCard, "Enter the teacher code from your school to switch this account to the teacher portal. Teacher accounts don't join classes as students.", Small, Muted);
            var row = RowH(tCard, 60);
            teacherCodeField = Input(row, "TeacherCodeInput", "Teacher code", 40, 420);
            teacherCodeField.onSubmit.AddListener(_ => ClaimTeacher());
            Btn(row, "ACTIVATE TEACHER PORTAL", ClaimTeacher, UIKit.HudVariant.Secondary, 440, 58, 20);
            teacherError = Line(tCard, "", Small, UIKit.Danger);
            teacherError.gameObject.SetActive(false);
        }
    }

    /// <summary>Server records merged with this PC's save (so levels finished offline show as finished straight away).</summary>
    List<CA.LevelRecord> MergedLevels()
    {
        var acts = records != null && records.activities.Count > 0 ? records.activities : CatalogActivities();
        var list = CA.LevelRecords(records != null ? records.attempts : new List<M.AttemptRow>(), acts);
        foreach (var l in list)
        {
            var local = LocalDataManager.Peek(l.levelId);
            if (local == null) continue;
            if (local.completed) l.completed = true;
            if (local.passed) l.passed = true;
            // A best saved when the level had more questions can be above today's maximum: never show more than 100%.
            int localBest = l.maxScore > 0 ? Mathf.Min(local.bestScore, l.maxScore) : local.bestScore;
            if (local.completed && localBest > l.bestScore) l.bestScore = localBest;
        }
        return list;
    }

    List<M.ActivityRow> CatalogActivities()
    {
        var list = new List<M.ActivityRow>();
        var cat = menu != null ? menu.Catalog : null;
        if (cat == null) return list;
        foreach (var l in cat.levels)
            if (l != null) list.Add(new M.ActivityRow { level_id = l.levelId, level_number = l.levelNumber, title = l.displayName, question_count = LevelContentResolver.QuestionCount(l) });
        return list;
    }

    void LevelCard(RectTransform parent, CA.LevelRecord l)
    {
        var card = UIKit.Rect("Level_" + l.number, parent);
        var cle = card.gameObject.AddComponent<LayoutElement>();
        cle.flexibleWidth = 1f; cle.preferredWidth = 0f;   // five equal cards
        UIKit.AddImage(card, new Color(0.08f, 0.12f, 0.2f, 1f), rounded: true).raycastTarget = false;
        var o = card.gameObject.AddComponent<Outline>();
        o.effectColor = l.completed ? new Color(UIKit.Safe.r, UIKit.Safe.g, UIKit.Safe.b, 0.6f) : UIKit.Border;
        o.effectDistance = new Vector2(2, -2);
        var v = UIKit.Vertical(card, 16, 4);
        v.padding.top = 14;
        Line(card, $"<color={UIKit.HexAccent}><b>{l.number:00}</b></color>  {Esc(l.title)}", 20f, UIKit.TextPrimary, true).textWrappingMode = TextWrappingModes.Normal;
        string state = l.completed ? (l.passed ? $"<color={UIKit.HexSafe}>✓ COMPLETED · PASSED</color>" : $"<color={UIKit.HexSafe}>✓ COMPLETED</color>")
                     : l.attempts > 0 ? $"<color={UIKit.HexWarning}>IN PROGRESS</color>" : $"<color={UIKit.HexSecondary}>NOT PLAYED</color>";
        Line(card, state, 16f, UIKit.TextPrimary, true);
        Line(card, l.bestScore >= 0 ? $"Best  <b>{l.bestScore}</b>{(l.maxScore > 0 ? " / " + l.maxScore : "")}" : "Best  -", 19f, UIKit.TextSecondary);
        Line(card, $"Attempts  <b>{l.attempts}</b>", 19f, UIKit.TextSecondary);
        if (!string.IsNullOrEmpty(l.lastPlayed)) Line(card, "Last  " + M.ShortDate(l.lastPlayed), 17f, Muted);
    }

    void RenderLocalLevels(RectTransform parent)
    {
        var lvCard = UICharts.Card(parent, "Progress on this PC");
        var lvRow = RowH(lvCard, 196);
        foreach (var l in MergedLevels()) LevelCard(lvRow, l);
    }

    void StudentResults()
    {
        var card = UICharts.Card(content, "Every attempt", "Newest first. Each finished level is recorded once per attempt - replays add a new attempt, they never overwrite one.");
        var pending = GameResultLog.PendingFor(LocalDataManager.ProfileId);
        var titles = (records?.activities ?? CatalogActivities()).ToDictionary(a => a.level_id, a => a);
        foreach (var p in Enumerable.Reverse(pending))
        {
            titles.TryGetValue(p.level_id, out var act);
            AttemptCard(card, act, "UPLOADING", p.score, p.max_score, p.correct_answers, p.incorrect_answers, p.passed, p.completed_at,
                        "Waiting to upload - it will appear with its attempt number shortly.");
        }
        var attempts = records != null ? records.attempts : new List<M.AttemptRow>();
        if (attempts.Count == 0 && pending.Count == 0) { UICharts.Empty(card, "No results yet. Finish a level and it appears here automatically."); return; }
        var byRun = (records?.answers ?? new List<M.AnswerRow>()).GroupBy(a => a.result_id).ToDictionary(g => g.Key, g => g.ToList());
        int shown = 0;
        foreach (var a in attempts.OrderByDescending(x => x.completed_at, StringComparer.Ordinal).ThenByDescending(x => x.id))
        {
            if (++shown > 60) { Line(card, $"Showing the latest 60 of {attempts.Count} attempts.", Small, Muted); break; }
            titles.TryGetValue(a.level_id, out var act);
            byRun.TryGetValue(a.id, out var ans);
            AttemptCard(card, act, "ATTEMPT " + a.attempt_number, a.score, a.max_score, a.correct_answers, a.incorrect_answers, a.passed, a.completed_at,
                        CA.Feedback(a, ans ?? new List<M.AnswerRow>(), records.questions));
        }
    }

    void AttemptCard(RectTransform parent, M.ActivityRow act, string tag, int score, int max, int correct, int incorrect, bool passed, string when, string feedback)
    {
        var row = UIKit.Rect("Attempt", parent);
        UIKit.AddImage(row, new Color(0.08f, 0.12f, 0.2f, 1f), rounded: true).raycastTarget = false;
        var v = UIKit.Vertical(row, 16, 4);
        v.padding.left = 22;
        float pct = max > 0 ? (float)score / max : 0f;
        string title = act != null ? $"Level {act.level_number}  ·  {Esc(act.title)}" : "Level";
        Line(row, $"<b>{title}</b>   <color={UIKit.HexAccent}>{tag}</color>", 21f, UIKit.TextPrimary);
        Line(row, $"Score <b>{score}</b> / {max}  (<color={Hex(UICharts.ScoreColor(pct))}>{UICharts.Percent(pct)}</color>)   ·   " +
                  $"<color={UIKit.HexSafe}>{correct} correct</color>   ·   <color={UIKit.HexDanger}>{incorrect} incorrect</color>   ·   " +
                  (passed ? $"<color={UIKit.HexSafe}>PASSED</color>" : $"<color={UIKit.HexWarning}>NOT PASSED</color>") +
                  $"   ·   {M.DateTimeText(when)}", 19f, UIKit.TextSecondary);
        if (!string.IsNullOrEmpty(feedback)) Line(row, feedback, 18f, Muted);
    }

    void StudentTopics()
    {
        var answers = records != null ? records.answers : new List<M.AnswerRow>();
        var catalog = records != null ? records.questions : new List<M.QuestionRow>();
        var weak = CA.WeakTopics(answers, catalog, 5);
        var card = UICharts.Card(content, "Topics to review", "Topics where at least a third of your first answers were wrong (worst first).");
        if (answers.Count == 0) UICharts.Empty(card, "Play a level to see which topics to review.");
        else if (weak.Count == 0) UICharts.Empty(card, "Nothing to review - you answered most questions in every topic correctly. Nice work!");
        else
        {
            UICharts.HorizontalBars(card, weak.Select(t => new UICharts.Item(t.topic, t.MissRate, $"{t.wrong} of {t.answered} missed", UIKit.Danger)).ToList(), 640f);
            var byId = catalog.ToDictionary(q => q.id, q => q);
            foreach (var t in weak)
            {
                var objectives = t.questionIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                var levels = objectives.Select(q => q.level_id.Replace("level_", "Level ")).Distinct();
                Line(card, $"<color={UIKit.HexAccent}><b>{Esc(t.topic)}</b></color>  <color={UIKit.HexSecondary}>({Esc(t.course)} · replay {string.Join(", ", levels)})</color>\n" +
                           string.Join("\n", objectives.Select(q => "•  " + Esc(q.learning_objective))), 19f, UIKit.TextPrimary);
            }
        }
        var all = CA.Topics(answers, catalog);
        if (all.Count > 0)
        {
            var allCard = UICharts.Card(content, "All topics you've practised");
            UICharts.HorizontalBars(allCard, all.Select(t => new UICharts.Item(t.topic, 1f - t.MissRate, $"{t.answered - t.wrong} / {t.answered} right",
                                                                               UICharts.ScoreColor(1f - t.MissRate))).ToList(), 640f);
        }
    }

    void StudentAchievements()
    {
        var defs = CareerSystem.Config.achievements;
        int got = LocalDataManager.Achievements.Count;
        var card = UICharts.Card(content, $"Achievements  {got} / {defs.Count}", "Earned by what you do in cases. They're saved with your account.");
        var grid = UIKit.Rect("Grid", card);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        float w = (content.rect.width > 0 ? content.rect.width : 1600f) - 48f;
        g.cellSize = new Vector2(Mathf.Floor((w - 16f) / 2f), 92f);
        g.spacing = new Vector2(16f, 10f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;
        UIKit.Size(grid, -1, Mathf.Ceil(defs.Count / 2f) * 102f);
        foreach (var a in defs.OrderByDescending(a => LocalDataManager.HasAchievement(a.id)))
        {
            bool on = LocalDataManager.HasAchievement(a.id);
            var cell = UIKit.Rect("Ach_" + a.id, grid);
            UIKit.AddImage(cell, on ? new Color(0.1f, 0.16f, 0.27f, 1f) : new Color(0.07f, 0.1f, 0.16f, 1f), rounded: true).raycastTarget = false;
            var t = UIKit.Text(cell, "Text", $"{(on ? $"<color={UIKit.HexWarning}>★</color>" : $"<color={UIKit.HexSecondary}>☆</color>")}  <b>{Esc(a.title)}</b>" +
                                             $"{(on ? "" : $"  <color={UIKit.HexSecondary}>· locked</color>")}\n<size=80%><color={UIKit.HexSecondary}>{Esc(a.description)}</color></size>",
                               20f, on ? UIKit.TextPrimary : Muted);
            UIKit.Stretch(t.rectTransform);
            t.margin = new Vector4(18, 10, 14, 8);
        }
    }

    async void JoinClass()
    {
        if (joinField == null) return;
        string problem = ClassCode.Problem(joinField.text);
        if (problem != null) { ShowFieldError(joinError, problem); return; }
        SetStatus("Joining…", false);
        int v = version;
        try
        {
            var res = await ClassroomService.JoinClassAsync(joinField.text);
            if (!Current(v)) return;
            if (!res.ok) { ShowFieldError(joinError, res.error); SetStatus("", false); return; }
            Reload();
            SetStatus($"You joined {res.class_name}. Your results now go to {res.teacher_name}'s class record.", false);
        }
        catch (Exception e) { if (Current(v)) { ShowFieldError(joinError, ClassroomService.Message(e)); SetStatus("", false); } }
    }

    async void LeaveClass()
    {
        if (!confirmLeave) { confirmLeave = true; Render(); return; }
        confirmLeave = false;
        int v = version;
        try
        {
            await ClassroomService.LeaveClassAsync();
            if (!Current(v)) return;
            Reload();
            SetStatus("You left the class. Your progress and results are kept.", false);
        }
        catch (Exception e) { if (Current(v)) SetStatus(ClassroomService.Message(e), true); }
    }

    async void ClaimTeacher()
    {
        if (teacherCodeField == null) return;
        if (string.IsNullOrWhiteSpace(teacherCodeField.text)) { ShowFieldError(teacherError, "Enter the teacher code from your school."); return; }
        int v = version;
        try
        {
            var res = await ClassroomService.ClaimTeacherAsync(teacherCodeField.text);
            if (!Current(v)) return;
            if (!res.ok) { ShowFieldError(teacherError, res.error); return; }
            tab = Tab.Classes;
            Reload();
            SetStatus("Teacher portal activated. Create your first class to get a class code.", false);
        }
        catch (Exception e) { if (Current(v)) ShowFieldError(teacherError, ClassroomService.Message(e)); }
    }

    // ---------- Teacher portal ----------

    M.ClassInfo CurrentClass => classes.FirstOrDefault(c => c.id == currentClassId);

    void RenderTeacher()
    {
        bool teacher = IsTeacherRole;
        string who = (svc != null ? svc.DisplayName : "Preview") + "  ·  " + (teacher && portalAdmin ? "Teacher and admin" : teacher ? "Teacher" : "Admin");
        if (!teacher)
        {
            // Admin who isn't a teacher: question management only.
            titleText.text = "ADMIN PORTAL";
            subtitleText.text = who;
            if (tab != Tab.Questions && tab != Tab.Requests) tab = Tab.Requests;
            TabButton("REQUESTS", Tab.Requests);
            TabButton("QUESTIONS", Tab.Questions);
            if (tab == Tab.Requests) QuestionRequests(); else TeacherQuestions();
            return;
        }

        titleText.text = portalAdmin ? "TEACHER PORTAL  ·  ADMIN" : "TEACHER PORTAL";
        var cls = CurrentClass;
        subtitleText.text = who + (cls != null ? $"  ·  <color={UIKit.HexAccent}>{Esc(cls.name)}</color>  ·  code <b>{ClassCode.Display(cls.code)}</b>" +
                                                 (cls.archived ? "  <color=#FBBF24>(archived)</color>" : "") : "");
        if ((cls == null || data == null) && tab != Tab.Questions && tab != Tab.Requests) tab = Tab.Classes;
        TabButton("DASHBOARD", Tab.Dashboard, cls != null);
        TabButton("STUDENTS", Tab.Students, cls != null);
        TabButton("ANALYTICS", Tab.Analytics, cls != null);
        TabButton("REPORTS", Tab.Reports, cls != null);
        TabButton("CLASSES", Tab.Classes);
        TabButton("QUESTIONS", Tab.Questions);   // the question bank is shared by every class, so no class is needed
        TabButton(portalAdmin ? "REQUESTS" : "MY REQUESTS", Tab.Requests);
        if (classes.Count > 1)
        {
            var spacer = UIKit.Rect("Spacer", tabBar);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var prev = Btn(tabBar, "◀", () => SwitchClass(-1), UIKit.HudVariant.Secondary, 70, 56, 22);
            prev.name = "PrevClass";
            var next = Btn(tabBar, "NEXT  ▶", () => SwitchClass(1), UIKit.HudVariant.Secondary, 150, 56, 20);
            next.name = "NextClass";
        }

        switch (tab)
        {
            case Tab.Requests: QuestionRequests(); break;
            case Tab.Students: if (selectedStudent != null) TeacherStudentDetail(); else TeacherStudents(); break;
            case Tab.Analytics: TeacherAnalytics(); break;
            case Tab.Reports: TeacherReports(); break;
            case Tab.Classes: TeacherClasses(); break;
            case Tab.Questions: TeacherQuestions(); break;
            default: TeacherDashboard(); break;
        }
    }

    void TeacherDashboard()
    {
        var s = CA.Dashboard(data, DateTime.UtcNow);
        var banner = UICharts.Card(content, data.info.name);
        Line(banner, $"CLASS CODE   <color={UIKit.HexAccent}><size=150%><b>{ClassCode.Display(data.info.code)}</b></size></color>", 26f, UIKit.TextPrimary);
        Line(banner, data.info.archived
            ? "This class is archived: nobody can join it, and its records are kept."
            : "Students join from CLASSROOM > JOIN A CLASS in the game menu. Their finished levels are recorded here automatically.", Small, Muted);

        var tiles = RowH(content, 124);
        UICharts.StatTile(tiles, s.enrolled.ToString(), "Students", null, "enrolled now");
        UICharts.StatTile(tiles, s.active.ToString(), "Active", UIKit.Accent, $"played in the last {CA.ActiveDays} days");
        UICharts.StatTile(tiles, s.completed.ToString(), "Completed", UIKit.Safe, "finished every level");
        UICharts.StatTile(tiles, s.notStarted.ToString(), "Not started", s.notStarted > 0 ? UIKit.Warning : (Color?)null, "no results yet");
        UICharts.StatTile(tiles, UICharts.Percent(s.classAverage), "Class average", UICharts.ScoreColor(s.classAverage), "average best score");
        UICharts.StatTile(tiles, UICharts.Percent(s.enrolled > 0 ? s.completionRate : -1f), "Completion rate", null, "levels completed");

        if (s.enrolled == 0) { UICharts.Empty(content, $"No students yet. Give your class the code {ClassCode.Display(data.info.code)}."); return; }
        var row = RowH(content, 380, 18);
        var lc = UICharts.Card(row, "Level completion", "Students who finished each level");
        var levels = CA.Levels(data);
        UICharts.VerticalBars(lc, levels.Select(l => new UICharts.Item("L" + l.number, l.completionRate, $"{l.studentsCompleted}/{s.enrolled}")).ToList(), 270f);

        var attention = UICharts.Card(row, "Needs attention");
        var students = CA.Students(data);
        var flagged = students.Where(st => st.attempts == 0 || (st.averageBest >= 0 && st.averageBest < 0.5f))
                              .OrderBy(st => st.averageBest).Take(6).ToList();
        if (flagged.Count == 0) Line(attention, "Every student has results and an average best score of 50% or more.", Small, Muted);
        foreach (var st in flagged)
            Line(attention, st.attempts == 0 ? $"•  <b>{Esc(st.name)}</b>  <color={UIKit.HexWarning}>hasn't finished a level yet</color>"
                                             : $"•  <b>{Esc(st.name)}</b>  average best <color={UIKit.HexDanger}>{UICharts.Percent(st.averageBest)}</color>", 20f, UIKit.TextPrimary);
        var topics = CA.Topics(EnrolledAnswers(), data.questions).Where(t => t.wrong > 0).Take(3).ToList();
        if (topics.Count > 0)
        {
            UIKit.Header(attention, "Most-missed topics", 17f);
            foreach (var t in topics) Line(attention, $"•  {Esc(t.topic)}  <color={UIKit.HexDanger}>{UICharts.Percent(t.MissRate)} missed</color>", 19f, UIKit.TextSecondary);
        }
    }

    List<M.AnswerRow> EnrolledAnswers()
    {
        var ids = new HashSet<string>(CA.Enrolled(data).Select(m => m.student_id));
        return data.answers.Where(a => ids.Contains(a.user_id)).ToList();
    }

    static readonly float[] RosterCols = { -1f, 170f, 110f, 130f, 130f, 180f, 200f, 330f };

    void TeacherStudents()
    {
        var enrolled = CA.Students(data);
        var card = UICharts.Card(content, $"Students  ({enrolled.Count})", $"Class code {ClassCode.Display(data.info.code)}. Removed students lose access to this class and leave its numbers; their records are kept.");
        if (enrolled.Count == 0) UICharts.Empty(card, "No students yet. Share the class code so they can join.");
        else
        {
            TableRow(card, RosterCols, true, "STUDENT", "STATUS", "LEVELS", "ATTEMPTS", "AVG BEST", "RIGHT / WRONG", "LAST ACTIVE", "");
            int levels = data.activities.Count;
            foreach (var st in enrolled)
            {
                string status = st.Status(levels);
                string statusText = status == "Completed" ? $"<color={UIKit.HexSafe}>{status}</color>" : status == "Not started" ? $"<color={UIKit.HexWarning}>{status}</color>" : status;
                var row = TableRow(card, RosterCols, false, Esc(st.name), statusText, $"{st.levelsCompleted} / {levels}", st.attempts.ToString(),
                                   $"<color={Hex(UICharts.ScoreColor(st.averageBest))}>{UICharts.Percent(st.averageBest)}</color>",
                                   $"<color={UIKit.HexSafe}>{st.correct}</color> / <color={UIKit.HexDanger}>{st.incorrect}</color>",
                                   st.lastActive.HasValue ? st.lastActive.Value.ToLocalTime().ToString("MMM d, HH:mm") : "-", null);
                var actions = row.GetChild(row.childCount - 1);
                var h = UIKit.Horizontal((RectTransform)actions, 10, TextAnchor.MiddleRight);
                h.childForceExpandWidth = false;
                string id = st.id;
                Btn(actions, "VIEW", () => { selectedStudent = id; detailProgress = null; detailAchievements = null; Render(); LoadDetail(id); }, UIKit.HudVariant.Primary, 140, 46, 18);
                bool confirming = confirmRemove == id;
                Btn(actions, confirming ? "CONFIRM" : "REMOVE", () => RemoveStudent(id), confirming ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, 170, 46, 18);
            }
        }

        var removed = data.members.Where(m => !M.IsActive(m)).ToList();
        if (removed.Count > 0)
        {
            var rc = UICharts.Card(content, $"Left or removed  ({removed.Count})");
            foreach (var m in removed)
            {
                var row = TableRow(rc, new[] { -1f, 420f, 260f }, false, Esc(data.NameOf(m.student_id)),
                                   (m.removed_by == "teacher" ? "Removed by you" : "Left the class") + " · " + M.ShortDate(m.removed_at), null);
                if (m.removed_by == "teacher")
                {
                    string id = m.student_id;
                    var cell = row.GetChild(row.childCount - 1);
                    var h = UIKit.Horizontal((RectTransform)cell, 10, TextAnchor.MiddleRight);
                    h.childForceExpandWidth = false;
                    Btn(cell, "RE-ADMIT", () => Readmit(id), UIKit.HudVariant.Secondary, 200, 46, 18);
                }
            }
        }
    }

    async void LoadDetail(string studentId)
    {
        int v = version;
        try
        {
            var (p, a) = await ClassroomService.LoadStudentProgressAsync(studentId);
            if (!Current(v) || selectedStudent != studentId) return;
            detailProgress = p; detailAchievements = a;
            Render();
        }
        catch (Exception e) { if (Current(v)) SetStatus(ClassroomService.Message(e), true); }
    }

    void TeacherStudentDetail()
    {
        var st = CA.Student(data, selectedStudent);
        var head = RowH(content, 60);
        Btn(head, "◀  BACK TO STUDENTS", () => { selectedStudent = null; Render(); }, UIKit.HudVariant.Secondary, 380, 56, 20);
        bool confirming = confirmRemove == st.id;
        Btn(head, confirming ? "CONFIRM REMOVE" : "REMOVE FROM CLASS", () => RemoveStudent(st.id), confirming ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, 380, 56, 20);

        var info = UICharts.Card(content, st.name);
        Line(info, $"Status <b>{st.Status(data.activities.Count)}</b>   ·   joined the class {M.ShortDate(st.joinedAt)}   ·   last active " +
                   (st.lastActive.HasValue ? st.lastActive.Value.ToLocalTime().ToString("MMM d, yyyy HH:mm") : "-") +
                   (detailAchievements != null ? $"   ·   achievements <b>{detailAchievements.Count}</b>" : "   ·   achievements …"), 20f, UIKit.TextSecondary);
        Line(info, "Teachers see display names and game results only - never email addresses.", 17f, Muted);

        var tiles = RowH(content, 110);
        UICharts.StatTile(tiles, $"{st.levelsCompleted} / {data.activities.Count}", "Levels completed");
        UICharts.StatTile(tiles, st.attempts.ToString(), "Attempts");
        UICharts.StatTile(tiles, st.correct.ToString(), "Correct answers", UIKit.Safe);
        UICharts.StatTile(tiles, st.incorrect.ToString(), "Incorrect answers", UIKit.Danger);
        UICharts.StatTile(tiles, UICharts.Percent(st.averageBest), "Average best", UICharts.ScoreColor(st.averageBest));

        var row = RowH(content, 140f + data.activities.Count * 68f, 18);   // header + one table row per level
        var lv = UICharts.Card(row, "Levels");
        var mine = data.attempts.Where(a => a.user_id == st.id).ToList();
        var cols = new[] { -1f, 130f, 150f, 150f, 210f };
        TableRow(lv, cols, true, "LEVEL", "ATTEMPTS", "BEST", "LAST", "STATUS");
        foreach (var l in CA.LevelRecords(mine, data.activities))
        {
            var saved = detailProgress?.FirstOrDefault(p => p.level_id == l.levelId);
            string status = l.completed ? (l.passed ? $"<color={UIKit.HexSafe}>Passed</color>" : "Completed")
                          : saved != null && saved.completed ? "Done before joining" : l.attempts > 0 ? "In progress" : $"<color={UIKit.HexSecondary}>Not played</color>";
            TableRow(lv, cols, false, $"{l.number}. {Esc(l.title)}", l.attempts.ToString(), l.bestScore >= 0 ? $"{l.bestScore} / {l.maxScore}" : "-",
                     l.lastScore >= 0 ? l.lastScore.ToString() : "-", status);
        }
        var weakCard = UICharts.Card(row, "Struggling topics");
        if (st.weakTopics.Count == 0) Line(weakCard, mine.Count == 0 ? "No results yet." : "No topic missed a third of the time or more.", Small, Muted);
        else UICharts.HorizontalBars(weakCard, st.weakTopics.Select(t => new UICharts.Item(t.topic, t.MissRate, $"{t.wrong}/{t.answered} missed", UIKit.Danger)).ToList(), 330f);

        var hist = UICharts.Card(content, "History", "Every attempt in this class, newest first, with the questions answered wrongly first time.");
        if (mine.Count == 0) UICharts.Empty(hist, "No results in this class yet.");
        var titles = data.activities.ToDictionary(a => a.level_id, a => a);
        var byRun = data.answers.Where(a => a.user_id == st.id).GroupBy(a => a.result_id).ToDictionary(g => g.Key, g => g.ToList());
        var qs = data.questions.ToDictionary(q => q.id, q => q);
        foreach (var a in mine.OrderByDescending(x => x.completed_at, StringComparer.Ordinal).ThenByDescending(x => x.id).Take(50))
        {
            titles.TryGetValue(a.level_id, out var act);
            byRun.TryGetValue(a.id, out var ans);
            var missed = (ans ?? new List<M.AnswerRow>()).Where(x => !x.correct)
                         .Select(x => x.question_id + (qs.TryGetValue(x.question_id, out var q) ? " " + q.topic : "") + (x.recovered ? " (recovered)" : "")).ToList();
            AttemptCard(hist, act, "ATTEMPT " + a.attempt_number, a.score, a.max_score, a.correct_answers, a.incorrect_answers, a.passed, a.completed_at,
                        missed.Count > 0 ? "Missed: " + string.Join("  ·  ", missed.Select(Esc)) : (ans != null && ans.Count > 0 ? "No questions missed." : ""));
        }
    }

    async void RemoveStudent(string id)
    {
        if (confirmRemove != id) { confirmRemove = id; Render(); return; }
        confirmRemove = null;
        int v = version;
        try
        {
            await ClassroomService.RemoveStudentAsync(data.info.id, id);
            if (!Current(v)) return;
            string name = data.NameOf(id);
            selectedStudent = null;
            await OpenClass(data.info.id, Tab.Students);
            SetStatus($"{name} was removed from the class. RE-ADMIT brings them back.", false);
        }
        catch (Exception e) { if (Current(v)) SetStatus(ClassroomService.Message(e), true); }
    }

    async void Readmit(string id)
    {
        int v = version;
        try
        {
            await ClassroomService.ReadmitStudentAsync(data.info.id, id);
            if (!Current(v)) return;
            string name = data.NameOf(id);
            await OpenClass(data.info.id, Tab.Students);
            SetStatus($"{name} is back in the class.", false);
        }
        catch (Exception e) { if (Current(v)) SetStatus(ClassroomService.Message(e), true); }
    }

    void TeacherAnalytics()
    {
        var levels = CA.Levels(data);
        var answers = EnrolledAnswers();
        var attempts = CA.EnrolledAttempts(data);
        if (attempts.Count == 0) { UICharts.Empty(content, "No results in this class yet. Charts appear as soon as students finish levels."); }

        var r1 = RowH(content, 360, 18);
        var c1 = UICharts.Card(r1, "Class average by level", "Average best score of students who finished the level");
        UICharts.VerticalBars(c1, levels.Select(l => new UICharts.Item("L" + l.number, Mathf.Max(0f, l.averageBest), UICharts.Percent(l.averageBest),
                                                                       UICharts.ScoreColor(l.averageBest))).ToList(), 250f);
        var c2 = UICharts.Card(r1, "Level completion", "Share of enrolled students who finished each level");
        UICharts.VerticalBars(c2, levels.Select(l => new UICharts.Item("L" + l.number, l.completionRate, UICharts.Percent(l.completionRate))).ToList(), 250f);

        var students = CA.Students(data).Where(s => s.attempts > 0).OrderBy(s => s.averageBest).ToList();
        var points = CA.OverTime(attempts);
        var r2 = RowH(content, Mathf.Max(360f, 110f + Mathf.Min(14, students.Count) * 46f), 18);
        var c3 = UICharts.Card(r2, "Results by student", "Average best score, lowest first");
        if (students.Count == 0) UICharts.Empty(c3, "No student has results yet.");
        else UICharts.HorizontalBars(c3, students.Take(14).Select(s => new UICharts.Item(s.name, s.averageBest, UICharts.Percent(s.averageBest), UICharts.ScoreColor(s.averageBest))).ToList(), 260f);
        if (students.Count > 14) Line(c3, $"+ {students.Count - 14} more in REPORTS.", 17f, Muted);
        var c4 = UICharts.Card(r2, "Progress over time", "Average score of the attempts finished each day");
        if (points.Count == 0) UICharts.Empty(c4, "No results yet.");
        else UICharts.LineChart(c4, points.Select(p => new UICharts.Item(p.label, p.average, UICharts.Percent(p.average))).ToList(), 250f);

        var qstats = CA.Questions(answers, data.questions).Where(q => q.answered > 0).Take(8).ToList();
        var tstats = CA.Topics(answers, data.questions).Take(8).ToList();
        var r3 = RowH(content, 110f + Mathf.Max(1, Mathf.Max(qstats.Count, tstats.Count)) * 46f, 18);
        var c5 = UICharts.Card(r3, "Hardest questions", "Wrong first answers, highest miss rate first");
        if (qstats.Count == 0) UICharts.Empty(c5, "No answers recorded yet.");
        else UICharts.HorizontalBars(c5, qstats.Select(q => new UICharts.Item($"{q.questionId} · L{q.levelId.Replace("level_", "")} · {q.topic}", q.MissRate,
                                                                                $"{q.wrong}/{q.answered} missed", UIKit.Danger)).ToList(), 420f);
        var c6 = UICharts.Card(r3, "Most-missed topics");
        if (tstats.Count == 0) UICharts.Empty(c6, "No answers recorded yet.");
        else UICharts.HorizontalBars(c6, tstats.Select(t => new UICharts.Item(t.topic, t.MissRate, $"{t.wrong}/{t.answered} missed", UIKit.Danger)).ToList(), 420f);
    }

    void TeacherReports()
    {
        var students = CA.Students(data);
        if (reportStudent >= students.Count) reportStudent = -1;
        var levels = data.activities.OrderBy(a => a.level_number).ToList();
        if (reportLevel >= levels.Count) reportLevel = -1;
        var filter = new CA.ReportFilter
        {
            studentId = reportStudent >= 0 ? students[reportStudent].id : null,
            levelId = reportLevel >= 0 ? levels[reportLevel].level_id : null,
        };

        var card = UICharts.Card(content, "Report", "Choose what to include, then export a CSV file (opens in Excel or Google Sheets).");
        var f1 = RowH(card, 58);
        Label(f1, "STUDENT", 170, 18f, UIKit.Accent, true);
        Btn(f1, "◀", () => { reportStudent = Step(reportStudent, -1, students.Count); Render(); }, UIKit.HudVariant.Secondary, 70, 52, 20);
        var sName = Label(f1, reportStudent >= 0 ? Esc(students[reportStudent].name) : "All students", 420, 21f, UIKit.TextPrimary, true);
        sName.alignment = TextAlignmentOptions.Center;
        Btn(f1, "▶", () => { reportStudent = Step(reportStudent, 1, students.Count); Render(); }, UIKit.HudVariant.Secondary, 70, 52, 20);

        var f2 = RowH(card, 58);
        Label(f2, "ACTIVITY", 170, 18f, UIKit.Accent, true);
        Btn(f2, "◀", () => { reportLevel = Step(reportLevel, -1, levels.Count); Render(); }, UIKit.HudVariant.Secondary, 70, 52, 20);
        var lName = Label(f2, reportLevel >= 0 ? $"Level {levels[reportLevel].level_number} · {Esc(levels[reportLevel].title)}" : "All levels", 420, 21f, UIKit.TextPrimary, true);
        lName.alignment = TextAlignmentOptions.Center;
        Btn(f2, "▶", () => { reportLevel = Step(reportLevel, 1, levels.Count); Render(); }, UIKit.HudVariant.Secondary, 70, 52, 20);

        var f3 = RowH(card, 58);
        Label(f3, "ROWS", 170, 18f, UIKit.Accent, true);
        Btn(f3, (reportAnswers ? "" : "›  ") + "ONE PER ATTEMPT", () => { reportAnswers = false; Render(); }, reportAnswers ? UIKit.HudVariant.Secondary : UIKit.HudVariant.Primary, 330, 52, 19);
        Btn(f3, (reportAnswers ? "›  " : "") + "ONE PER QUESTION ANSWER", () => { reportAnswers = true; Render(); }, reportAnswers ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, 420, 52, 19);

        var attempts = CA.FilterAttempts(data, filter);
        var runIds = new HashSet<long>(attempts.Select(a => a.id));
        int rows = reportAnswers ? data.answers.Count(x => runIds.Contains(x.result_id)) : attempts.Count;
        var f4 = RowH(card, 62);
        Btn(f4, "EXPORT CSV", () => Export(filter), UIKit.HudVariant.Primary, 300, 58, 22);
        if (lastExportFolder != null) Btn(f4, "OPEN FOLDER", OpenExportFolder, UIKit.HudVariant.Secondary, 280, 58, 22);
        Line(f4, $"<b>{rows}</b> row{(rows == 1 ? "" : "s")} match", 20f, UIKit.TextSecondary);

        var preview = UICharts.Card(content, "Preview", "The first rows of the report");
        if (attempts.Count == 0) { UICharts.Empty(preview, "Nothing matches these filters yet."); return; }
        var titles = data.activities.ToDictionary(a => a.level_id, a => a.title);
        if (!reportAnswers)
        {
            var cols = new[] { -1f, 230f, 120f, 170f, 150f, 150f, 130f, 240f };
            TableRow(preview, cols, true, "STUDENT", "LEVEL", "ATTEMPT", "SCORE", "CORRECT", "WRONG", "PASSED", "FINISHED");
            foreach (var a in attempts.Take(15))
                TableRow(preview, cols, false, Esc(data.NameOf(a.user_id)), titles.TryGetValue(a.level_id, out var t) ? $"L{a.level_id.Replace("level_", "")}" : a.level_id,
                         a.attempt_number.ToString(), $"{a.score} / {a.max_score}", a.correct_answers.ToString(), a.incorrect_answers.ToString(),
                         a.passed ? "yes" : "no", M.DateTimeText(a.completed_at));
        }
        else
        {
            var runs = attempts.ToDictionary(a => a.id, a => a);
            var qs = data.questions.ToDictionary(q => q.id, q => q);
            var cols = new[] { 300f, 90f, 120f, 120f, -1f, 140f, 150f };
            TableRow(preview, cols, true, "STUDENT", "LEVEL", "ATTEMPT", "QUESTION", "TOPIC", "FIRST TRY", "RECOVERED");
            foreach (var x in data.answers.Where(x => runIds.Contains(x.result_id)).Take(15))
                TableRow(preview, cols, false, Esc(data.NameOf(x.user_id)), "L" + x.level_id.Replace("level_", ""), runs[x.result_id].attempt_number.ToString(),
                         x.question_id, qs.TryGetValue(x.question_id, out var q) ? Esc(q.topic) : "", x.correct ? $"<color={UIKit.HexSafe}>right</color>" : $"<color={UIKit.HexDanger}>wrong</color>",
                         x.recovered ? "yes" : "no");
        }
        if (rows > 15) Line(preview, $"+ {rows - 15} more rows in the CSV.", 17f, Muted);
    }

    static int Step(int index, int step, int count)
    {
        // -1 = "all", then 0..count-1
        int n = count + 1;
        return ((index + 1 + step) % n + n) % n - 1;
    }

    void Export(CA.ReportFilter filter)
    {
        try
        {
            string csv = reportAnswers ? CA.AnswersCsv(data, filter) : CA.AttemptsCsv(data, filter);
            string folder = ReportFolder();
            Directory.CreateDirectory(folder);
            string safe = new string(data.info.name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
            string file = Path.Combine(folder, $"{(string.IsNullOrEmpty(safe) ? "class" : safe)}_{(reportAnswers ? "answers" : "attempts")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(file, csv, new System.Text.UTF8Encoding(true));   // BOM: Excel reads the names correctly
            lastExportFolder = folder;
            Render();
            SetStatus("Saved " + file, false);
        }
        catch (Exception e) { SetStatus("Couldn't save the report: " + e.Message, true); }
    }

    static string ReportFolder()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(string.IsNullOrEmpty(docs) ? Application.persistentDataPath : docs, "Scam Sense Reports");
    }

    void OpenExportFolder()
    {
        if (lastExportFolder != null) Application.OpenURL("file:///" + lastExportFolder.Replace('\\', '/'));
    }

    void TeacherClasses()
    {
        var create = UICharts.Card(content, "Create a class", "Each class gets its own unique 6-character code for students to join with.");
        var row = RowH(create, 64);
        newClassField = Input(row, "ClassNameInput", "Class name, e.g. BSIT 1-A Computer Security", 60, 640);
        newClassField.onSubmit.AddListener(_ => CreateClass());
        Btn(row, "CREATE CLASS", CreateClass, UIKit.HudVariant.Primary, 300);
        newClassError = Line(create, "", Small, UIKit.Danger);
        newClassError.gameObject.SetActive(false);

        int archivedCount = classes.Count(c => c.archived);
        var list = UICharts.Card(content, $"Your classes  ({classes.Count - archivedCount} open" + (archivedCount > 0 ? $", {archivedCount} archived)" : ")"));
        if (classes.Count == 0) { UICharts.Empty(list, "No classes yet. Create one above."); return; }
        if (archivedCount > 0)
        {
            var toggle = RowH(list, 50);
            Line(toggle, showArchived ? "Showing archived classes too." : $"{archivedCount} archived class{(archivedCount == 1 ? " is" : "es are")} hidden.", 17f, Muted);
            Btn(toggle, showArchived ? "HIDE ARCHIVED" : $"SHOW ARCHIVED ({archivedCount})", () => { showArchived = !showArchived; confirmDeleteClass = null; Render(); },
                UIKit.HudVariant.Secondary, 330, 48, 18).name = "ToggleArchived";
        }
        var shown = classes.Where(c => showArchived || !c.archived || c.id == confirmDeleteClass).ToList();
        if (shown.Count == 0) UICharts.Empty(list, "All your classes are archived. Press SHOW ARCHIVED to see them.");
        var cols = new[] { -1f, 200f, 200f, 170f, 650f };
        if (shown.Count > 0) TableRow(list, cols, true, "CLASS", "CODE", "CREATED", "STATUS", "");
        foreach (var c in shown)
        {
            bool current = c.id == currentClassId;
            var r = TableRow(list, cols, false, (current ? $"<color={UIKit.HexAccent}>›</color> " : "") + Esc(c.name),
                             $"<b>{ClassCode.Display(c.code)}</b>", M.ShortDate(c.created_at),
                             c.archived ? $"<color={UIKit.HexWarning}>Archived</color>" : $"<color={UIKit.HexSafe}>Open</color>", null);
            var cell = r.GetChild(r.childCount - 1);
            var h = UIKit.Horizontal((RectTransform)cell, 10, TextAnchor.MiddleRight);
            h.childForceExpandWidth = false;
            string id = c.id;
            Btn(cell, "OPEN", () => { confirmDeleteClass = null; _ = OpenClass(id, Tab.Dashboard); }, UIKit.HudVariant.Primary, 140, 46, 18);
            bool confirming = confirmArchive == id;
            string label = c.archived ? "RESTORE" : confirming ? "CONFIRM ARCHIVE" : "ARCHIVE";
            Btn(cell, label, () => ToggleArchive(id), confirming ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, confirming ? 300 : 190, 46, 18);
            bool deleting = confirmDeleteClass == id;
            var del = Btn(cell, deleting ? "CONFIRM DELETE" : "DELETE", () => DeleteClass(id), deleting ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, deleting ? 290 : 160, 46, 18);
            del.name = "Delete_" + c.code;
        }
        var pending = classes.FirstOrDefault(x => x.id == confirmDeleteClass);
        if (pending != null)
            Line(list, $"<color={UIKit.HexWarning}>Delete <b>{Esc(pending.name)}</b>? Its class code stops working and any students in it are removed from it. " +
                       "This can't be undone. Press CONFIRM DELETE, or anything else to cancel.</color>", Small, UIKit.TextPrimary).name = "DeleteWarning";
        Line(list, "DELETE removes a class that has no results yet (a test class, a typo). A class with results can't be deleted, because student records are kept. " +
                   "ARCHIVE it instead: the code stops working, its records stay, it's hidden from this list, and RESTORE reopens it.", 17f, Muted);
    }

    async void DeleteClass(string id)
    {
        var c = classes.FirstOrDefault(x => x.id == id);
        if (c == null) return;
        confirmArchive = null;
        if (confirmDeleteClass != id) { confirmDeleteClass = id; Render(); return; }
        confirmDeleteClass = null;
        int v = version;
        try
        {
            var res = await ClassroomService.DeleteClassAsync(id);
            if (!Current(v)) return;
            if (!res.ok) { Render(); SetStatus(res.error, true); return; }
            if (currentClassId == id) { currentClassId = null; data = null; }
            classes = await ClassroomService.ListClassesAsync();
            if (!Current(v)) return;
            if (currentClassId == null)
            {
                var next = classes.FirstOrDefault(x => !x.archived) ?? classes.FirstOrDefault();
                if (next != null) { currentClassId = next.id; data = await ClassroomService.LoadClassAsync(next); if (!Current(v)) return; }
            }
            tab = Tab.Classes;
            Render();
            SetStatus($"{c.name} was deleted." + (res.members_removed > 0 ? $" {res.members_removed} student{(res.members_removed == 1 ? " was" : "s were")} removed from it and can join another class." : ""), false);
        }
        catch (SupabaseException se) when (se.Status == 404 || (se.Code ?? "").Contains("PGRST202"))
        {
            if (Current(v)) { Render(); SetStatus("Deleting classes isn't set up on the server yet (developer: apply supabase/migrations/20261128044325_delete_class.sql).", true); }
        }
        catch (Exception e) { if (Current(v)) { Render(); SetStatus(ClassroomService.Message(e), true); } }
    }

    async void CreateClass()
    {
        if (newClassField == null) return;
        string name = newClassField.text.Trim();
        if (name.Length < 2 || name.Length > 60) { ShowFieldError(newClassError, "Class names are 2 to 60 characters long."); return; }
        int v = version;
        try
        {
            var c = await ClassroomService.CreateClassAsync(name);
            if (!Current(v)) return;
            classes = await ClassroomService.ListClassesAsync();
            if (!Current(v)) return;
            await OpenClass(c.id, Tab.Dashboard);
            SetStatus($"Class created. Its code is {ClassCode.Display(c.code)} - give it to your students.", false);
        }
        catch (Exception e) { if (Current(v)) ShowFieldError(newClassError, ClassroomService.Message(e)); }
    }

    async void ToggleArchive(string id)
    {
        var c = classes.FirstOrDefault(x => x.id == id);
        if (c == null) return;
        confirmDeleteClass = null;
        if (!c.archived && confirmArchive != id) { confirmArchive = id; Render(); return; }
        confirmArchive = null;
        int v = version;
        try
        {
            await ClassroomService.SetArchivedAsync(id, !c.archived);
            if (!Current(v)) return;
            classes = await ClassroomService.ListClassesAsync();
            if (!Current(v)) return;
            Render();
            SetStatus(c.archived ? $"{c.name} is open again." : $"{c.name} is archived. Its records are kept.", false);
        }
        catch (Exception e) { if (Current(v)) SetStatus(ClassroomService.Message(e), true); }
    }

    // ---------- Small builders ----------

    void TabButton(string label, Tab t, bool enabled = true)
    {
        bool on = tab == t;
        var b = Btn(tabBar, (on ? "›  " : "") + label, () => { if (tab == t) return; tab = t; selectedStudent = null; confirmRemove = confirmArchive = confirmDeleteClass = null; confirmLeave = false; openRequestId = null; Render(); },
                    on ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, Mathf.Max(180f, 50f + label.Length * 16f), 56, 20);
        b.name = "Tab_" + t;
        UIKit.SetInteractable(b, enabled);
    }

    Button Btn(Transform parent, string label, UnityAction onClick, UIKit.HudVariant variant, float width = -1f, float height = 58f, float font = 22f)
    {
        var b = UIKit.HudButton(parent, label, onClick, variant, font);
        UIKit.Size(b, width, height);
        if (menu != null) menu.AddFx(b);
        return b;
    }

    static RectTransform RowH(Transform parent, float height, float spacing = 14f)
    {
        var r = UIKit.Rect("Row", parent);
        var h = UIKit.Horizontal(r, spacing, TextAnchor.MiddleLeft);
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        UIKit.Size(r, -1, height);
        // Children without a fixed width share the rest.
        return r;
    }

    static TextMeshProUGUI Line(Transform parent, string text, float size, Color color, bool bold = false)
    {
        var t = UIKit.Text(parent, "Text", text, size, color, TextAlignmentOptions.MidlineLeft, bold);
        t.lineSpacing = 4f;
        var le = t.gameObject.AddComponent<LayoutElement>();
        if (parent.GetComponent<HorizontalLayoutGroup>() != null) le.flexibleWidth = 1f;
        return t;
    }

    static TextMeshProUGUI Label(Transform parent, string text, float width, float size, Color color, bool bold = false)
    {
        var t = Line(parent, text, size, color, bold);
        var le = t.GetComponent<LayoutElement>();
        le.flexibleWidth = 0f;
        le.preferredWidth = le.minWidth = width;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    TMP_InputField Input(Transform parent, string name, string placeholder, int limit, float width)
    {
        var f = AccountUI.Field((RectTransform)parent, name, placeholder, false);
        f.characterLimit = limit;
        UIKit.Size(f, width, 58);
        return f;
    }

    /// <summary>A table row: fixed-width cells (-1 = takes the rest). A null cell is an empty container for buttons.</summary>
    static RectTransform TableRow(Transform parent, float[] widths, bool header, params string[] cells)
    {
        var row = UIKit.Rect(header ? "Header" : "Row", parent);
        if (!header) UIKit.AddImage(row, new Color(0.08f, 0.12f, 0.2f, 1f), rounded: true).raycastTarget = false;
        var h = UIKit.Horizontal(row, 10, TextAnchor.MiddleLeft);
        h.padding = new RectOffset(16, 12, 0, 0);
        h.childForceExpandWidth = false;
        UIKit.Size(row, -1, header ? 36f : 58f);
        for (int i = 0; i < cells.Length; i++)
        {
            float w = i < widths.Length ? widths[i] : 120f;
            if (cells[i] == null)
            {
                var holder = UIKit.Rect("Actions", row);
                var le = holder.gameObject.AddComponent<LayoutElement>();
                if (w > 0) { le.preferredWidth = le.minWidth = w; } else le.flexibleWidth = 1f;
                continue;
            }
            var t = UIKit.Text(row, "Cell" + i, cells[i], header ? 16f : 20f, header ? Muted : UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, header);
            if (header) t.characterSpacing = 2f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            var e = t.gameObject.AddComponent<LayoutElement>();
            if (w > 0) { e.preferredWidth = e.minWidth = w; } else { e.flexibleWidth = 1f; e.minWidth = 160f; }
        }
        return row;
    }

    void ShowFieldError(TMP_Text t, string msg)
    {
        if (t == null) { SetStatus(msg, true); return; }
        t.text = msg ?? "";
        t.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    void SetStatus(string msg, bool error)
    {
        if (statusText == null) return;
        statusText.text = msg ?? "";
        statusText.color = error ? UIKit.Danger : UIKit.TextSecondary;
    }

    /// <summary>Player-typed text shown as rich text: tags are neutralised.</summary>
    static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "<​");

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    // ---------- Build (editor: into the scene; runtime: only as a fallback) ----------

#if UNITY_EDITOR
    /// <summary>Editor only: creates the classroom panel shell under the menu's panels and connects CLOSE / REFRESH.</summary>
    public void BuildIntoScene(MainMenuUI owner)
    {
        Build(owner.PanelHost, owner);
        builtInScene = true;
    }
#endif

    void Wire(Button b, UnityAction call)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEventTools.AddPersistentListener(b.onClick, call); return; }
#endif
        b.onClick.AddListener(call);
    }

    void Build(RectTransform host, MainMenuUI owner)
    {
        dim = UIKit.Dimmer(host, 0.86f).gameObject;
        dim.name = "ClassroomDim";

        panel = UIKit.Rect("ClassroomPanel", host);
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1760, 980));
        UIKit.PanelImage(panel);
        UIKit.AccentStrip(panel, null, 8f);
        panel.gameObject.AddComponent<CanvasGroup>();

        titleText = UIKit.Text(panel, "Title", "CLASSROOM", UIKit.SizeH1, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        titleText.characterSpacing = 6f;
        UIKit.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -30), new Vector2(900, 50));
        subtitleText = UIKit.Text(panel, "Subtitle", "", 22f, UIKit.TextSecondary, TextAlignmentOptions.MidlineLeft);
        subtitleText.rectTransform.anchorMin = new Vector2(0, 1); subtitleText.rectTransform.anchorMax = new Vector2(1, 1);
        subtitleText.rectTransform.pivot = new Vector2(0, 1);
        subtitleText.rectTransform.offsetMin = new Vector2(52, -116); subtitleText.rectTransform.offsetMax = new Vector2(-520, -80);
        subtitleText.textWrappingMode = TextWrappingModes.NoWrap; subtitleText.overflowMode = TextOverflowModes.Ellipsis;

        var refresh = UIKit.HudButton(panel, "REFRESH", null, UIKit.HudVariant.Secondary, 22);
        refresh.name = "RefreshButton";
        UIKit.Place((RectTransform)refresh.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-260, -32), new Vector2(220, 58));
        Wire(refresh, Refresh);
        var close = UIKit.HudButton(panel, "CLOSE", null, UIKit.HudVariant.Secondary, 22);
        close.name = "CloseButton";
        UIKit.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -32), new Vector2(200, 58));
        Wire(close, Close);
        if (owner != null) { owner.AddFx(refresh); owner.AddFx(close); }

        tabBar = UIKit.Rect("Tabs", panel);
        tabBar.anchorMin = new Vector2(0, 1); tabBar.anchorMax = new Vector2(1, 1); tabBar.pivot = new Vector2(0.5f, 1);
        tabBar.offsetMin = new Vector2(52, -192); tabBar.offsetMax = new Vector2(-40, -132);
        var th = UIKit.Horizontal(tabBar, 12, TextAnchor.MiddleLeft);
        th.childForceExpandWidth = false;

        var viewport = UIKit.Rect("Viewport", panel);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(40, 64); viewport.offsetMax = new Vector2(-28, -208);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // catches the mouse wheel between cards
        content = UIKit.Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(-16, 0);
        var v = UIKit.Vertical(content, 0, 18);
        v.padding = new RectOffset(12, 12, 4, 24);
        UIKit.FitHeight(content);
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        statusText = UIKit.Text(panel, "Status", "", 20f, UIKit.TextSecondary, TextAlignmentOptions.MidlineLeft);
        statusText.rectTransform.anchorMin = new Vector2(0, 0); statusText.rectTransform.anchorMax = new Vector2(1, 0);
        statusText.rectTransform.pivot = new Vector2(0.5f, 0);
        statusText.rectTransform.offsetMin = new Vector2(52, 14); statusText.rectTransform.offsetMax = new Vector2(-40, 56);
        statusText.textWrappingMode = TextWrappingModes.NoWrap; statusText.overflowMode = TextOverflowModes.Ellipsis;

        dim.SetActive(false);
        panel.gameObject.SetActive(false);
    }
}
