# Release Process

How Scam Sense goes from an accepted `main` to a packaged, tested and traceable Windows release.
Use this for every version after v0.1.0.

## Environments

| Environment | Where / platform | Purpose | Required software |
|---|---|---|---|
| Development | Team members' Windows 10/11 PCs with the Unity Editor | Build and change the game | Unity Hub, Unity 6000.0.38f1 with Windows Build Support, Visual Studio or VS Code, Git, GitHub access |
| Testing | The extracted release ZIP on a teammate's PC (not the developer's project folder) | Check the packaged build works outside the editor | Windows 10/11 64-bit, DirectX 11 graphics; internet only to test accounts |
| Release / target | Player's Windows PC (home or school lab) | Play the game | Windows 10/11 64-bit; nothing to install (the build contains its runtime) |

Online accounts use the hosted **Supabase** project (database, authentication). It's the only cloud
service; the game itself needs no server, VM or container.

## Version numbers: `MAJOR.MINOR.PATCH`

| Change | Example | Why |
|---|---|---|
| Bug fix only | 0.1.0 → **0.1.1** | Nothing new, something corrected |
| New working feature | 0.1.0 → **0.2.0** | New capability (patch resets to 0) |
| Change that breaks saved data or a major redesign | 0.x → **1.0.0**, 1.x → **2.0.0** | Old saves or behaviour are no longer compatible |

**1.0.0** is the first complete, stable capstone release (see the conditions below). Until then, versions stay `0.x.y`.

## Steps for each release

```
Develop changes → Review and integrate (PR into main) → Decide the new version number → Update the version
→ Build → Test the packaged build → Write release notes → Tag the source → Publish / archive the release
```

1. **Develop and integrate.** Work on a feature branch, open a pull request, get it reviewed and merge it into `main`.
   Releases are only built from `main`, never from a personal copy.
2. **Readiness check.** Pull the latest `main`. In Unity: no Console errors, and **Tools → Scam Sense → Run All Tests** shows 0 failed.
3. **Decide the version** with the table above.
4. **Update the version** on a `release/vX.Y.Z` branch:
   - **Edit → Project Settings → Player → Version** = `X.Y.Z` (shown on the main menu);
   - copy the previous `Docs/Releases/RELEASE_NOTES_v….txt` to `RELEASE_NOTES_vX.Y.Z.txt` and update it.

   Commit, open a PR, and merge it into `main`.
5. **Build** from the merged `main`: **Tools → Scam Sense → Build Release**. It creates
   `Builds/ScamSense-vX.Y.Z/` and `Builds/ScamSense-vX.Y.Z.zip` with `README.txt`, `RELEASE_NOTES.txt` and
   `BUILD_INFO.txt` (version, commit, date). Check that `BUILD_INFO.txt` says **Clean source: yes**.
6. **Test the packaged build:** extract the ZIP to a new folder (ideally on another PC) and run `ScamSense.exe`.
   Do not use Play Mode in the editor for this. Check the following:

   | Check | Expected |
   |---|---|
   | Launch | Main menu appears, no error dialogs |
   | Version | Top-right corner shows `Version X.Y.Z` |
   | Main feature | Play Room 1 to the results screen |
   | Save / load | Quit, relaunch: CONTINUE and the room's best score are still there |
   | Assets | Every room in Level Selection opens with its models, text and sound |
   | Close | QUIT GAME closes the window |
   | Changes | Each item under "Included" and "Fixed" in the notes works |

7. **Tag the source** that produced the build:
   ```bash
   git tag -a vX.Y.Z -m "Scam Sense X.Y.Z"
   git push origin vX.Y.Z
   ```
8. **Publish:** on GitHub → Releases → Draft a new release, choose tag `vX.Y.Z`, paste the release notes
   and attach `ScamSense-vX.Y.Z.zip`. Never name files `final.zip` or `latest.zip`.

`Builds/` is git-ignored: builds are attached to GitHub Releases, never committed.

## Ready for 1.0.0 when

- All planned rooms and features are complete, and no critical or major bugs are open.
- Guest play, sign-up, sign-in and cloud sync have been tested end to end in a packaged build.
- The packaged build has passed the release tests on at least two different Windows PCs, including a school lab PC.
- The adviser and target users have play-tested it, and their feedback has been addressed.
- The window title and save location use the final product name (a save-compatibility decision made before 1.0.0).
- Third-party asset sources and licences are all confirmed.
- README, user instructions and release notes are complete.
