# Wrong answer consequences

A wrong verdict now has consequences instead of a plain "Not quite". They are built on the existing systems:
- `QuizManager` still scores each answer once, through `ScoringSystem.TryRecord`.
- `WrongAnswerConsequences` reacts to that real result. It is added at runtime by QuizManager, so the scenes need no setup.

## What happens

| Mistake | Reaction | Follow-up |
|---|---|---|
| Trusted a scam (answered LEGITIMATE) | **Investigation Error** and a **simulated security incident**. The incident depends on the message: a look-alike sign-in, a suspicious payment attempt, or a request completed for an impostor. Case status: **Security risk missed**. | Investigated emails only |
| Reported a genuine message as a scam | **Investigation accuracy warning**. Shows the details that support legitimacy and says to verify through a trusted channel. There is never a fake incident. Case status: **False alarm raised**. | Investigated emails only |
| Wrong final case (Room 4) | **Evidence overlooked**. Lists the required records the player never opened at the evidence stations. | None |

Every card explains why the answer was wrong and names the clue that was overlooked. The clue comes from the message's own evidence data:
1. a key clue the player missed;
2. otherwise, one they found but didn't mark;
3. otherwise, the Details clues.

**Effects:**
- A red frame on the computer's reading pane or the phone's decision card.
- Two soft pulses on the screen edges and a small shake of the card.
- A quiet two-tone alert from `LevelAudio`. You can assign your own clip to *Investigation Warning*; if none is assigned, one is generated.
- All effects are over in under a second. The screen is never tinted.

**Closing the card:** Continue (or Esc) moves on.

## Follow-up investigation (re-check)
For emails with investigation data, **REVIEW EVIDENCE** opens the same email again with a checklist in the inbox column:
1. Inspect up to three key details. Links and senders come first.
2. Mark a key clue in the Evidence Notebook.
3. Give the verdict again. SCAM and LEGITIMATE stay locked until steps 1 and 2 are done.

**Scoring the re-check:**
- A correct re-check earns **20 recovery points**, or **10** if HINT was used.
- It is allowed once per message. A wrong re-check earns 0 with no further penalty.
- The first answer's result is never changed.

**SKIP FOLLOW-UP** gives up the recovery and continues.

## Scoring and case evaluation (`ScoringSystem`)
- A wrong answer costs **10 points**, once per message. The shown total never goes below 0.
- Penalties, recovery points and hint points are set in the Inspector under *Mistakes & Recovery*.
- **Results screen:** case grade (A-F, half score and half accuracy, with a corrected mistake counting half), first-answer accuracy, evidence found, mistakes corrected, hints and average response time.
- **HUD:** the case status line ("On track", "1 security risk missed" and so on) with the accuracy.
- Stars, room unlocking (score only), career XP, saving and the cloud results are unchanged. They read the same `TotalScore`.

## Per-message settings (optional)
Each message in a Level Definition has a **Wrong answer** block (`consequence`). Empty fields are generated.

| Field | What it does |
|---|---|
| Kind | Auto, Security Incident, Accuracy Warning, Missed Clue or Feedback Only. A genuine message never gets a security incident. |
| Alert Title / Alert Body | Custom text for the simulated alert. Use fictional content only. |
| Overlooked Clue | The clue to name. |
| Follow Up | Auto (offered when the message is investigated) or None. |
| Recheck Details | The inspectable ids to re-check. If empty, up to three key details are chosen. |

Everything shown is simulated: no account is accessed, nothing is sent and no file is changed. Links are never opened.
