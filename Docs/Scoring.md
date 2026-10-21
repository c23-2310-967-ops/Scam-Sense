# Scoring and reading time

All scoring rules live in each level's **Level Definition** asset, in its **Scoring** block (`LevelDefinition.scoring`). `ScoringSystem` reads them when the level starts. No other script hardcodes points. To rebalance a level, edit its asset in `Assets/Levels/`.

## How an answer is scored
- **Correct, within the reading allowance:** the level's **base points**.
- **Speed bonus:** a small extra for a quick, correct answer. The full bonus applies in the first 35% of the allowance and fades to 0 by the end of it. It never replaces the base points. A fast but wrong answer always earns 0.
- **Overtime:** after the allowance, a small deduction applies in steps (for example, 10 points every 10 seconds). It is capped, and a correct answer never drops below the level's minimum. Time never drains per second, and the first step past the allowance is free.
- **Wrong:** 0 points and the level's **wrong-answer penalty** comes off the total (once per message). The total never goes below 0. The wrong-answer card explains the mistake and offers the follow-up re-check (see `WrongAnswerConsequences.md`).
- **Scored once:** each message is scored exactly once. Repeated clicks are ignored.

## Fair reading time
Each message gets its own **reading allowance**:

`allowance = base seconds + words x seconds per word + details x seconds per detail`

- **Words:** sender, address, subject, body and link.
- **Details:** clickable details plus Details clues.
- **Limits:** clamped between the level's minimum and maximum.
- **Final cases:** get extra time on top.
- **Manual override:** set **Reading Seconds** on any message.

**When the timer runs:**
- It runs only while the message can be read. It stops in the pause menu and while the phone is being pulled out.
- It stops for 6 seconds after each newly inspected detail, so reading a finding is free.
- Follow-up re-checks are not timed.

**When the allowance runs out:** the message stays open and nothing is auto-answered. The timer shows **Overtime** and the capped deduction begins (no deduction in Level 1).

## Values per level

| | Level 1 | Level 2 | Level 3 | Level 4 |
|---|---:|---:|---:|---:|
| Difficulty | Easy | Easy-Medium | Medium-Hard | Hard |
| Base points (correct) | 100 | 150 | 200 | 250 |
| Speed bonus (max) | none | +15 | +20 | +25 |
| Wrong-answer penalty | 10 | 25 | 40 | 50 |
| Recovery (re-check; half with a hint) | 20 | 40 | 50 | 60 |
| Reading time: base + per word + per detail | 25 s + 0.6 + 4 | 20 s + 0.5 + 3 | 20 s + 0.5 + 3 | 20 s + 0.5 + 3 |
| Reading time limits (min-max) | 45-150 s | 40-120 s | 40-110 s | 40-120 s |
| Typical allowance in this level | 55-70 s | 57-79 s | 43-50 s | 42-58 s (final case 130 s) |
| Overtime deduction | none | 5 per 15 s | 10 per 10 s | 10 per 10 s |
| Max overtime deduction | 0 | 30 | 50 | 60 |
| Minimum for a correct answer | 100 | 120 | 150 | 190 |
| Messages | 5 | 7 | 6 | 8 |
| Max score (all correct and quick) | 500 | 1155 | 1320 | 2200 |
| Stars (1 / 2 / 3) | 200 / 325 / 425 | 1 / 715 / 900 | 1 / 870 / 1040 | 1 / 1500 / 1700 |
| Score to unlock the next room | 325 | 715 | 870 | 1500 |

Level 1 has no time pressure. The phone shows "TIME SPENT · NO RUSH" and the computer shows "No time pressure" instead of a countdown.

**How the thresholds were set:** they keep each level's old ratio to its maximum, but they are applied to base points only. A careful player who reads everything can reach them without the speed bonus.

## Connections
- **Stars and unlocking** still use the final `TotalScore` (stars are a reward; unlocking is score only).
- **Saves and cloud results** store the score and its maximum per run, as before.
- **Career XP** uses the score as a share of the maximum, so it works with any scale.
- **Rapid Response achievement:** every message answered correctly at full points.
- **Score scale change:** best scores saved before this change for Levels 2-4 are on the old 100-per-message scale. Any new run that scores higher replaces them.
- **Where the rules are shown:** the briefing shows each level's rules (`LevelScoring.Summary()`), and How to Play shows Level 1's.
