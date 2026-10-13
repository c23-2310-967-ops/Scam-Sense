# First-person phone for text messages

When a text message is opened (press **E** or click the notification), the player pulls a phone out of their pocket, holds it up and reads the message on its screen. The decision card (timer, Details, SCAM / LEGITIMATE, feedback) sits to the right of the phone. After **Continue**, the phone goes back into the pocket, and only then does the player get movement and mouse-look back.

Emails are unchanged: they still use the computer, or the full-screen view in a scene without a computer.

## Files
| Path | Change |
|---|---|
| `Assets/Scripts/Game/PocketPhone.cs` | **New.** Low-poly phone with a holding hand, attached to the player camera. It animates the pull-out (0.6 s) and put-away (0.45 s) along a curved path with rotation and easing, plus a slight breathing sway while held. |
| `Assets/Scripts/UI/MessagePopupUI.cs` | For text messages, its existing phone screen is moved onto the 3D phone. While the phone is out, the dimmer and HUD are hidden and the decision card moves to the right. The setting `Texts On Handheld Phone` (on by default) switches back to the old view. |
| `Assets/Scripts/Game/IMessagePresenter.cs` | Added the optional `IPresenterTiming` interface. |
| `Assets/Scripts/Game/QuizManager.cs` | The response timer only counts once the presenter is ready, i.e. once the phone is up. This is a one-line change. |

The original files are in `Backups/2026-10-14_before_pocket_phone/`.

## Setup
None. `PocketPhone` is added to the camera tagged **MainCamera** the first time a text message opens, in every level.

Settings on the component (visible during Play Mode, or add it to the Main Camera yourself to change them permanently):
- **Hold Distance**: 0.37 m. Keep it between the camera's near clip plane (0.3) and the player's collider radius (0.5), so the phone is never cut by the camera or pushed into walls.
- **Screen Height Fill**: 0.7 (share of the screen height the phone covers).
- **Viewport Y**.
- **Reserved Right Units**: space kept free for the decision card.
- **Pull Out Time** and **Put Away Time**.
- Body, edge and skin colours.

## Behaviour notes
- The phone can't be pulled out twice. QuizManager only opens a message that is waiting, and while the phone is out or moving, the interaction lock blocks E.
- If a new message ever arrived while the phone was going down, it would turn around from where it is.
- The floating hands are hidden while the phone is out. The phone has its own hand, so nothing overlaps.
- The pause menu freezes the animation and the timer.
- Leaving the scene (Main Menu, Replay) removes the phone with the camera.

## Not included
- No rigged or animated hand model exists in the project, so the hand is built from low-poly blocks in code, matching the existing FloatingHand style. A rigged hand with a grip pose could replace it later; you'd swap the blocks built in `PocketPhone.Build()`.
- The SCAM / LEGITIMATE buttons stay on the decision card next to the phone, not on the phone screen. At the screen sizes tested (800×450), buttons on the phone would be small, and the card keeps the timer, Details and feedback together.
