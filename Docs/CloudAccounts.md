# Accounts and cloud progress (Unity Gaming Services)

## What players see
> **Updated:** the account panels are now real scene objects, opened from the menu's **PLAY** button. The welcome screen on launch was removed. See `Docs/MainMenuHierarchy.md`.

Pressing **PLAY** (or starting any level) while nobody is signed in opens the **AuthenticationPanel**:
- **PLAY AS GUEST:** anonymous sign-in, then the level starts.
- **PLAYER SIGN IN:** username and password. Its **CREATE ACCOUNT** button opens the account form, with a confirm-password field.
- **PLAY OFFLINE:** uses this PC's save.
- **BACK:** returns to the menu.

Returning players are signed back in silently, so PLAY starts straight away. A chip in the top-right corner shows **`GUEST PLAYER · ONLINE`**, **`<USERNAME> · SYNC PENDING`**, or **`OFFLINE`**. Clicking it opens the **Account** panel:
- **Guests:** Create Account (Keep Progress), Sign In to Another Account, Sign Out.
- **Players:** Sync Now, Sign Out.
- **Offline:** Try to Connect.

## Packages (installed)
- `com.unity.services.authentication` **3.8.0**
- `com.unity.services.cloudsave` **3.4.2**
- `com.unity.services.core`, pulled in automatically.

The project is already linked to the Unity Cloud project **Capstone** (organization `c23-2493-354`, project ID `619d3e32-…`).

## Dashboard setup: one step is still required
1. Open https://cloud.unity.com, choose the **Capstone** project, and go to **Player Authentication** (or **Authentication**) › **Identity providers**.
2. Click **Add Identity Provider**, choose **Username & Password**, and save.
   - Until you do this, sign-in and account creation show *"Username sign-in isn't switched on for this game yet"* (tested).
3. **Cloud Save** needs no setup; it already worked in testing. **Anonymous** sign-in is on by default.

No secrets are stored in the game. The client only uses the public project ID that Unity links automatically.

## How it works

### Files
| File | Role |
|---|---|
| `Assets/Scripts/Online/CloudAccountService.cs` (new) | One persistent service (`DontDestroyOnLoad`): sign-in, linking, Cloud Save sync, retry, status. |
| `Assets/Scripts/UI/AccountUI.cs` (new) | Welcome gate, forms, account panel and status chip. Built at runtime with UIKit; added automatically by MainMenuUI. |
| `Assets/Scripts/Data/LocalDataManager.cs` (modified) | Per-player saves, merge rules, sync-pending flag, changed event. The public API is unchanged, so GameManager needed no edits. |
| `Assets/Scripts/UI/MainMenuUI.cs` (modified) | Adds AccountUI, makes `RefreshProgress()` public, and ignores Esc while an account screen is open. |

Originals are in `Backups/2026-10-14_before_cloud_accounts/`.

**Inspector:** nothing to connect. The buttons are created in code, like the rest of the menu.

### Guest accounts
- `SignInAnonymouslyAsync()` creates the guest. Unity caches a session token on the PC, and the next launch signs the same guest back in (tested).
- **Signing out as a guest keeps that token**, so PLAY AS GUEST returns to the same guest and the same progress.
- ⚠️ **Guest progress can be lost** if Unity's saved login data is cleared (reinstalling or wiping PlayerPrefs) or the player moves to another device. Creating an account prevents that. The game says this on the welcome screen and in the Account panel.

### Guest → account (keeping progress)
- **Create Account while signed in as a guest** calls `AddUsernamePasswordAsync`, which adds the username to the current guest. The player ID and cloud data stay the same; no new empty profile is created.
- **If the username already belongs to another account,** the player is told to use Player Sign In instead.
- **Player Sign In while signed in as a guest:**
  - the guest's local progress is merged into the account (best scores kept, unlocks kept, nothing discarded);
  - if the sign-in fails, the guest session is restored.
- ⚠️ **Not yet verified end to end:** linking needs the Dashboard step above. Testing confirmed the request reaches Unity, the player ID and progress stay intact when it's refused, and the player sees the right message. The successful link itself is untested.

### Cloud Save data
- **Location:** one key per player, `progress_v1`. Its value is the same JSON the game saves locally:
  ```json
  { "levels": [ { "levelId": "level_1", "unlocked": true, "completed": true, "passed": true,
                  "bestScore": 500, "bestStars": 3, "lastScore": 500, "lastStars": 3,
                  "timesCompleted": 2, "lastPlayedUtc": 638... } ],
    "pendingSync": false, "updatedUtc": 638... }
  ```
- **Sync** runs after sign-in, after every finished level, on Sync Now, and every 20 s while results are pending. Each sync downloads the cloud copy, merges it, then uploads.
- **Merge rules** apply everywhere (cloud, offline cache, guest → account):
  - `unlocked`, `completed` and `passed` never go back to false;
  - best score and stars only go up, and `timesCompleted` takes the maximum;
  - the most recent score comes from whichever run has the newer `lastPlayedUtc`.
- **Duplicate uploads are harmless,** because the merge is idempotent.
- **A result recorded during an upload** is not lost: the pending flag only clears if nothing changed since the upload began.
- **Room unlocking is still decided by score in GameManager.** Stars never unlock rooms.
- **Local cache:** each Unity player has its own local save (`scamsense_progress_v1_<playerId>`), so two people on one PC don't mix. Progress made before accounts existed is merged into the first profile that signs in (tested: Level 1's 500 points carried over).

### Keeping players' progress apart (2026-10-15)
- **Sync only touches the signed-in player's own save.** `SyncAsync` checks that the signed-in player ID matches the active local save before starting. It checks again after each network request; if someone signed out or switched account meanwhile, the sync is abandoned and re-run for the new player. Before this, a sync that was still downloading could merge the previous player's cloud progress into the next player's save.
- **Signing out of an account puts its save away.**
  - Its progress stays in its own local save and in Cloud Save.
  - The menu and PLAY OFFLINE switch back to this PC's guest save (`scamsense_last_guest_profile`), or a fresh offline save if there's no guest.
  - Whoever plays next therefore can't add results to the signed-out account.
  - Signing out as a guest still keeps the guest's save and session; PLAY AS GUEST continues it.
- **A renewed session must be the same player.** If an expired session comes back as a different player, the game stays offline on the local save instead of syncing it into the wrong cloud slot.
- **Account → account never mixes.**
- **Guest → existing account still merges, on purpose.** The guest's progress is added to the account, as the sign-in screen says. Once the guest's login is replaced, the guest can't come back, so not merging would lose its progress.

### Account popup
- **SIGN OUT asks first.** The button turns into CONFIRM SIGN OUT and explains what happens to the progress. Closing or reopening the popup cancels it.
- **SYNC NOW feedback:** it shows SYNCING… and reports "Progress synced." or the reason it failed (it retries automatically).
- **Buttons disable** while an account operation or sync is running, so nothing can start twice.
- **Feedback line:** `AccountPanel/AccountMessage` (field *Account Message* on AccountUI) shows these messages and any outstanding sync problem.
- **The menu's saved-progress line refreshes** when a background sync brings progress down from the cloud.
- **Footer:** the saved-progress line is now one line that shrinks to fit. The "Offline training" note sits underneath. Before, the two overlapped on narrower screens and in small Game views.

### Offline
- If the services can't be reached, the game uses the PC's last save and shows **OFFLINE**.
- It tries to reconnect every 20 s. Once it reconnects as the same player, pending results upload.

## Testing

### Tested in the Editor (real Unity services)
- Packages compile, and the services initialize.
- The welcome screen appears for a new player, in the menu's style.
- **PLAY AS GUEST:**
  - the anonymous sign-in succeeded with a real player ID;
  - the chip shows `GUEST PLAYER · ONLINE`;
  - existing local progress (Level 1, 500 points) moved into the guest profile.
- **Cloud Save round trip:**
  - progress uploaded with nothing left pending;
  - after wiping the guest's local save, Sync restored Level 1 (500, 3 stars) and the Level 2 unlock from the cloud.
- **Returning player:** the next launch signed the same guest back in automatically.
- **Account panel:** shows the guest warning and the right buttons.
- **Create Account form:**
  - instant validation ("Usernames are 3 to 20 characters long.");
  - a password field that's masked and wiped after submit;
  - with the provider off, a clear "isn't switched on" message, with the guest still signed in under the same player ID.
- No compile or runtime errors.

### Still needs manual testing (after the Dashboard step)
- [ ] Create Account as a guest: the chip changes to your username and the **player ID stays the same**, with the same progress.
- [ ] Sign out, then Player Sign In with that account: progress loads from the cloud.
- [ ] Create Account from the welcome screen (not a guest): a new account starts at Level 1.
- [ ] Wrong password shows "Wrong username or password."; an existing username shows "already taken".
- [ ] Linking a username that another account already uses shows the "use Player Sign In" message.
- [ ] Offline: disconnect from the internet and launch. You should see **OFFLINE**, be able to play a level, and the result should be saved locally (**SYNC PENDING** once online). Reconnect, and it should sync within about 20 s.
- [ ] Two players on one PC keep separate progress.
- [ ] A standalone build (all testing so far was in the Editor).

## Known limits
- **Scores aren't tamper-proof.** They're stored as the client reports them; anyone determined could edit them. That's fine for progress saves. For competitive **leaderboards**, use server-side validation (Unity Cloud Code), which is why Leaderboards were left out for now.
- **No email sign-in or password reset.** Unity's built-in username provider has neither. Players can change their password while signed in (`UpdatePasswordAsync`), but there's no UI for it yet.
- **Leftover test player:** testing created one anonymous test player (`t2mj0yig…`) in your Unity project. It's harmless. You can delete it in the Dashboard under **Player Management** if you like.
