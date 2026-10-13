# Scam-Sense: Think Before You Click

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
- 🏠 **Level 1 — The First Case**: a home where everyday texts and emails arrive while you explore
- 🕵️ **Level 2 — The Investigation**: a cybersecurity office with an evidence board and investigation details for every message
- 📱 **First-person phone**: pull your phone out of your pocket to read and answer text messages
- 🖥️ **Working computers**: read emails on the in-game monitor like a real desktop mail app
- ⏱️ **Scoring and stars**: faster correct answers earn more points; reach the passing score to unlock the next room
- 🖥️ **Atmospheric effects**: screen flickering, glitch effects, soft lighting and shadows
- 🧭 **Full menu system**: main menu, level selection, pause menu, settings, and exit confirmation
- 🔊 **Audio feedback**: notification, answer, and button sounds
- 🎬 **Scene management**: scene loading with fade-in transitions

## 🛠️ Built With

| Tool | Purpose |
|------|---------|
| [Unity](https://unity.com/) | Game engine |
| C# | Gameplay scripting |
| Git & GitHub | Version control |

## 📂 Project Structure

This repository is the complete Unity project.

```
Scam-Sense/
├── Assets/
│   ├── Scenes/                   # Menu, Game (Level 1), Room2_InvestigationOffice (Level 2)
│   ├── Scripts/
│   │   ├── Data/                 # Level, message and save-data definitions
│   │   ├── Game/                 # Quiz flow, scoring, computers, pocket phone, level audio
│   │   ├── UI/                   # Main menu, HUD, briefing, message panels, results, UI kit
│   │   └── *.cs                  # Player, doors, chairs, menus, scene loading, effects
│   ├── Editor/                   # Editor tools (e.g. contact-shadow builder)
│   ├── Levels/                   # Level data assets (messages, scoring, unlock rules)
│   ├── Model/, Materials/, Textures/, Fonts/, ...   # Art and audio assets
├── Packages/                     # Unity package manifest
├── ProjectSettings/              # Unity project settings
├── Docs/                         # Feature notes and setup guides
└── README.md
```

Key gameplay scripts:

| Script | Purpose |
|--------|---------|
| `PlayerMotor.cs`, `Mouselook.cs`, `InputManager.cs` | First-person movement and look |
| `PlayerInteractor.cs`, `Door.cs`, `Sittablechair.cs` | Interactions with doors and chairs |
| `Game/QuizManager.cs` | Message sequence, response timer and answers |
| `Game/ScoringSystem.cs`, `Game/GameManager.cs` | Points, stars, saving and room unlocking |
| `Game/ComputerTerminal.cs`, `Game/PocketPhone.cs` | Reading emails on monitors and texts on the phone |
| `UI/MainMenuUI.cs`, `UI/LevelHUD.cs`, `UI/ResultsUI.cs` | Main menu, in-game HUD and results screen |
| `PauseMenu.cs`, `SettingsMenu.cs`, `Sceneloader.cs` | Pause, settings and scene transitions |

## 🚀 Getting Started

### Prerequisites

- [Unity Hub](https://unity.com/download)
- Unity Editor **6000.0.38f1** (Unity 6), Built-in Render Pipeline
- [Git](https://git-scm.com/)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/c23-2310-967-ops/Scam-Sense.git
   ```
2. **Open Unity Hub** and click **Add → Add project from disk**.
3. Select the cloned `Scam-Sense` folder.
4. Open the project with the matching Unity version.
5. Open `Assets/Scenes/Menu.unity` and press **Play ▶**. (The first open takes a few minutes while Unity rebuilds its `Library` folder.)

## 🎮 Controls

> ✏️ **Edit to match your input settings.**

| Action | Key |
|--------|-----|
| Move | `W` `A` `S` `D` |
| Look around | Mouse |
| Interact (doors, objects) | `E` |
| Sit / Stand | `E` |
| Read a text message (pull out phone) | `E` |
| Show / hide objectives | `Tab` |
| Pause | `Esc` |


## 👥 Team

| Name | Role |
|------|------|
| Polly Jireh Mane | Developer, Designer, Project Manager |
| Andrew Tenorio | Documentary |
| Prince Moses Jimenez | Documentary, Designer |
| Chester Guille Castillo | Developer |

## 🔮 Future Improvements

- [ ] Add more scam cases and scenarios
- [x] Add a scoring or progress system
- [ ] Add voice acting and background music
- [ ] Release a downloadable build

## 📄 License

This project was created as a capstone for academic purposes.
<!-- Or add a license, e.g.: Distributed under the MIT License. See `LICENSE` for details. -->

## 🙏 Acknowledgements

- [Unity Documentation](https://docs.unity3d.com/)
- Our capstone adviser and instructors

---
