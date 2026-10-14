# Performance and smoothness

All measurements come from **standalone development builds** on the project laptop:
- Ryzen 5 4500U with integrated Radeon graphics, Direct3D 11;
- 1600×900 windowed, Ultra quality (the standalone default);
- VSync off, so the real cost is visible.

Each test is a scripted, repeatable 15 s walk-and-turn per level. Editor Play Mode was **not** used for timing, because its own overhead (EditorLoop) hid the game's costs.

Run-to-run noise on this laptop is about ±15%. So only results that held across repeated or alternating runs are claimed below.

## What was actually causing stutter

| Symptom | Root cause (measured) | Fixed? |
|---|---|---|
| Hitch when a message arrives / opens / is answered (~20 ms frame, ~1 MB garbage) | The email computer **destroyed and rebuilt its whole inbox UI** on every message event (4× per message). Each new row's text then had to be laid out from scratch, with the full email body passed into a 45-character preview. | **Yes:** rows are reused, and previews are capped at 100 characters. |
| First appearance of new text letters (briefing, notifications, feedback) | The UI fonts (Inter Regular/SemiBold) were Dynamic with **0 glyphs shipped**, so every new character was rasterised and the 1024² atlas re-uploaded at runtime. | **Yes:** both fonts are prefilled with all 116 characters the game uses, and kept in the build. |
| Constant garbage → periodic garbage collection | **Every frame allocated 0.6–1.3 KB.** Five scripts each fired `RaycastAll` from the camera, plus a per-frame clock string, enum names and timer strings. | **Yes:** one shared allocation-free look ray, and strings updated only when what they show changes. |
| Level 1 slow when walls/ceiling fill the view (p95 ≈ 24 ms) | **GPU-bound forward lighting.** The big wall/ceiling meshes span several rooms, so every ceiling light (up to 8 per object) re-draws them, each with soft-shadow sampling. Geometry is tiny (19k triangles); scripts are under 1 ms. | **Partly:** smaller shadow maps for Level 1's point lights (−7%). The real fix is below under "Recommended next steps". |
| Not a cause | GC pauses in the worst frames (none ran in them), physics (no rigidbodies), mouse look (correct, no smoothing), camera/hand smoothing (frame-rate independent). | No change needed. |

## Measured results

### Garbage (deterministic, every run)
| | Before | After |
|---|---|---|
| Frames that allocate | **100%** (every frame, every level) | **1–3%** (message events only) |
| Steady-state allocation | 0.6–1.3 KB per frame | **0 B** |
| Largest one-off allocation | 0.8–1.1 MB | 0.13–0.29 MB |
| GC collections per 15 s | 1–2 | 0–1 |

### Level 1 point-light shadow resolution (3 alternating A/B pairs, same build)
| | Default (Very High) | Medium |
|---|---|---|
| Average frame | 13.6 ms | **12.6 ms** |
| p95 / p99 | 24.4 / 26.0 ms | **23.0 / 24.2 ms** |
| Worst frame (mean of runs) | ~33 ms | **~27 ms** |

Visual check: identical renders of the living room before and after (these ceiling-light shadows are already soft).

### Other options measured, not applied (Level 1, single runs, for your decision)
| Option | Avg change | Visual cost |
|---|---|---|
| Point-light shadows off | −18% | Light leaks through walls between rooms; no furniture shadows from ceiling lights. |
| All shadows hard instead of soft | ≈ −14% | Jagged shadow edges. |
| Shadow resolution High + MSAA off | ≈ −26% avg, p95 25→18 ms | Jagged object edges. |
| Max 4 per-pixel lights | −10% | Some lights become per-vertex; on large walls this changes the look. |
| Deferred rendering | p95 −10%, avg +9% | No MSAA. |

### Not verified
- **The message-arrival frame time after the fix.** The garbage reduction is measured. The inbox reuse was verified in Play Mode: unchanged rows keep the same objects, and only the changed row is rebuilt. But the final timing run was interrupted, so the exact new frame time isn't measured.
- **Total frame-time gains beyond the A/B above.** Other runs were within noise. The game is GPU-bound, so CPU-side fixes remove hitches and garbage rather than raising average FPS.

## Files changed
| File | Change |
|---|---|
| `Scripts/Game/LookRaycast.cs` (new) | One shared, allocation-free "what is the player looking at" ray per frame. Each caller keeps its own reach. |
| `Scripts/PlayerInteractor.cs`, `Sittablechair.cs`, `Game/QuizManager.cs`, `Game/ComputerTerminal.cs`, `Game/CaseFilesTerminal.cs` | Use LookRaycast instead of `RaycastAll` plus sort. Behaviour: the first thing in the way within each script's reach decides, as before. |
| `Scripts/UI/InteractionPrompt.cs` | `KeyName()` caches key labels (enum `ToString()` allocated every frame). |
| `Scripts/Game/QuizManager.cs` | The response timer text is rebuilt only when the shown tenth of a second or the point value changes. The timing itself is unchanged. |
| `Scripts/Game/ComputerTerminal.cs` | The inbox reuses rows (only a row whose state changed is rebuilt). The preview is capped at 100 characters. The clock is formatted once per minute. |
| `Fonts/Resources/Inter-Regular SDF.asset`, `Inter-SemiBold SDF.asset` | Prefilled with 116 characters; *Clear Dynamic Data On Build* off. Still Dynamic. |
| `Scenes/Game.unity` | The 5 shadow-casting point lights have Shadow Resolution set to **Medium** (Inspector › Light › Shadow Type › Resolution). |
| `Editor/FontPrefill.cs` (new) | **Tools › Scam Sense › Performance › Prefill UI Fonts.** Re-run after adding text with new symbols. |
| `Scripts/Dev/PerformanceProbe.cs` (new, Editor and development builds only) | The frame-time probe and the build benchmark mode. |
| `Editor/PerformanceBenchmarkBuild.cs` (new) | **Tools › Scam Sense › Performance › Build Benchmark Player.** |

Originals are in `Backups/2026-10-14_before_performance/`.

## How to measure it yourself
1. **Tools › Scam Sense › Performance › Build Benchmark Player**, then choose a folder.
2. Run, and don't click the window (about 1 minute; it closes itself):
   `ScamSenseBenchmark.exe -perfprobe -perfout "C:\temp\report.txt" -screen-width 1600 -screen-height 900 -screen-fullscreen 0`
3. Optional:
   - `-benchscene Game` runs one level only.
   - `-benchexp pointmedium|nopointshadows|hard|noaa|shadowhigh|deferred|pixel4` tries a setting at runtime without changing the project.
   - `-perfraw "C:\temp\p.raw"` saves a 4 s profiler capture of Level 1 (open it with Window › Analysis › Profiler › Load).

The benchmark backs up and restores the build's saved progress, including when its window is closed early.

## Recommended next steps (highest impact first)
1. **Level 1 lighting (root cause of its GPU load):** either option keeps the look.
   - **(a) Split the large wall and ceiling meshes per room.** Lights in other rooms then stop touching them, so each wall gets 1–3 light passes instead of up to 8. No visual change.
   - **(b) Bake the static lighting** (lightmaps plus light probes for the player and doors). This removes most realtime per-pixel lighting and usually looks better (bounce light), but needs lightmap UVs, a bake, and a visual review.
2. **Static batching for Level 1 furniture.** Furnitures, ComputerRoom and Room aren't marked static; there are 1,650 draw calls and 167 materials. Exclude the 3 doors.
3. **Quality default on integrated graphics.** Standalone defaults to Ultra for everyone. Consider letting players pick, or detecting low-end GPUs on first launch.
4. **Unused package shaders.** `com.unity.ai.inference` (pulled in by the AI Assistant package) compiles many shaders into every build. Remove it from the game build if the game doesn't use it.
