# Asset Inventory

Scam Sense: Think Before You Click - BSIT Capstone Project.

This file lists the assets each room uses and where each one comes from. The lists were taken from Unity's own dependency data for each room scene (`AssetDatabase.GetDependencies`, recursive), so nested prefabs, materials and textures are included. Scripts are left out. The menu's background (`Scenes/MenuBackdrop_Room1.unity`) is a copy of Room 1 and is listed where it shares a model.

## Model folders

| Folder | Contents |
|---|---|
| `Assets/Models/Room1/` | 39 models used only by Room 1 |
| `Assets/Models/Room2/` | 4 models used only by Room 2 |
| `Assets/Models/Shared/` | 15 models used by two or more rooms (`FloatingHand.obj` with its `.mtl`) |
| `Assets/Models/Room5/` | 9 models made for Room 5, in `Computers/`, `Desks/` and `Walls/` |
| `Assets/Models/Unused/` | 85 Kenney Furniture Kit models no room uses, kept with the kit |

Rooms 3 and 4 use only shared models, so they have no folder of their own. Every model keeps its `.meta` file, so moving it does not break scene references.

**Original vs new:** the 140 Kenney models, `Phone.fbx`, `inkpen.fbx` and `FloatingHand.obj` were already in the project. The 9 `R5_*` models in `Assets/Models/Room5/` were created later for this project from Room 5's own geometry.

## Where the assets come from

| Source | What | Licence / status |
|---|---|---|
| **Kenney - Furniture Kit** (kenney.nl/assets/furniture-kit) | 140 FBX models in `Assets/Models/Room1/`, `Room2/`, `Shared/` and `Unused/` (desks, chairs, computers, kitchen, bathroom, lamps, props). Each file says "Model created by Kenney"; the kit lists 140 files, matching the project. | CC0 (public domain): free to use, credit optional. No licence file is in the project. |
| **`Assets/Models/Shared/Phone.fbx`** | The phone on the desk (Rooms 1-4) | **Unknown source**: exported from Blender 2.79, no author recorded. |
| **`Assets/Models/Room1/inkpen.fbx`** | The pen on the Room 1 desk | **Unknown source**: exported from Blender 4.0.2, no author recorded. |
| **`Assets/Scam Sense Menu/CaseFolder.png`, `papers.png`** | Case folder and paper images on the Room 1 desk | **Unknown source**: no author or licence recorded. |
| **Inter** font (Rasmus Andersson) | Game text | SIL Open Font License; licence included (`Assets/Fonts/Inter-LICENSE.txt`). |
| **Liberation Sans** (TextMesh Pro default) | Fallback font | SIL Open Font License; ships with Unity's TextMesh Pro. |
| **Unity packages** | Input System, TextMesh Pro shader and icons | Part of Unity. |
| **Room 5 models (new)** | 9 OBJ models in `Assets/Models/Room5/` (walls, feature walls, pilaster, hexagonal desk pod, all-in-one computer, keyboard and mouse, laptop), each with a prefab that adds its colliders. See "Room 5 models" below. | Team's own work. Created in this project, not taken from any outside source. |
| **Made for this project** | Room 1-4 walls, floors, ceilings, windows, boards and signs, and Room 5's floor, ceiling, windows, door, instructor counter and small props, built from Unity primitives by the room builder scripts; all materials (`Assets/*.mat` colours, `Art_`, `CR_`, `L1_`, `R2_`-`R5_`, `SetDressing/`, `FX_`, `Kid_`); the player (`KidHoodie`, `Player` prefabs); the first-person hand (`Assets/Models/Shared/FloatingHand.obj`); UI sprites (`Resources/UIKit/`); the contact-shadow texture; the level preview images | Team's own work. Room sounds are generated in code. |

## Models

| Model | Format | Location | Used in | Source | Licence |
|---|---|---|---|---|---|
| chairDesk | FBX | `Assets/Models/Shared/chairDesk.fbx` | Rooms 1, 2, 3, 4, 5 + menu backdrop | Kenney - Furniture Kit | CC0 |
| FloatingHand | OBJ | `Assets/Models/Shared/FloatingHand.obj` | Rooms 1, 2, 3, 4, 5 | Made for this project | Team's own work |
| computerKeyboard | FBX | `Assets/Models/Shared/computerKeyboard.fbx` | Rooms 1, 2, 3, 4 + menu backdrop | Kenney - Furniture Kit | CC0 |
| computerMouse | FBX | `Assets/Models/Shared/computerMouse.fbx` | Rooms 1, 2, 3, 4 + menu backdrop | Kenney - Furniture Kit | CC0 |
| computerScreen | FBX | `Assets/Models/Shared/computerScreen.fbx` | Rooms 1, 2, 3, 4 + menu backdrop | Kenney - Furniture Kit | CC0 |
| desk | FBX | `Assets/Models/Shared/desk.fbx` | Rooms 1, 2, 3, 4 + menu backdrop | Kenney - Furniture Kit | CC0 |
| lampSquareTable | FBX | `Assets/Models/Shared/lampSquareTable.fbx` | Rooms 1, 2, 3, 4 + menu backdrop | Kenney - Furniture Kit | CC0 |
| Phone | FBX | `Assets/Models/Shared/Phone.fbx` | Rooms 1, 2, 3, 4 + menu backdrop | Unknown | Unknown |
| cabinetBedDrawerTable | FBX | `Assets/Models/Shared/cabinetBedDrawerTable.fbx` | Rooms 2, 3, 4 + menu backdrop | Kenney - Furniture Kit | CC0 |
| bookcaseOpen | FBX | `Assets/Models/Shared/bookcaseOpen.fbx` | Rooms 1, 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| books | FBX | `Assets/Models/Shared/books.fbx` | Rooms 1, 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| cardboardBoxClosed | FBX | `Assets/Models/Shared/cardboardBoxClosed.fbx` | Rooms 1, 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| pottedPlant | FBX | `Assets/Models/Shared/pottedPlant.fbx` | Rooms 1, 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| rugRectangle | FBX | `Assets/Models/Shared/rugRectangle.fbx` | Rooms 1, 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| trashcan | FBX | `Assets/Models/Shared/trashcan.fbx` | Rooms 1, 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| bathroomMirror | FBX | `Assets/Models/Room1/bathroomMirror.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| bathroomSink | FBX | `Assets/Models/Room1/bathroomSink.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| bathtub | FBX | `Assets/Models/Room1/bathtub.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| bear | FBX | `Assets/Models/Room1/bear.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| bedSingle | FBX | `Assets/Models/Room1/bedSingle.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| bookcaseClosedWide | FBX | `Assets/Models/Room1/bookcaseClosedWide.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| cabinetBedDrawer | FBX | `Assets/Models/Room1/cabinetBedDrawer.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| cabinetTelevision | FBX | `Assets/Models/Room1/cabinetTelevision.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| cardboardBoxOpen | FBX | `Assets/Models/Room2/cardboardBoxOpen.fbx` | Room 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| ceilingFan | FBX | `Assets/Models/Room1/ceilingFan.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| chairModernCushion | FBX | `Assets/Models/Room1/chairModernCushion.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| coatRack | FBX | `Assets/Models/Room1/coatRack.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| deskCorner | FBX | `Assets/Models/Room1/deskCorner.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| doorway | FBX | `Assets/Models/Room1/doorway.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| doorwayFront | FBX | `Assets/Models/Room2/doorwayFront.fbx` | Room 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| inkpen | FBX | `Assets/Models/Room1/inkpen.fbx` | Room 1 + menu backdrop | Unknown | Unknown |
| kitchenBlender | FBX | `Assets/Models/Room1/kitchenBlender.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| kitchenCabinet | FBX | `Assets/Models/Room1/kitchenCabinet.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| kitchenCabinetCornerInner | FBX | `Assets/Models/Room1/kitchenCabinetCornerInner.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| kitchenCabinetUpperDouble | FBX | `Assets/Models/Room1/kitchenCabinetUpperDouble.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| kitchenFridgeLarge | FBX | `Assets/Models/Room1/kitchenFridgeLarge.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| kitchenSink | FBX | `Assets/Models/Room1/kitchenSink.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| kitchenStove | FBX | `Assets/Models/Room1/kitchenStove.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| lampRoundFloor | FBX | `Assets/Models/Room1/lampRoundFloor.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| lampSquareCeiling | FBX | `Assets/Models/Room2/lampSquareCeiling.fbx` | Room 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| lampSquareFloor | FBX | `Assets/Models/Room1/lampSquareFloor.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| laptop | FBX | `Assets/Models/Room1/laptop.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| loungeChairRelax | FBX | `Assets/Models/Room2/loungeChairRelax.fbx` | Room 2 + menu backdrop | Kenney - Furniture Kit | CC0 |
| loungeSofaCorner | FBX | `Assets/Models/Room1/loungeSofaCorner.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| loungeSofaLong | FBX | `Assets/Models/Room1/loungeSofaLong.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| paneling | FBX | `Assets/Models/Room1/paneling.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| pillowBlue | FBX | `Assets/Models/Room1/pillowBlue.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| plantSmall1 | FBX | `Assets/Models/Room1/plantSmall1.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| plantSmall2 | FBX | `Assets/Models/Room1/plantSmall2.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| R5_AllInOneComputer | OBJ | `Assets/Models/Room5/Computers/R5_AllInOneComputer.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_FeatureWall_East | OBJ | `Assets/Models/Room5/Walls/R5_FeatureWall_East.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_FeatureWall_West | OBJ | `Assets/Models/Room5/Walls/R5_FeatureWall_West.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_HexDeskPod | OBJ | `Assets/Models/Room5/Desks/R5_HexDeskPod.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_KeyboardMouse | OBJ | `Assets/Models/Room5/Computers/R5_KeyboardMouse.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_Laptop | OBJ | `Assets/Models/Room5/Computers/R5_Laptop.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_Pilaster | OBJ | `Assets/Models/Room5/Walls/R5_Pilaster.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_Wall_Long | OBJ | `Assets/Models/Room5/Walls/R5_Wall_Long.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| R5_Wall_Short | OBJ | `Assets/Models/Room5/Walls/R5_Wall_Short.obj` | Room 5 | Made for this project (new, Room 5) | Team's own work |
| rugSquare | FBX | `Assets/Models/Room1/rugSquare.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| sideTable | FBX | `Assets/Models/Room1/sideTable.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| speakerSmall | FBX | `Assets/Models/Room1/speakerSmall.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| stairsOpen | FBX | `Assets/Models/Room1/stairsOpen.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| table | FBX | `Assets/Models/Room1/table.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| tableRound | FBX | `Assets/Models/Room1/tableRound.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| toilet | FBX | `Assets/Models/Room1/toilet.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| wallCorner | FBX | `Assets/Models/Room1/wallCorner.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |
| wallWindowSlide | FBX | `Assets/Models/Room1/wallWindowSlide.fbx` | Room 1 + menu backdrop | Kenney - Furniture Kit | CC0 |

`FloatingHand.obj` uses `FloatingHand.mtl` in the same folder. The other 85 Kenney models are not used by any room; they are kept with the rest of the kit in `Assets/Models/Unused/`.

## Room 5 models

Room 5's walls, hexagonal desk pods and computers used to be Unity cubes placed by `Assets/Editor/Room5Builder.cs`. They are now real model files that can be opened in Unity or Blender. They were created for this project by exporting the room's own geometry, so the room looks the same. Each model has an `.obj` (geometry), an `.mtl` (colours) and a `.prefab`. In Unity, the `.mtl` colours are mapped to the project's own `Assets/Materials/R5_*.mat`. The prefab adds the colliders. `Room5Builder` now only places these prefabs.

| Model | Parts | Used |
|---|---|---|
| `Walls/R5_Wall_Long` | 20.4 x 2.75 x 0.2 m wall | north and south walls |
| `Walls/R5_Wall_Short` | 0.2 x 2.75 x 14 m wall | east and west walls |
| `Walls/R5_Pilaster` | 0.4 x 2.75 x 0.24 m pilaster | 6 along the window wall |
| `Walls/R5_FeatureWall_West`, `R5_FeatureWall_East` | taupe panel with diagonal silver inlays | west and east walls |
| `Desks/R5_HexDeskPod` | hexagonal desk top, 6 legs, cable core, 6 partitions with caps | 6 pods |
| `Computers/R5_AllInOneComputer` | stand, body, bezel, screen | 36 seats + instructor terminal |
| `Computers/R5_KeyboardMouse` | keyboard, keys, mouse | 36 seats |
| `Computers/R5_Laptop` | base and lid | instructor counter |

## Shared models (2 or more rooms)

- bookcaseOpen.fbx: Rooms 1, 2
- books.fbx: Rooms 1, 2
- cabinetBedDrawerTable.fbx: Rooms 2, 3, 4
- cardboardBoxClosed.fbx: Rooms 1, 2
- chairDesk.fbx: Rooms 1, 2, 3, 4, 5
- computerKeyboard.fbx: Rooms 1, 2, 3, 4
- computerMouse.fbx: Rooms 1, 2, 3, 4
- computerScreen.fbx: Rooms 1, 2, 3, 4
- desk.fbx: Rooms 1, 2, 3, 4
- FloatingHand.obj: Rooms 1, 2, 3, 4, 5
- lampSquareTable.fbx: Rooms 1, 2, 3, 4
- Phone.fbx: Rooms 1, 2, 3, 4
- pottedPlant.fbx: Rooms 1, 2
- rugRectangle.fbx: Rooms 1, 2
- trashcan.fbx: Rooms 1, 2

## By room

### Room 1 - The First Case (`Scenes/Game.unity`)  (141 files)

- **Models (53):** FloatingHand.obj, Phone.fbx, bathroomMirror.fbx, bathroomSink.fbx, bathtub.fbx, bear.fbx, bedSingle.fbx, bookcaseClosedWide.fbx, bookcaseOpen.fbx, books.fbx, cabinetBedDrawer.fbx, cabinetTelevision.fbx, cardboardBoxClosed.fbx, ceilingFan.fbx, chairDesk.fbx, chairModernCushion.fbx, coatRack.fbx, computerKeyboard.fbx, computerMouse.fbx, computerScreen.fbx, desk.fbx, deskCorner.fbx, doorway.fbx, inkpen.fbx, kitchenBlender.fbx, kitchenCabinet.fbx, kitchenCabinetCornerInner.fbx, kitchenCabinetUpperDouble.fbx, kitchenFridgeLarge.fbx, kitchenSink.fbx, kitchenStove.fbx, lampRoundFloor.fbx, lampSquareFloor.fbx, lampSquareTable.fbx, laptop.fbx, loungeSofaCorner.fbx, loungeSofaLong.fbx, paneling.fbx, pillowBlue.fbx, plantSmall1.fbx, plantSmall2.fbx, pottedPlant.fbx, rugRectangle.fbx, rugSquare.fbx, sideTable.fbx, speakerSmall.fbx, stairsOpen.fbx, table.fbx, tableRound.fbx, toilet.fbx, trashcan.fbx, wallCorner.fbx, wallWindowSlide.fbx
- **Prefabs:** KidHoodie.prefab, Player.prefab
- **Materials (72, all made for the project):** Art_ x9, CR_ x9, FX_ x1, Kid_ x9, L1_ x5, Room 1 colour materials (`Assets/*.mat`) x12, SetDressing x27
- **Textures/images:** CaseFolder.png, FX_ContactShadow.png, level_1_preview.png, papers.png
- **Other:** Level1_TheFirstCase.asset, MainMixer.mixer
- **Fonts:** Liberation Sans (fallback)

### Room 2 - The Investigation (`Scenes/Room2_InvestigationOffice.unity`)  (100 files)

- **Models (19):** FloatingHand.obj, Phone.fbx, bookcaseOpen.fbx, books.fbx, cabinetBedDrawerTable.fbx, cardboardBoxClosed.fbx, cardboardBoxOpen.fbx, chairDesk.fbx, computerKeyboard.fbx, computerMouse.fbx, computerScreen.fbx, desk.fbx, doorwayFront.fbx, lampSquareCeiling.fbx, lampSquareTable.fbx, loungeChairRelax.fbx, pottedPlant.fbx, rugRectangle.fbx, trashcan.fbx
- **Prefabs:** KidHoodie.prefab
- **Materials (64, all made for the project):** CR_ x5, FX_ x1, Kid_ x9, R2_ x17, SetDressing x32
- **Textures/images:** FX_ContactShadow.png, level_2_preview.png
- **Other:** Level2_TheInvestigation.asset, MainMixer.mixer
- **Fonts:** Inter (Regular, SemiBold), Liberation Sans (fallback)

### Room 3 - The Digital Trap (`Scenes/Room3_DigitalTrap.unity`)  (113 files)

- **Models (9):** FloatingHand.obj, Phone.fbx, cabinetBedDrawerTable.fbx, chairDesk.fbx, computerKeyboard.fbx, computerMouse.fbx, computerScreen.fbx, desk.fbx, lampSquareTable.fbx
- **Prefabs:** KidHoodie.prefab
- **Materials (84, all made for the project):** FX_ x1, Kid_ x9, R3_ x46, SetDressing x28
- **Textures/images:** FX_ContactShadow.png, UIKit_Chamfer.png, UIKit_ChamferOutline.png, UIKit_Rounded.png, level_3_preview.png
- **Other:** Level3_TheDigitalTrap.asset, MainMixer.mixer
- **Fonts:** Inter (Regular, SemiBold), Liberation Sans (fallback)

### Room 4 - Digital Forensics Lab (`Scenes/Room4_DigitalForensicsLab.unity`)  (113 files)

- **Models (9):** FloatingHand.obj, Phone.fbx, cabinetBedDrawerTable.fbx, chairDesk.fbx, computerKeyboard.fbx, computerMouse.fbx, computerScreen.fbx, desk.fbx, lampSquareTable.fbx
- **Prefabs:** KidHoodie.prefab
- **Materials (84, all made for the project):** FX_ x1, Kid_ x9, R3_ x4, R4_ x40, SetDressing x30
- **Textures/images:** FX_ContactShadow.png, UIKit_Chamfer.png, UIKit_ChamferOutline.png, UIKit_Rounded.png, level_4_preview.png
- **Other:** Level4_DigitalForensicsLab.asset, MainMixer.mixer
- **Fonts:** Inter (Regular, SemiBold), Liberation Sans (fallback)

### Room 5 - MacLab (`Scenes/Room5_MacLab.unity`)  (90 files)

- **Models (11):** FloatingHand.obj, R5_AllInOneComputer.obj, R5_FeatureWall_East.obj, R5_FeatureWall_West.obj, R5_HexDeskPod.obj, R5_KeyboardMouse.obj, R5_Laptop.obj, R5_Pilaster.obj, R5_Wall_Long.obj, R5_Wall_Short.obj, chairDesk.fbx
- **Prefabs:** KidHoodie.prefab, R5_AllInOneComputer.prefab, R5_FeatureWall_East.prefab, R5_FeatureWall_West.prefab, R5_HexDeskPod.prefab, R5_KeyboardMouse.prefab, R5_Laptop.prefab, R5_Pilaster.prefab, R5_Wall_Long.prefab, R5_Wall_Short.prefab
- **Materials (49, all made for the project):** FX_ x1, Kid_ x9, R5_ x39
- **Textures/images:** FX_ContactShadow.png, UIKit_Chamfer.png, UIKit_ChamferOutline.png, UIKit_Circle.png, UIKit_Rounded.png, level_5_preview.png
- **Other:** Level5_MacLab.asset, MainMixer.mixer
- **Fonts:** Inter (Regular, SemiBold), Liberation Sans (fallback)

## What to credit

- Kenney Furniture Kit - www.kenney.nl (CC0; credit is optional but good practice).
- Inter typeface - Rasmus Andersson (SIL Open Font License).
- Phone model, ink pen model, case folder and papers images - **source still to be confirmed**.

## Other third-party files to check

These are not used by the room scenes, but they are in the repository and their licences are not recorded:

| File | Used by | Note |
|---|---|---|
| `Assets/Scam Sense Menu/Wrath by Soundridemusic.mp3` (5.2 MB) | Menu assets | The file name names the artist, but no licence file is in the project. Confirm the licence before a public release. |
| `Assets/Resources/GoldropspersonaluseRegular-9Ynn5.otf` (13.1 MB) | Nothing references it | The file name says "personal use". It appears unused; consider removing it after the team confirms. |
