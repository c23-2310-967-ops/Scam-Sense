# Main menu UI in the Hierarchy

The main menu (PLAY, CONTINUE, LEVEL SELECTION, SETTINGS, HOW TO PLAY, ABOUT, QUIT, with the Digital Light Sweep buttons) and the account panels are now **real objects in `Assets/Scenes/Menu.unity`**. You can select, move, restyle and rewire them in the Inspector. Every button's **OnClick** list holds normal persistent events.

## Hierarchy (Menu scene)
```
MenuManager                [ExitConfirmation, NewCaseTransition, MenuButtons, MainMenuUI, AccountUI]
├── MainMenu Canvas        [Canvas (sort 10), CanvasScaler 1920x1080 match height, GraphicRaycaster]
│   ├── Background         ShotCurrent / ShotNext (house shots)
│   ├── Tint, Gradient, Grid
│   ├── Title              Name / Line / Tagline / Sub
│   ├── Navigation
│   │   ├── PLAY Button              OnClick: MainMenuUI.PlayClickSound, MainMenuUI.Play
│   │   ├── CONTINUE Button          OnClick: PlayClickSound, Continue
│   │   ├── LEVEL SELECTION Button   OnClick: PlayClickSound, OpenPanel("levels")
│   │   ├── SETTINGS Button          OnClick: PlayClickSound, OpenPanel("settings")
│   │   ├── HOW TO PLAY Button       OnClick: PlayClickSound, OpenPanel("how")
│   │   ├── ABOUT Button             OnClick: PlayClickSound, OpenPanel("about")
│   │   ├── QUIT GAME Button         OnClick: PlayClickSound, OpenPanel("quit")
│   │   └── Status
│   ├── Progress, Offline, LiveFeed
│   ├── PanelDim                     OnClick: MainMenuUI.ClosePanel (click outside closes)
│   ├── Panels
│   │   ├── Panel_settings / Panel_levels / Panel_how / Panel_about / Panel_quit
│   │   ├── AuthDim
│   │   ├── AuthenticationPanel
│   │   │   ├── Header/PanelTitle, PanelBody
│   │   │   ├── PlayAsGuestButton     OnClick: AccountUI.PlayAsGuest
│   │   │   ├── PlayerSignInButton    OnClick: AccountUI.OpenSignIn
│   │   │   ├── PlayOfflineButton     OnClick: AccountUI.PlayOffline
│   │   │   ├── BackButton            OnClick: AccountUI.Back
│   │   │   ├── GuestNote, StatusText
│   │   ├── PlayerSignInPanel
│   │   │   ├── PanelSubtitle, UsernameInput (TMP_InputField), PasswordInput (TMP_InputField, Password)
│   │   │   ├── ErrorMessageText
│   │   │   └── Buttons: SignInButton (SubmitSignIn), CreateAccountButton (OpenCreateAccount), BackButton (Back)
│   │   ├── CreateAccountPanel
│   │   │   ├── PanelSubtitle, UsernameInput, PasswordInput (Password), ConfirmPasswordInput (Password), Rules
│   │   │   ├── ErrorMessageText
│   │   │   └── Buttons: CreateAccountButton (SubmitCreateAccount), BackButton (Back)
│   │   ├── AccountPanel          AccountInfo, AccountDetail, CreateAccountKeepProgressButton, SignInOtherAccountButton,
│   │   │                         SyncNowButton, TryConnectButton, SignOutButton, CloseButton
│   │   └── BusyPanel             BusyText
│   └── AccountChip               OnClick: AccountUI.OpenAccountPanel
└── MainMenu Fade Canvas
    └── FadeOverlay               (SceneLoader waits for this name)
```
Every account button also has `MainMenuUI.PlayClickSound` in its OnClick list, so it sounds the same as the rest of the menu. Your original **Canvas › SceneRoot** menu (New Case, Briefing, Settings, Exit) and the single **EventSystem** are unchanged.

## Flow
- **PLAY** (or any level card, or CONTINUE) with nobody signed in opens the **AuthenticationPanel**:
  - **PLAY AS GUEST:** anonymous sign-in, then the level starts.
  - **PLAYER SIGN IN:** opens the PlayerSignInPanel.
    - **SIGN IN:** signs in, then the level starts.
    - **CREATE ACCOUNT:** opens the CreateAccountPanel. Confirm Password must match, and Unity's username/password rules are checked first.
  - **PLAY OFFLINE:** uses this PC's save; results sync later.
  - **BACK:** returns to the panel you came from, and finally to the menu. Progress is never touched.
- **A returning player** is signed back in silently, so **PLAY starts straight away**. The **AccountChip** (top-right, e.g. "GUEST PLAYER · ONLINE") opens the AccountPanel.

## Scripts
| Script | Responsibility |
|---|---|
| `UI/MainMenuUI.cs` | Menu navigation, settings and level cards. Holds all scene references (Inspector: "Scene UI" headers), and its handlers are public for OnClick. |
| `UI/AccountUI.cs` | Account panels, messages and status chip. Its handlers are public for OnClick. |
| `Online/CloudAccountService.cs` | Unity Authentication and Cloud Save. Unchanged by this step. |
| `UI/DigitalLightSweep.cs`, `UI/ButtonPressFeedback.cs` | Existing button animations. `ButtonPressFeedback` was moved out of `UIKit.cs`, unchanged, so it can be saved in scenes. |
| `UI/UIKit.cs` | Now loads its sprites from `Assets/Resources/UIKit/*.png`, so scene objects can reference them. Same look as before. |
| `Editor/MenuUIBuilder.cs` | **Tools › Scam Sense › Build Menu UI In Scene**. |

### About Tools › Scam Sense › Build Menu UI In Scene
- It creates the objects above once, assigns every reference and adds all OnClick events.
- **Running it again replaces the generated objects**, which discards manual edits to them. After you start customizing in the Inspector, don't run it again (or back up the scene first).
- **Made at runtime on purpose:**
  - the **level cards** inside `Panel_levels/Cards`, because they come from the LevelCatalog data;
  - the Light Sweep band;
  - the hover sounds, which are C# callbacks that Unity can't save in a scene.
- **Fallback:** if `MainMenu Canvas` is deleted, or *Built In Scene* is unticked on MainMenuUI or AccountUI, the same UI is built at runtime, so the menu never breaks.

## Tested (Editor Play Mode, by invoking the real buttons' OnClick lists)
- The scene menu is used at runtime, with no duplicate canvas and one EventSystem. The cursor is free, and the look is identical to before.
- 47/47 MainMenuUI and 26/26 AccountUI references are assigned. All 114 images use sprite assets.
- **PLAY** opens AuthenticationPanel.
- **BACK** closes it, with nothing blocking the menu afterwards.
- **PLAYER SIGN IN**, **CREATE ACCOUNT** and mismatched passwords ("The passwords don't match.") work, and **BACK, BACK, BACK** returns step by step.
- **PLAY AS GUEST** signs in and Level 1 loads.
- **Back in the menu:** the chip shows GUEST PLAYER · ONLINE, and **PLAY starts directly**.
- I fixed one layout bug: CREATE ACCOUNT was clipped in the sign-in row.

## Not tested
- **Signing in or creating an account against Unity:** this needs **Username & Password** switched on in the Unity Dashboard (Player Authentication › Identity providers).
- **Real mouse clicks and typing:** testing invoked the buttons' events.
- **Exact 1366×768 rendering:** the layout is resolution-independent (1920×1080 reference, matched by height), but I only viewed it in the Editor's Game view.
- **A standalone build.**
