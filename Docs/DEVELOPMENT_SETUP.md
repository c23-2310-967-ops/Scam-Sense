# Development Setup

How to open, run and work on Scam Sense: Think Before You Click. For what the game is and how to play it, see the [README](../README.md).

## Requirements

- Unity Hub and Unity Editor **6000.0.38f1** (Unity 6). Other versions may upgrade the project and change many files.
- Git.
- Windows PC (the game targets Windows Standalone).

## First open

1. Clone the repository:
   ```bash
   git clone https://github.com/c23-2310-967-ops/Scam-Sense.git
   ```
2. In Unity Hub, choose **Add → Add project from disk** and select the `Scam-Sense` folder.
3. Open it with Unity 6000.0.38f1. The first open takes several minutes while Unity rebuilds `Library/`, which is not stored in Git.
4. Open `Assets/Scenes/Menu.unity` and press **Play**.

Online accounts are optional. Without them, progress is saved on the PC only. To set them up, follow [SupabaseAccounts.md](SupabaseAccounts.md). The real `Assets/Resources/SupabaseSettings.json` is git-ignored and must never be committed.

## Scenes

Build order (**File → Build Profiles → Scene List**):

| # | Scene | What it is |
|---|---|---|
| 0 | `Menu` | Main menu, accounts, level selection, settings |
| 1 | `Game` | Room 1 - The First Case |
| 2 | `Room2_InvestigationOffice` | Room 2 - The Investigation |
| 3 | `Room3_DigitalTrap` | Room 3 - The Digital Trap |
| 4 | `Room4_DigitalForensicsLab` | Room 4 - Digital Forensics Lab |
| 5 | `MenuBackdrop_Room1` | 3D background shown behind the main menu |
| 6 | `Room5_MacLab` | Room 5 - The Compromised Campus Network |

A room scene can be played on its own for testing, but the normal flow starts from `Menu`.

## Where things live

| Path | Contents |
|---|---|
| `Assets/Levels/` | One `LevelDefinition` asset per room (messages, explanations, scoring, timing, unlock rules) and `LevelCatalog` |
| `Assets/Scripts/` | Game code: `Game/` (level flow, scoring, computers, phone), `UI/`, `Data/` (saving, career), `Online/` (Supabase) |
| `Assets/Editor/` | Editor-only tools: room builders, level content, previews. Not included in builds |
| `Assets/Models/` | 3D models by room. See [ASSET_INVENTORY.md](ASSET_INVENTORY.md) |
| `Assets/Materials/` | Materials (prefixes show the room: `R2_` to `R5_`, `SetDressing/`, `Kid_` for the player) |
| `Assets/Art/LevelPreviews/` | Room preview images used by the menu |
| `Docs/` | Feature notes and guides |
| `supabase/` | Database migrations (linked to Supabase through GitHub) and the settings template |

## Editor tools

The project adds a **Tools → Scam Sense** menu:

| Menu item | What it does |
|---|---|
| Level 3 / 4 / 5 → **Create Room N Scene** | Creates the room scene once, if it does not exist yet |
| Level 3 / 4 / 5 → **Rebuild Room N Environment** | Deletes and rebuilds the open room's environment (gameplay objects are kept). Rooms 1 and 2 are edited directly in their scenes |
| Environment → **Dress All Rooms** / **Remove Set Dressing From All Rooms** | Adds or removes the decorative props in the rooms |
| Environment → **Set Up Interactive Objects (All Rooms)** | Sets up readable case folders, boards and documents |
| Investigation → **Apply Message Investigation Data** | Adds the clickable details and clues to the Level 1 and Level 2 messages |
| Menu → **Build Menu Backdrop** / **Capture Level Previews** | Rebuilds the menu's 3D background and re-captures the room preview images |
| **Rebuild Contact Shadows**, **Rebuild Kid Character Prefab**, **Add Kid Character To Player** | Art helpers |
| Performance → **Build Benchmark Player** / **Prefill UI Fonts** | Performance tools |

A rebuild changes the scene file. Commit or back up first, and don't rebuild a scene a teammate is editing.

## Room 5 models

Room 5's walls, feature walls, pilasters, hexagonal desk pods, all-in-one computers, keyboards and mice, and the laptop are model files in `Assets/Models/Room5/`:

- `R5_*.obj`: the geometry. It can be opened in Blender. Units are metres, Y is up.
- `R5_*.mtl`: the colours. In Unity they are mapped to the project's `Assets/Materials/R5_*.mat`, so changing those materials changes the room.
- `R5_*.prefab`: the model plus its colliders. `Room5Builder` places these prefabs.

To change a model, edit the `.obj` (for example in Blender, exporting OBJ with the same name), then let Unity reimport it. Every placed copy updates. The prefab keeps its colliders, so adjust them in the prefab if the shape changes a lot.

## Working with Git

- Pull before you start, and keep each commit focused on one change.
- Unity scene files don't merge well. Agree who is editing which scene.
- Commit every asset together with its `.meta` file; the `.meta` holds the ID that scenes use to find the asset.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, builds, `.env` files or `SupabaseSettings.json`. `.gitignore` already excludes them.
- Before pushing, open the changed scenes and check that the Console shows no errors.

## Testing

There are no automated tests yet. The Unity Test Framework package is installed but has no test scripts. Use the manual checklist in the [README](../README.md#testing).
