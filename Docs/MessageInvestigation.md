# Message Investigation (inside normal gameplay)

There is no separate Investigation Mode. **Emails** that have investigation data can be inspected while they are open, before the player answers SCAM or LEGITIMATE as usual. Text messages and chats on the phone are plain read-and-answer (`MessageInvestigation.Investigates` checks the channel; their data in the Level assets is kept but unused). Scoring, stars, XP, progression and results are unchanged.

## What the player does
1. An email opens as usual.
2. Inspectable details are underlined in cyan: sender, sender details, subject, body phrases and links. Clicking or tapping one opens a **finding card** that explains it.
   - Links are **never opened**. The card shows the address in a box marked "SIMULATED LINK · NOT OPENED".
   - A detail that reveals a key clue turns gold ★ and the card says **KEY CLUE FOUND**. Neutral details turn grey ✓. Clicking a detail again adds nothing new.
   - Each newly inspected detail pauses the response timer for 6 s of reading time.
3. The player answers with the normal buttons. The clues are evaluated once, with that answer.
4. The normal feedback adds a **CLUES YOU FOUND** block: the key clues found and the ones missed. CONTINUE moves on as before.

There is no notebook or clue selection: clues are found directly on screen. Found clues don't change the score; they add feedback and count towards *Evidence found* on the results screen.

## Files
| File | Role |
|---|---|
| `Scripts/Data/MessageInvestigationData.cs` | `InspectField`, `MessageInspectable` (field + match phrase + finding + optional clue), `MessageEvidence` (clue; `supportsVerdict = false` for decoys). |
| `Scripts/Data/CaseData.cs` | `ScamMessage.inspectables` / `evidence` / `HasInvestigation`; `MessageChannel.Chat`; `MessageChannels.OnPhone`. |
| `Scripts/Game/InvestigationSession.cs` | Per-message state: inspected details, discovered clues, one-time `Submit` (key clues found / missed), data `Validate`. |
| `Scripts/Game/MessageInvestigation.cs` | Added by `QuizManager` at runtime. Builds the "Investigation Canvas" (hint and finding card) and provides `Bind`/`Mark` for presenters, the timer pause and `FeedbackFor`. |
| `Scripts/UI/InvestigationLinkText.cs` | Makes TMP `<link>` parts clickable on overlay and world-space canvases. |
| `Scripts/Game/IMessagePresenter.cs` | `IInvestigationDock`: where the investigation UI can sit without covering the message. The phone uses the gap beside the decision card; the computer uses the inbox column. |
| `Editor/MessageInvestigationContent.cs` | **Tools › Scam Sense › Investigation › Apply Message Investigation Data**: writes the details and clues for Levels 1–2 and adds the Level 2 IT chat message. |

No Inspector setup is needed: `QuizManager` adds `MessageInvestigation` in `Awake`, and the presenters bind their texts through it.

## Adding investigation to a message
Edit the message in its Level asset, or extend `MessageInvestigationContent.cs`:
- `inspectables`: `id`, `field`, and `match`, the exact phrase in that field's text (leave it empty for the whole field). Then add `label`, `finding`, an optional `linkAddress` and an optional `clueId`.
- `evidence`: `id`, `title`, `whyItMatters`, and `supportsVerdict`.

Message text is never changed. When the message opens, problems are logged as warnings, for example a match phrase that isn't in the text or a clue that can't be discovered.

Levels 3 and 4 have no investigation data yet. They play exactly as before.
