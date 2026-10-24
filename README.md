# Arena Shooter

[![Tests](https://github.com/crismand/arena-shooter/actions/workflows/tests.yml/badge.svg?branch=dev)](https://github.com/crismand/arena-shooter/actions/workflows/tests.yml)

A top-down twin-stick shooter made in Unity 6.3. Survive wave after wave of enemies in a single arena, grab power-ups, and chase a high score.

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

- **Three enemy types.** Chasers rush you, Shooters keep their distance and fire back, and Tanks are slow but take a beating.
- **Waves.** Four hand-designed waves, then endless waves that keep getting bigger and faster.
- **Power-ups.** Enemies sometimes drop Spread shot, Rapid fire or a Shield.
- **Game feel.** Screen shake, hit-stop, particle bursts, sound effects and music.
- **High score.** Saved between sessions, shown on the HUD and the game over screen.

![Game over screen with score, wave reached, best score, and Restart and Quit buttons](media/game-over.png)

## Working on the project

### Requirements

- Unity **6000.3.26f1** (6.3 LTS)
- [Git LFS](https://git-lfs.com/): sprites, sounds and the font atlas are stored with LFS. Run `git lfs install` once before cloning.

### Getting started

1. Clone the repository and open the folder with Unity Hub.
2. Open `Assets/_Project/Scenes/Arena.unity` and press Play.

### How it's put together

All game code is in `Assets/_Project/Scripts`, split into assemblies (`AS.Core`, `AS.Combat`, `AS.Player`, `AS.Enemies`, `AS.Waves`, `AS.Pickups`, `AS.Feedback`, `AS.UI`). Systems don't reference each other directly: they publish and listen to events through `AS.Core.EventBus`. Tuning lives in ScriptableObject assets under `Assets/_Project/Data`.

The scene, prefabs, placeholder sprites and sounds, audio mixer and physics layers are all **generated from code** by `Assets/_Project/Editor/SceneBuilder.cs`. To regenerate them, use the menu **AS → Setup → Build Arena Scene**. Data assets, sprites and sounds are only created if missing, so your tuning survives a rebuild. The scene and prefabs are rebuilt every time, so put permanent changes to them in `SceneBuilder`.

### Tests

The tests live in `Assets/_Project/Tests`. Run them from **Window → General → Test Runner**:

- **EditMode** (`AS.Tests.EditMode`): the event bus, state machine, high score storage, wave scaling and object pool, plus checks that every data asset and prefab is wired up correctly.
- **PlayMode** (`AS.Tests.PlayMode`): play the real scene with simulated keyboard and mouse input.

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
   git tag -a v1.1.0 -m "Arena Shooter v1.1.0"
   git push origin v1.1.0
   ```
3. The Release workflow tests, builds and creates a draft release with `ArenaShooter-v1.1.0-windows-x64.zip`. Edit the notes on GitHub, then press **Publish release**.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for what's done and what might come next.
