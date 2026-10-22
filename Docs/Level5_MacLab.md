# Level 5 - The Compromised Campus Network (MacLab)

The scene is `Assets/Scenes/Room5_MacLab.unity`. It is built by `Assets/Editor/Room5Builder.cs`:
- **Tools > Scam Sense > Level 5 > Create Room 5 Scene** builds it once.
- **Rebuild Room 5 Environment** refreshes the decor in the open scene.

The level content is in `Assets/Editor/Level5Content.cs`, which writes `Assets/Levels/Level5_MacLab.asset`. After the first build, edit the asset in the Inspector.

**Setting:** the MacLab at UPHSL Biñan (College of Computer Studies). The people, messages, office addresses, numbers and domains are fictional training content, and there are no university logos. Domains stay on the reserved `.example` suffix:
- official: `uphsl-binan.example`
- fake: `uphsl-binan-accounts.example`, `uphsl-scholarship-awards.example`, the misspelled `upshl-binan.example`, and a free-mail `quickmail.example` address

## Room (modelled on the reference photos, without the people)
- **Size:** 20 x 14 m, with a 2.75 m suspended ceiling (60 cm tiles and square LED panels in a grid).
- **Walkways (measured with physics checks):** centre aisle 2.65 m, between pods 2.45 m, along the windows 2.4 m, front of the room 3.0 m, in front of the instructor counter 1.3 m, west wall 1.78 m, east wall 1.28 m at its tightest (beside the sittable chair at MAC-31).
- **Walls and floor:** white walls; taupe feature panels with diagonal silver inlays on the west and east walls; a run of tall windows on the south wall; light glossy floor tiles.
- **Front (north):** a wall-mounted display with a fictional campus cybersecurity notice and this week's schedule, above a grey instructor counter with an all-in-one computer and a laptop.
- **Pods:** six hexagonal six-seat pods in two rows of three, 36 seats in total. Each seat has a silver-and-white all-in-one computer facing out, a white keyboard and mouse, and a black chair. Radial grey partitions sit between seats, and each seat has a number on the desk edge (MAC-01 to MAC-36).
- **Props:** a floor-standing air conditioner, ceiling CCTV domes, a printer, a bin and a wall clock.
- **Entrance:** the player enters at the east door, looking down the centre aisle (windows on the left, display on the right).

**Floor plan** (north = front):

```
            [ wall display ]
            [ instructor counter ]            printer
  Pod 1 (MAC-01..06)   Pod 2 (07..12)   Pod 3 (13..18)
  ----------------------- centre aisle ------------------ door (east)
  Pod 4 (19..24)       Pod 5 (25..30)   Pod 6 (31..36)
  [ windows ............................................ ]
```

## Gameplay
- **Five flagged workstations** each hold one email:
  - MAC-31: account verification (scam)
  - MAC-15: scholarship (scam)
  - MAC-20: Wi-Fi maintenance notice (legitimate)
  - MAC-11: `.pdf.exe` attachment (scam)
  - MAC-06: Accounting impersonation (scam)
- **The phone** gets one text: a lab booking reminder (legitimate).
- **Station-specific messages:** a message's **Station Id** must match the terminal's **Station Id**, otherwise that computer doesn't show it.
  - Other levels have no station ids, so any computer still works there.
  - The HUD notification names the workstation.
- **Flagged stations** have an amber "FLAGGED" tent card and a chair pushed back. The other 31 computers are props showing ordinary desktop content.
- **Instructor computer:** the official records station (`CaseFilesTerminal`). It holds Campus IT Security Notice, Official Contacts & Domains, Scholarships & Payments, and This Week on Campus.
- **Every email has clickable details and key clues,** so the existing investigation flow, the wrong-answer consequences and the follow-up re-check all apply.

## Scoring and progression
| | Level 5 |
|---|---|
| Points per correct answer | 300 (up to +30 speed bonus) |
| Wrong-answer penalty | 60 |
| Recovery (re-check) | 70 |
| Overtime | 10 per 10 s after the allowance, max 70, never below 230 |
| Max score | 1,980 (1,800 without bonuses) |
| Stars | 1 / 1,380 / 1,560 |
| Pass (case solved) | 1,380 |

- **Unlocking:** Level 4 unlocks Level 5 with its existing rule (score 1500+). Level 4 already pointed to `level_5`, so earlier unlocks stay valid.
- **Final case:** Level 5 is the last case, so its results say "This was the final case".
- **Saving:** progress is saved per level id (`level_5`), with no change to the save format.
- **Career:** *All Cases Solved* now needs 5 cases (`CareerConfig.casesInGame`). Players who already earned it keep it.
