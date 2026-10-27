# Roadmap

## Done: v1.0.0

The game was built in six steps, each on its own branch and merged into `main`.

| Step | What it added |
|---|---|
| 1. Move and shoot | Eight-way movement, mouse and right-stick aiming, pooled bullets, the arena and a following camera. |
| 2. First enemy | Chaser enemy, health and contact damage, enemy spawner. |
| 3. Game flow | Wave director, score, HUD, pause menu, game over and restart. |
| 4. Variety | Shooter and Tank enemies; Spread, Rapid and Shield power-ups dropped by enemies. |
| 5. Polish | Screen shake, hit-stop, particle bursts, sound effects and music, saved high score, TextMesh Pro UI. |
| 6. Tests and build | EditMode tests, asset validation, a Windows build script; a Quit button on the game over screen. |

## Next: v1.1.0

Instant restarts and the menus the game is missing. Also ships the groundwork already on `dev`: CI, the movement input fix, and steps 1 and 2 of the ML-Agents player below.

- [x] Instant restart: a run resets in place instead of reloading the scene (step 3 of the ML-Agents player).
- [x] Quit button in the pause menu as well.
- [x] Settings menu: music and effects volume (through the existing Music and SFX mixer groups), fullscreen toggle.
- [x] Title screen with Play, Settings and Quit, plus the best score.

## Then: v1.2.0, the ML-Agents player

Train a reinforcement-learning agent with [Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents) (PPO) to play the arena, and ship it as an "AI plays" mode. Later, use it as an automated playtester for new content. The full plan, agent design and training setup are on the wiki's [ML Agent](https://github.com/crismand/arena-shooter/wiki/ML-Agent) page.

The first three steps are refactors that more arenas and bosses need anyway, so they come first and ship in v1.1.0.

| Step | What it adds | Ships in |
|---|---|---|
| 1. Input seam | An `IPlayerInput` interface between the player and the Input System, so either a person or an agent can drive `PlayerController` and `PlayerAim`. Done. | v1.1.0 |
| 2. Arena container | An `Arena` root that owns an event channel; gameplay under it publishes there, and it forwards to the global bus unless it's an unwatched training arena. Done. | v1.1.0 |
| 3. In-place reset | Restart a run without reloading the scene; needed for training episodes and boss retries. | v1.1.0 |
| 4. ML-Agents package | `com.unity.ml-agents` and a separate `AS.Agents` assembly, so the game's own assemblies never depend on it. | v1.2.0 |
| 5. Agent and training scene | `ArenaAgent` (ray sensors, move/aim/fire actions, kill and survival rewards) and a stripped-down `Training` scene. | v1.2.0 |
| 6. Python training | An `ml/` folder with a pinned Python environment, a PPO config, a headless training build, and TensorBoard. | v1.2.0 |
| 7. AI plays | The trained model shipped with the game, as an attract mode on the title screen or a Watch AI option. | v1.2.0 |

The ML part is done when the trained agent clearly beats a random agent on survival time and kills, and plays the real arena in the game.

Later milestones: the agent as a playtester that reports difficulty per wave, room or boss; self-play duels; learning from recorded human play; a curriculum across several arenas.

## Ideas for next

Nothing below is committed to yet. These are candidates, roughly in the order they would pay off.

### Small improvements
- [ ] Gamepad rumble on hits and deaths.

### More game
- [ ] New enemies: a Dasher that charges in straight lines, a Splitter that breaks into smaller enemies.
- [ ] A boss every few waves.
- [ ] More arenas, moved between without reloading the whole game.
- [ ] New pickups: a health pickup, a screen-clearing bomb, homing shots.
- [ ] Score multiplier for kills in quick succession.
- [ ] Local top-10 leaderboard instead of a single best score.

### Presentation
- [ ] Replace the placeholder shapes, sounds and music with real art and audio.
- [ ] Simple animation: squash and stretch on spawn and death, a muzzle flash.

### Reach and tooling
- [ ] WebGL build that runs in a browser (for example on itch.io).
- [x] GitHub Actions that run the EditMode and PlayMode tests on pushes to `dev` and pull requests into `main`, and attach a Windows build to tagged releases.
