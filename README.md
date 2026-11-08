# Arena Shooter

[![Tests](https://github.com/crismand/arena-shooter/actions/workflows/tests.yml/badge.svg?branch=dev)](https://github.com/crismand/arena-shooter/actions/workflows/tests.yml)

A top-down twin-stick shooter made in Unity 6.3. Survive wave after wave of enemies in a single arena, grab power-ups, and chase a high score. Or sit back and watch a reinforcement-learning agent, trained with Unity ML-Agents, play it for you.

![Gameplay: the player in the arena after clearing wave 1](media/gameplay.png)

## Play it

Download the latest Windows build from [Releases](https://github.com/crismand/arena-shooter/releases/latest), extract the zip and run `ArenaShooter.exe` (64-bit Windows 10 or 11). The build isn't code-signed, so SmartScreen may ask you to confirm: **More info → Run anyway**.

### Controls

| | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | WASD or arrow keys | Left stick |
| Aim | Mouse | Right stick |
| Fire | Hold left mouse button | Push the right stick (fires automatically) |
| Pause | Esc | Start |

## Features

- **Five enemy types.** Chasers rush you, Shooters keep their distance and fire back, Tanks are slow but take a beating, Dashers telegraph a charge and then can't steer, and Splitters break into three fast Splitlings when killed.
- **A boss every fifth wave.** It circles you firing turning rings of bullets, summons Chasers below half health, and gets tougher each time; later bosses add aimed bursts. A health bar and a warning banner come with it.
- **Waves.** Six hand-designed waves between the bosses, then endless waves that keep getting bigger and faster.
- **Pickups.** Enemies sometimes drop Spread shot, Rapid fire, Homing shots, a Shield, a Health heart or a screen-clearing Bomb; the boss always drops one.
- **Field guide.** A two-page guide to the enemies and pickups before the first run, and from the title screen.
- **Game feel.** Screen shake, hit-stop, particle bursts, sound effects and music.
- **High score.** Saved between sessions, shown on the title screen, the HUD and the game over screen.
- **Menus.** A title screen, instant restarts, and settings for music and effects volume and fullscreen.
- **Watch AI.** A trained ML-Agents model plays the arena from the title screen; press any button to return to the title.

![Game over screen with score, wave reached, best score, and Restart and Quit buttons](media/game-over.png)

## Working on the project

### Requirements

- Unity **6000.3.26f1** (6.3 LTS)
- [Git LFS](https://git-lfs.com/): sprites, sounds, the font atlas and the trained model are stored with LFS. Run `git lfs install` once before cloning.

### Getting started

1. Clone the repository and open the folder with Unity Hub.
2. Open `Assets/_Project/Scenes/Arena.unity` and press Play.

### How it's put together

All game code is in `Assets/_Project/Scripts`, split into assemblies (`AS.Core`, `AS.Combat`, `AS.Player`, `AS.Enemies`, `AS.Waves`, `AS.Pickups`, `AS.Feedback`, `AS.UI`), plus `AS.Agents` for the ML-Agents player, which no game assembly references. Systems don't reference each other directly: they publish and listen to events through `AS.Core.EventBus`. Tuning lives in ScriptableObject assets under `Assets/_Project/Data`.

The scene, prefabs, placeholder sprites and sounds, audio mixer and physics layers are all **generated from code** by `Assets/_Project/Editor/SceneBuilder.cs`. To regenerate them, use the menu **AS → Setup → Build Arena Scene**. Data assets, sprites and sounds are only created if missing, so your tuning survives a rebuild. The scene and prefabs are rebuilt every time, so put permanent changes to them in `SceneBuilder`.

### The ML agent

`ArenaAgent` plays the player through the same `IPlayerInput` seam as a person. It sees every enemy and pickup and the 24 nearest enemy bullets through two buffer sensors, walls through 2D ray sensors, and its own position, velocity, health, aim, cooldown, enemies left and wave. It's rewarded for kills, clearing waves and collecting pickups, and pays for every hit, for dying, and for every step while enemies remain. It was trained with PPO in a headless training build; `ml/` has the pinned Python environment, the config, `train.ps1` and `evaluate.py` (see [ml/README.md](ml/README.md)). The shipped model, `Assets/_Project/Models/ArenaAgent.onnx`, runs on Inference Engine in the game, with no Python needed.

The shipped model is `m3_001`, trained for 10M steps. Over 50 evaluation episodes each in the v1.3.0 arena, starting at wave 1:

| Policy | Survival | Kills | Wave reached | Died |
|---|---|---|---|---|
| Idle | 9.0 s | 0 | 1.0 | 100% |
| Random | 11.4 s | 2.3 | 1.0 | 100% |
| `m1_001` (v1.2.0's model) | 151.6 s | 12.9 | 2.0 | 58% |
| **`m3_001`** | 82.8 s | **21.1** | **3.0** | 88% |

`m3` kills more and gets further than `m1`, which lasts longer by keeping out of the way rather than clearing waves. Neither has reached the boss yet.

The design, training setup and what's next for the agent are on the wiki's [ML Agent](https://github.com/crismand/arena-shooter/wiki/ML-Agent) page.

### Tests

The tests live in `Assets/_Project/Tests`. Run them from **Window → General → Test Runner**:

- **EditMode** (`AS.Tests.EditMode`): the event bus and arena channels, state machine, high score and settings storage, wave and boss scaling and object pool, the agent's action mapping, that the shipped model matches the agent's sensors, plus checks that every data asset and prefab is wired up correctly and that no game assembly depends on ML-Agents.
- **PlayMode** (`AS.Tests.PlayMode`): play the real scene with simulated keyboard and mouse input, plus the enemies, pickups and boss, the training scene's episodes, rewards and sensors, and Watch AI.

They can also run headless with the editor closed:

```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames AS.Tests.EditMode -testResults editmode.xml
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -assemblyNames AS.Tests.PlayMode -testResults playmode.xml
```

### Building

Use the menu **AS → Build → Windows (64-bit)**, or build headless:

```bash
Unity.exe -batchmode -quit -projectPath . -executeMethod AS.EditorTools.BuildScript.BuildWindows
```

The build goes to `Builds/Windows/ArenaShooter.exe`; add `-development` for a development build.

### Branches

`main` holds released versions, each tagged (`v1.0.0`, …). Day-to-day work branches off `dev` and merges back into it, and `dev` is merged into `main` through a pull request for a release.

### CI

GitHub Actions run the tests in Unity using [GameCI](https://game.ci/):

| Workflow | Runs when | What it does |
|---|---|---|
| [Tests](.github/workflows/tests.yml) | Push to `dev` (unless only docs changed), any pull request into `main`, or started by hand | EditMode and PlayMode tests, as two parallel jobs |
| [Release](.github/workflows/release.yml) | A `v*` tag is pushed | All tests, then a Windows build, then a **draft** release with the zipped build attached |

Unity needs a license to run in CI. The workflows read three repository secrets, which the repository owner sets once:

```bash
gh secret set UNITY_LICENSE --repo crismand/arena-shooter < "C:\ProgramData\Unity\Unity_lic.ulf"
gh secret set UNITY_EMAIL --repo crismand/arena-shooter
gh secret set UNITY_PASSWORD --repo crismand/arena-shooter
```

The `.ulf` file appears after activating a Personal license in Unity Hub (Preferences → Licenses). Pull requests from forks don't receive secrets, so their Unity jobs can't run.

### Releasing

1. Merge `dev` into `main` through a pull request (the tests must pass).
2. Tag the merge commit on `main` and push the tag:
   ```bash
   git tag -a v1.3.0 -m "Arena Shooter v1.3.0"
   git push origin v1.3.0
   ```
3. The Release workflow tests, builds and creates a draft release with `ArenaShooter-v1.3.0-windows-x64.zip`. Edit the notes on GitHub, then press **Publish release**.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for what's done and what might come next.
