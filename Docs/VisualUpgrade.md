# Visual upgrade: lighting, environment and results polish

Render pipeline: **Built-in**, Linear colour space, unchanged. No packages were added.

## What was wrong (found by inspecting the project)
- **Level 1:**
  - 14 point lights, none casting shadows, so furniture looked like it floated;
  - flat ambient light;
  - no reflection probes;
  - the spawn bedroom (with the downstairs computer) had no ceiling light and was very dark.
- **Quality settings:**
  - shadow distance was 150 m for indoor rooms, which made shadows blurry;
  - only 2–4 pixel lights were allowed, so most lamps fell back to flat vertex lighting.
- **Both levels:** bare walls, and no contact or ambient-occlusion shadows under furniture.
- **Results screen:** the score appeared instantly, and there was no completion or unlock sound.

## Changes

### Project settings
`ProjectSettings/QualitySettings.asset`:

| Tier | Shadow distance | Pixel lights | Shadow resolution |
|---|---|---|---|
| Medium | 25 m | 4 | unchanged |
| High | 30 m | 6 | unchanged |
| Very High | 35 m | 8 | Very High |
| Ultra | 35 m | 8 | Very High |

Very Low and Low are unchanged. Players still choose the tier in Settings.

### Level 1, `Assets/Scenes/Game.unity`
Everything new sits under the root object **VisualUpgrade** (plus **ContactShadows**).
- **Ambient:** warm three-colour gradient (sky/equator/ground) instead of flat.
- **Soft shadows** on the main light of each room: living room, kitchen, dining/TV corner, upstairs, and the computer-room desk spotlight.
- **New lights:** a bedroom ceiling light (warm, soft shadows) and a subtle blue monitor glow on the downstairs desk.
- **Sun:** a warm, low-angle directional light that only enters through the upstairs windows (walls and roof block it elsewhere).
- **Reflection probes:** two baked, box-projected probes (ground floor and upstairs). Textures are in `Assets/Scenes/Game_Probes/`.
- **Contact shadows:** 42, under furniture.
- **Decor:** three framed low-poly artworks (two in the living room, one above the bed), a kitchen wall clock, and magazines on the coffee table.

### Level 2, `Assets/Scenes/Room2_InvestigationOffice.unity`
- **Ambient:** cool, technical three-colour gradient.
- **Shadows:** soft on the ceiling lights, desk lamp and board spotlight. The monitor-glow fill light has no shadows (cheaper). The desk lamp and monitor glow are slightly stronger.
- **Reflection probe:** one baked probe. Texture is in `Assets/Scenes/Room2_Probes/`.
- **Contact shadows:** 16, under furniture.
- **Props** (all fictional, placeholder `.example.invalid` domains only):
  - printed email and SMS-log sheets on the desk, marked "SUSPICIOUS?";
  - notebook and pen;
  - keyboard and monitor cables;
  - folders on the filing cabinets;
  - two storage boxes;
  - a "THINK BEFORE YOU CLICK / Check · Verify · Report" awareness poster;
  - a wall clock.

### Scripts
| File | Change |
|---|---|
| `Assets/Editor/ContactShadowBuilder.cs` | **New editor tool.** Menu **Tools › Scam Sense › Rebuild Contact Shadows**. It puts a soft darkened footprint under each prop (a cheap stand-in for ambient occlusion: multiply blend, no lighting cost, no colliders). Re-run it after moving furniture, then save the scene. |
| `Assets/Scripts/UI/ResultsUI.cs` | The final score counts up from 0 (0.9 s, ease-out) before the stars appear. When the next room unlocks, the unlock banner pulses gently and a chime plays. Unlocking logic is untouched: it is still decided by score in GameManager. |
| `Assets/Scripts/Game/LevelAudio.cs` | Added `levelComplete` and `roomUnlocked` clips. They are generated if empty, can be replaced in the Inspector, and go through the SFX mixer group, so the Settings volume applies. |

New assets:
- `Assets/Textures/FX_ContactShadow.png` and `Assets/Materials/FX_ContactShadow.mat`;
- `Assets/Materials/L1_*` and `R2_*` prop materials.

Backups are in `Backups/2026-10-14_before_visual_upgrade/`: the three scenes, QualitySettings, ResultsUI.cs and LevelAudio.cs.

To undo the scene changes without restoring a backup, delete the **VisualUpgrade** and **ContactShadows** objects. That leaves only the lighting values (ambient colours and light shadow toggles) changed.

## Tested
- Both scenes rendered before and after from several angles.
- Furniture now sits on the floor with soft shadows, the bedroom is no longer dark, and the art, poster, clock and props are placed flush on walls and surfaces.
- I fixed one placement problem I found: the storage boxes were oversized and have been resized.
- **Level 2 played start to finish in Play Mode** (scripted clicks on the real buttons):
  - messages on the computer and the pocket phone;
  - results showed 500/600, 6/6 correct, 3 stars and the unlock banner;
  - the save showed Level 3 unlocked;
  - the score finished counting up to the correct value.
- **Level 1 loaded and started in Play Mode** with the new lighting. There were no errors in the Console.
- Your save was backed up before testing and restored afterwards.

## Not tested / limitations
- **Frame rate:**
  - the Editor showed roughly 70–110 FPS, but timings from an unfocused Editor are unreliable;
  - Level 1 now has 6 shadow-casting lights;
  - if a low-end PC struggles, lower the quality tier, or turn off shadows on `Ceiling_Light (2)` and `Ceiling_Light (6)`.
- **Ambient occlusion:**
  - real screen-space AO needs the Post Processing package (`com.unity.postprocessing`), which isn't installed;
  - I didn't download it without your permission; contact shadows are used instead;
  - say the word if you want it installed and set up.
- **Baked lightmaps:** not baked, because baking takes a long time and locks lights in place. Everything is real-time, so lights can still be moved freely.
- **No new windows:** the ground floor of Level 1 has no windows, so sunlight only reaches upstairs. Cutting new windows into the walls would change the level layout.
- **Not checked by eye:** the results animation's sounds (playback can't be heard through my tools) and a hands-on keyboard and mouse playthrough.
- **Existing warning:** `Furnitures/wallCorner (1)` has a negative scale with a BoxCollider. This predates these changes and was left as is.
