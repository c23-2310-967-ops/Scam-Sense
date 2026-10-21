# Room interactions (look + E)

All interactions use the existing system:
- **`LookRaycast`:** the first collider in front of the camera wins, so walls block interaction.
- **`InteractionPrompt`:** the on-screen "[E] …" prompt.
- **`InteractionGate`:** freezes the player while a panel is open, uses E once per frame, handles Esc, and never lets two panels open at once.

| Object | What E does | Handled by |
|---|---|---|
| Computer | Opens the computer's mail screen (Esc or Exit leaves) | `ComputerTerminal` (existing) |
| Phone | A waiting text opens on the left-hand pocket phone. With nothing waiting: "No new messages on your phone yet." | `QuizManager` + `PocketPhone` (existing); the note comes from `PlayerInteractor` |
| Case folder | Reads the level briefing, case notes and objectives | `InspectableObject` → `DocumentReaderUI` |
| Evidence document | Reads a printed clue (or the level's case files) | `InspectableObject` → `DocumentReaderUI` |
| Investigation board | Reviews every message answered so far, with the answer and the explanation. Locked until the first message is answered ("Review a message first"). | `InspectableObject` → `DocumentReaderUI` |
| Case-file station / final case terminal (Rooms 3–4) | Unchanged | existing terminals |

**Reader controls:** E, Esc or CLOSE closes it; Left / Right (or A / D) turn pages; the mouse wheel scrolls. While it is open, movement, mouse-look and the HUD are hidden or frozen, and the cursor is free.

## Objects set up per room
| Room | Case folder | Evidence document | Investigation board |
|---|---|---|---|
| 1 | Desk folders (bedroom) | Mail on the kitchen table (order receipt) | "My Case Notes" corkboard |
| 2 | Desk files | Printed purchase record; printed IT announcement | Evidence board (west wall) |
| 3 | Case folder on the case-files desk | Printed case files (the level's Case Files) | Incident Log whiteboard |
| 4 | Clipboard on the workbench | Chain-of-custody form on the evidence cart | Website Captures board |

## Adding another readable object
1. Select the object and add **Inspectable Object**.
2. Set **Kind**, **Title** and **Source**:
   - **Custom Pages:** type the pages yourself;
   - **Level Briefing**;
   - **Level Case Files**;
   - **Reviewed Evidence**.
3. Optionally set **Requirement = Messages Reviewed** with a **Locked Hint** and **Locked Message**.
4. Give it a **non-trigger Collider**.

**Re-run the setup:** `Tools > Scam Sense > Environment > Set Up Interactive Objects (All Rooms)` reapplies the setup above. Dress All Rooms also reapplies it after rebuilding the props.

**Chairs:** a chair's "standing next to it" shortcut now steps aside when you aim at a readable object, so E on a document beside a chair reads it. Aiming at the chair itself still sits.
