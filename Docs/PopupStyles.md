# Pop-up Styles

Every in-game pop-up is built at runtime with `UIKit`, so there are no prefabs to update. Pop-ups share four **tones**. Each tone has its own colour **and** its own icon, so meaning never depends on colour alone.

| Tone | Colour | Icon | Entrance | Used for |
|---|---|---|---|---|
| `Info` | cyan | **i** | pop | Case briefing |
| `Success` | green | **✓** | pop + expanding ring | Correct answer, recovered follow-up, case solved |
| `Error` | red | **×** | pop + one small shake | Wrong answer, trusted scam, reset progress |
| `Warning` | amber | **!** | pop + two soft pulses | False alarm, needs review, quit / exit confirmations |

## Pop-ups and what changed

| Pop-up | Script | Changes |
|---|---|---|
| Answer feedback card | `MessagePopupUI` | Status icon with the verdict and points; card border and strip in the result's colour; the answer in a tinted callout box; the "Why" header now says *Why it's right* or *What to look for*; the card fades and scales in. It resets to neutral for the next question. |
| Wrong-answer panel | `WrongAnswerConsequences` | Icon header (× for a mistake, ! for a false alarm or missed evidence, ✓ for a recovered follow-up); border in the tone colour; the card scales in. The shake moved from the whole card to the icon, so the text never moves while the player reads it. |
| Case briefing | `BriefingUI` | Info icon beside the title and subtitle; the panel animates open; on *Begin* the overlay fades out, and the level still starts immediately as before. |
| Results | `ResultsUI` | Outcome header (✓ CASE SOLVED / ! CASE NEEDS REVIEW) beside the case title; panel border in the outcome colour; the panel animates open. Stars, score count-up, unlock box and career section are unchanged. |
| Pause-menu confirmations | `PauseMenu` | Amber ! icon, amber strip and border, animated open. |
| Main-menu Quit / Reset progress | `MainMenuUI` | Icon beside the question (amber ! for quit, red × for reset); the panel strip and border match; the icon animates when the panel opens. |

## Building blocks (reuse these for new pop-ups)

- `UIKit.IconHeader(parent, tone, out PopupIcon icon, size)`: an icon plus a text column. Add the kicker and title to the returned column.
- `UIKit.IconBadge(parent, tone, size)`: the round icon on its own. Put it inside a centred `HorizontalLayoutGroup` so it stays round.
- `PopupIcon.SetTone(tone)` and `PopupIcon.Play()`: recolour the icon, and play the entrance for its tone.
- `UIKit.TintPanel(panel, tone)`: recolour a `PanelImage` + `AccentStrip` panel. Passing `Info` resets it to neutral.
- `UIKit.Callout(rect, tone)` and `UIKit.TintCallout(image, tone)`: the tinted box for a key line.
- `PopupAnimator` (a component): `Open()` and `Close(done)`. The panel fades and scales from 0.96, about 0.2 s, on unscaled time, so it also works while paused. Clicks are blocked while closing. Set `drift = 0` for panels inside a layout group.

**Motion rules:**
- All animation uses unscaled time.
- The lowest quality level (reduced effects) skips all motion.
- Every animation resets cleanly if its pop-up is hidden mid-animation.

## Notes

- **Ring sprite:** `UIKit_Ring` is generated at runtime when it isn't baked. To bake it with the other UIKit sprites, use the existing sprite-writing menu in `MenuUIBuilder` (optional).
- **No blur:** a real background blur needs a custom shader or render feature, so pop-ups keep the existing dark dimmer.
