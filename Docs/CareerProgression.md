# Cybersecurity Career and Ranks

Every closed case earns career XP. XP raises your rank from **Cybersecurity Trainee** to **Master Digital Investigator**, and real gameplay events unlock achievements. The career sits on top of the existing systems and doesn't replace any of them:
- Saving uses `LocalDataManager`, including Cloud Save, guest/account profiles and RESET PROGRESS.
- Scoring uses `ScoringSystem`.
- Room unlocking still comes from `LevelCatalog` and the passing score.

**Ranks never lock or unlock rooms.**

## Ranks (cumulative XP)
| # | Rank | XP | Insignia |
|---|---|---|---|
| 1 | Cybersecurity Trainee | 0 | bar |
| 2 | Junior Scam Analyst | 150 | 1 chevron |
| 3 | Scam Detection Specialist | 350 | 2 chevrons |
| 4 | Cybersecurity Investigator | 650 | 3 chevrons |
| 5 | Senior Cybersecurity Investigator | 1,000 | 3 chevrons + bar |
| 6 | Digital Forensics Analyst | 1,400 | diamond |
| 7 | Cybercrime Specialist | 1,800 | diamond + chevron |
| 8 | Elite Cybersecurity Agent | 2,100 | star |
| 9 | Master Digital Investigator | 2,300 | star in a ring |

The maximum possible is **2,412 XP** (375 / 500 / 600 / 937 for Cases 1–4).
- **A good first playthrough** (most answers right, about 8 s each, all evidence) ends at about **1,855 XP, Cybercrime Specialist**.
- **Elite** needs strong replays.
- **Master** needs close to flawless, fast answers in every case.

## XP for a case
Each case is multiplied by its difficulty: ×1, ×1.25, ×1.5 and ×1.75 for Levels 1–4.

| Part | XP |
|---|---|
| Case closed (every message answered) | 60 |
| Case solved (passing score reached) | 60 |
| Each correct decision | 25 |
| Response quality (score ÷ max score) | up to 80 |
| Flawless accuracy (all correct) | 50 |
| Each required piece of evidence actually opened (Level 4) | 15 |
| Final multi-evidence conclusion correct (Level 4) | 40 |

**No farming.** Each case remembers its best XP, and career XP is the sum of those bests. A replay adds only the improvement over your best run: identical or worse replays add 0. The results screen says so and shows what the run was worth.

XP is awarded only when a case is closed. Entering a room, exiting or quitting earns nothing.

**Saves from before the career system:** completed cases are credited with an estimate based on their best score, so no one starts from zero. Playing the case again replaces the estimate with real data.

**Not tracked by the game:** "red flags found", "sender verified" and "unsafe simulated actions". No XP or achievement claims them.

## Achievements
| Achievement | Earned when |
|---|---|
| First Case Solved | You solve any case |
| Flawless Analysis | Every message in a case is correct |
| Rapid Response | Every answer is correct and at full speed |
| Evidence Expert | All required evidence is opened before closing the case |
| Consistent Analyst | Best-run accuracy is 80%+ in 3 cases |
| Promotion Day | You earn a new rank |
| Master of Digital Forensics | The final multi-evidence case is solved correctly |
| Case Load Cleared | All 4 cases are solved |
| Elite Investigator | You reach Elite Cybersecurity Agent |
| Master Digital Investigator | You reach the top rank |

## Where it shows
- **Results screen, after every case:**
  - outcome (CASE SOLVED / CASE CLOSED · NEEDS REVIEW) with the case number and name;
  - +XP with a breakdown;
  - rank insignia and name;
  - an animated XP bar with "x / y XP to *next rank*";
  - new achievements.
- **Promotion card:** only when a new rank is reached. It shows the insignia with a soft glow, says what earned the rank, lists every rank passed if several were crossed in one case, and names the next rank.
- **Main menu:** a *CAREER PROFILE* card at the top right shows insignia, rank, XP bar, XP, cases solved, accuracy and achievement count. The Level 1 backdrop and room previews are unchanged. Clicking the card opens the **career dashboard**: rank and statistics, best XP per case, the full ladder, and every achievement.

**Effects and sound:** at the lowest graphics quality setting, the career section and promotion card appear without animation. The promotion card uses the existing room-unlocked sound and the bar uses the star sound, through `LevelAudio`.

## Files
| File | What it is |
|---|---|
| `Assets/Scripts/Data/CareerConfig.cs` + `Assets/Resources/CareerConfig.asset` | **Edit ranks, XP values and achievements here.** It's the only place they're defined; built-in defaults apply if the asset is missing. |
| `Assets/Scripts/Data/CareerSystem.cs` | `RecordCase`, the one method that awards XP, plus rank maths, stats and achievement checks |
| `Assets/Scripts/UI/RankBadge.cs` | Rank insignia and glow, drawn in code (no texture assets) |
| `LocalDataManager.cs` (modified) | Per case: `bestXp`, `bestCorrect`, `messageCount`, `perfect`. Per profile: `achievements`. Merges keep the best values, and RESET PROGRESS clears them all. |
| `GameManager.cs` (modified) | Calls `CareerSystem.RecordCase` once when the case closes |
| `ResultsUI.cs` (modified) | Career section and promotion card |
| `MainMenuUI.cs` (modified) | Career profile card and dashboard |
| `FinalCaseTerminal.cs` (modified) | Exposes its required-evidence list |

No Inspector assignments are needed: everything is found or built at runtime.
