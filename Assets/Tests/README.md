# Scam Sense automated tests

Run these every time a feature is finished, before committing. They check the things that have broken
before, so nobody has to re-test everything by hand.

## How to run

- **In Unity (easiest):** `Tools > Scam Sense > Run All Tests`. Takes a few seconds. The Console shows the
  summary and any failures; the full report is written to `TestResults/latest.txt`.
- **Test Runner window:** `Window > General > Test Runner` > `EditMode` > `Run All` (shows each test with a tick or cross).
- **Command line (Unity closed):** `powershell -ExecutionPolicy Bypass -File Assets/Tests/run-tests.ps1`

Save your scene first: the scene tests open each scene in turn (read-only) and reopen yours afterwards.
The tests never press Play, never save anything and never touch PlayerPrefs or the online account.

## What is tested

| File | Checks |
|---|---|
| `Editor/ScoringTests.cs` | Points for right/wrong answers, the wrong-answer penalty, score never below 0, no double scoring, missed-scam / false-alarm counters, the timer text |
| `Editor/LevelDataTests.cs` | Every room is in the catalog and the build, messages have an id, body, explanation and (for emails) a subject, ids are unique, each room mixes scams and real messages, star and unlock scores are reachable |
| `Editor/SceneIntegrityTests.cs` | In every scene: no missing scripts, no objects without a mesh or material, every computer has a screen, every chair has seat and exit points |
| `Editor/UILayoutTests.cs` | Crowded panels do not draw text lines over each other (the email-screen bug), UI panels keep their soft depth, the interaction highlight shader loads |

## Adding a test

When a bug is fixed, add a test that would have caught it, so it cannot come back unnoticed:
create a class in `Assets/Tests/Editor/`, add `using NUnit.Framework;` and write methods marked `[Test]`
that use `Assert` (see the existing files). `Run All Tests` picks it up automatically.
