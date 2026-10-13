# HUD Buttons + Digital Light Sweep (main menu)

Reference: flat sci-fi HUD element sheet (cut-corner frames, thin outlines, segmented tick bars, inset tabs).
Kept the game's cyan palette instead of the reference's violet.

## Files
| File | Change |
|---|---|
| `Assets/Scripts/UI/DigitalLightSweep.cs` | **New.** Cyan light band that sweeps diagonally across a button on hover / keyboard select; frame glow while hovered; optional idle sweep; `Play(strength)`, `PreviewAt(progress)`. Unscaled, hitch-safe timing. The band never receives raycasts. |
| `Assets/Scripts/UI/UIKit.cs` | **Added** `HudButton(...)` (+ `HudVariant` Secondary / Primary / Danger), generated `ChamferSprite`, `ChamferOutlineSprite`, `SweepSprite`; `Badge` no longer stretches. Existing `UIKit.Button` is unchanged, so in-game UI is unaffected. |
| `Assets/Scripts/UI/MainMenuUI.cs` | Main-menu buttons now use `UIKit.HudButton`: 7 navigation buttons, panel Back/Cancel, Settings footer (Reset/Apply/Back), level-card buttons, red "Yes, Quit". Small icon buttons (×, ←, →) keep the old style (too small for cut corners). PLAY has an idle sweep every 6 s. |

No scenes, prefabs or assets were changed (the menu is built at runtime).

**Backups** of the two pre-existing files: `Backups/2026-10-14_before_hud_buttons/` (`UIKit.cs`, `MainMenuUI.cs`). To roll back: copy them over `Assets/Scripts/UI/` and delete `DigitalLightSweep.cs`.

## Button structure (runtime)
```
"<LABEL> Button"   Image (cut-corner fill) + Mask + Button + DigitalLightSweep + ButtonPressFeedback   <- only raycast target
  LightSweep        Image (band, clipped by the Mask, raycast off)
  Frame             Image (2 px cut-corner outline, raycast off) - glows cyan on hover
  Ticks             3 segmented bars (raycast off)
  Tab               small top-edge tab (raycast off)
  Label             TMP text (raycast off)
  Sub               optional right-hand TMP text (menu only)
```

## Tuning (DigitalLightSweep fields)
`Sweep Color` (default cyan, alpha 0.55), `Duration` (0.6 s), `Band Width` (0.32 of width), `Angle` (18°), `Idle Interval` (0 = hover only), `Idle Strength`, frame idle/hover colours. In code: `UIKit.HudButton(parent, label, onClick, UIKit.HudVariant.Primary, fontSize, idleSweepEvery)`.
Corner size: `pixelsPerUnitMultiplier = 0.7` on the fill and frame in `HudButton` (lower = bigger cuts; keep both equal). Buttons should be at least ~60 px tall.

To use it in-game too, replace `UIKit.Button(...)` calls with `UIKit.HudButton(...)` (not done: in-game UI was left untouched on purpose).

## Verified in Play Mode
- PLAY test button first: shape, sweep (frozen mid-way with `PreviewAt`), frame glow on a real pointer-enter event, only the root receives raycasts (centre, edges and cut corner all hit the button).
- Rollout: all 7 menu buttons are the top raycast hit; Settings and Quit opened with real pointer-click events; Level Selection buttons correct (disabled state included); red "Yes, Quit" variant.
- Regression: Level 1 PLAY loads `Game` once, in-game UI has no sweep components, pause → Main Menu returns with a free cursor, timeScale 1; no Console errors from project scripts.

## Not verified
- Watching the sweep play at full speed with a real mouse (the Editor throttles when not focused, so timing was checked with `PreviewAt`), keyboard/controller navigation sweep, and a standalone build.

## Troubleshooting
| Symptom | Fix |
|---|---|
| Sweep shows outside the button shape | The root must keep its `Mask` (showMaskGraphic on). |
| Corners look stretched | Button too small for the slice border, or fill/frame `pixelsPerUnitMultiplier` differ. |
| Button not clickable | Something new under the button has `raycastTarget` on and no handler - turn it off. |
| No sweep on hover | Button not interactable (sweep is skipped on disabled buttons), or `DigitalLightSweep` disabled. |
