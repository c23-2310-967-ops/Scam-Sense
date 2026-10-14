# Player character: kid in a hoodie

The grey capsule player is now a low-poly **kid in a blue hoodie, jeans and white sneakers**, in Level 1 (`Game.unity`) and Level 2 (`Room2_InvestigationOffice.unity`).

## What you see in first person
| Part | In view | In the shadow |
|---|---|---|
| Jeans and sneakers | ✔ when you look down | ✔ |
| Hoodie (with hood, drawstrings and pocket), head, hair, arms | hidden, so they never block or clip the camera | ✔ full silhouette |
| Hands | your `FloatingHand.obj` hands, now with **blue hoodie sleeves** and a darker cuff | — |
| Pocket-phone hand | hoodie sleeve and cuff | — |

A box-shaped torso seen from directly above reads as a flat slab, so the hoodie is shown in the shadow and on the sleeves rather than in view. The camera FOV and near clip plane are unchanged.

## Movement
`PlayerBody.cs` animates it in code, with no Animator or extra assets:
- **Walking:** legs and arms swing with your real speed, plus a small step bob.
- **Sitting:** a seated pose while you're in any SittableChair (thighs on the seat, shins down, arms toward the desk).
- **Facing:** the body always faces where you look.
- **Purely visual:** no colliders, so movement, interactions and scoring are unchanged. The Player's CharacterController and collider are untouched; only the capsule's MeshRenderer is switched off.

## Files
| File | What it is |
|---|---|
| `Assets/Prefabs/Player/KidHoodie.prefab` | The character. Edit it in the Inspector: move parts, or recolour via the materials. |
| `Assets/Materials/Kid_*.mat` | Hoodie, trim, jeans, skin, hair, sneaker, sneaker stripe, sole, drawstring. |
| `Assets/Scripts/PlayerBody.cs` | Walking, sitting and facing. Its rig references are assigned on the prefab. |
| `Assets/Editor/PlayerCharacterBuilder.cs` | **Tools › Scam Sense › Add Kid Character To Player** (adds it to the open scene's Player), and **Rebuild Kid Character Prefab**. |
| `Assets/Scripts/FloatingHand.cs` | Adds the sleeve to your hand model (it uses the model's **Cuff** and **Palm** parts). New field: **Sleeve Color**. |
| `Assets/Scripts/Game/PocketPhone.cs` | Sleeve on the phone-holding arm. New field: **Sleeve Color**. |

To change the hoodie colour, edit `Kid_Hoodie.mat` and `Kid_HoodieTrim.mat`. Then set the same colour in **Sleeve Color** on the FloatingHand component (on the Main Camera) and on PocketPhone.

⚠️ **Rebuild Kid Character Prefab** regenerates the prefab from code, which discards edits you made to it.

Originals are backed up in `Backups/2026-10-14_before_kid_character/`.

## Tested (Editor Play Mode, Level 2)
- The kid replaces the capsule, and the capsule is no longer drawn.
- Walking: the legs swing (measured while moving). The sneaker toes show at the bottom of the screen when looking down.
- Both hands show hoodie sleeves.
- Sitting in the desk chair: the seated pose fits the chair with nothing poking through.
- No Console errors.

## Not tested
- Real WASD walking: the test moved the CharacterController directly.
- **Level 1 by eye:** it's installed with the same prefab and the same Player setup, but I didn't play it there.
- A standalone build.
