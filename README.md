  Scam-Sense

> A first-person Unity game that teaches players how to recognize and avoid scams. *(Capstone Project)*

<!-- Add a banner or screenshot here, e.g.: ![Scam-Sense Screenshot](docs/screenshot.png) -->

---

## 📖 About the Project

**Scam-Sense** is a capstone project built with **Unity** and **C#**. Players step into the role of an investigator who receives briefings, explores the environment, and works through a series of **cases** to spot the warning signs of scams.

> ✏️ **Edit this section:** Add 2–3 sentences about your game's story, goal, and the problem it addresses (for example, rising online scams and low public awareness).

## ✨ Features

- 🎮 **First-person controls**: smooth player movement and mouse look
- 🪑 **Interactive environment**: sit on chairs, open doors, and interact with objects
- 📋 **Case system**: briefings and transitions between new cases
- 🖥️ **Atmospheric effects**: screen flickering and glitch effects
- 🧭 **Full menu system**: main menu, pause menu, settings, and exit confirmation
- 🔊 **Audio feedback**: button click sounds
- 🎬 **Scene management**: scene loading with fade-in transitions

## 🛠️ Built With

| Tool | Purpose |
|------|---------|
| [Unity](https://unity.com/) | Game engine |
| C# | Gameplay scripting |
| Git & GitHub | Version control |

## 📂 Project Structure

```
Scam-Sense/
├── Player
│   ├── PlayerMotor.cs            # Player movement
│   ├── Mouselook.cs              # Camera look controls
│   ├── Seatedheadlook.cs         # Head movement while seated
│   ├── InputManager.cs           # Handles player input
│   ├── PlayerInteractor.cs       # Interaction with objects
│   ├── PlayerSitInteractor.cs    # Sitting interaction
│   └── FloatingHand.cs           # Hand/interaction visual
│
├── Interactables
│   ├── Door.cs                   # Door behavior
│   ├── ChairInteractables.cs     # Chair interaction logic
│   └── Sittablechair.cs          # Sittable chair behavior
│
├── Game Flow
│   ├── BriefingGlitch.cs         # Glitch effect during briefing
│   ├── NewCaseTransition.cs      # Transition between cases
│   ├── Sceneloader.cs            # Loads scenes
│   ├── Scenefadein.cs            # Fade-in effect
│   └── ScreenFlickering.cs       # Screen flicker effect
│
└── UI
    ├── MenuButtons.cs            # Menu button logic
    ├── Menu Actions.cs           # Menu actions
    ├── Button Sounds.cs          # Button sound effects
    ├── PauseMenu.cs              # Pause menu
    ├── SettingsMenu.cs           # Settings menu
    └── ExitConfirmation.cs       # Exit confirmation prompt
```

> ℹ️ The folders above are grouped for readability. In the repository, all scripts are currently in the root folder.

## 🚀 Getting Started

### Prerequisites

- [Unity Hub](https://unity.com/download)
- Unity Editor *(version: **X.X.X**; check `ProjectSettings/ProjectVersion.txt`)*
- [Git](https://git-scm.com/)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/c23-2310-967-ops/Scam-Sense.git
   ```
2. **Open Unity Hub** and click **Add → Add project from disk**.
3. Select the cloned `Scam-Sense` folder.
4. Open the project with the matching Unity version.
5. Open the main scene in `Assets/Scenes` and press **Play ▶**.

## 🎮 Controls

> ✏️ **Edit to match your input settings.**

| Action | Key |
|--------|-----|
| Move | `W` `A` `S` `D` |
| Look around | Mouse |
| Interact (doors, objects) | `E` |
| Sit / Stand | `E` |
| Pause | `Esc` |


## 👥 Team

|------|------|
| Polly Jireh Mane | Developer |Designer | Project Manager
| Andrew Tenorio | Documentary |
| Prince Moses Jimenez | Documentary |Designer
| Chester Guille Castillo| Developer

## 🔮 Future Improvements

- [ ] Add more scam cases and scenarios
- [ ] Add a scoring or progress system
- [ ] Add voice acting and background music
- [ ] Release a downloadable build

## 📄 License

This project was created as a capstone for academic purposes.
<!-- Or add a license, e.g.: Distributed under the MIT License. See `LICENSE` for details. -->

## 🙏 Acknowledgements

- [Unity Documentation](https://docs.unity3d.com/)
- Our capstone adviser and instructors

---
