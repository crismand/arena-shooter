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

## Done: v1.1.0

Instant restarts and the menus the game was missing. Also shipped the groundwork for the ML-Agents player (steps 1 to 3 below), CI, and the movement input fix.

- [x] Instant restart: a run resets in place instead of reloading the scene (step 3 of the ML-Agents player).
- [x] Quit button in the pause menu as well.
- [x] Settings menu: music and effects volume (through the existing Music and SFX mixer groups), fullscreen toggle.
- [x] Title screen with Play, Settings and Quit, plus the best score.

## Next: v1.2.0, the ML-Agents player

Train a reinforcement-learning agent with [Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents) (PPO) to play the arena, and ship it as a Watch AI mode. Later, use it as an automated playtester for new content. The full plan, agent design and training setup are on the wiki's [ML Agent](https://github.com/crismand/arena-shooter/wiki/ML-Agent) page.

The first three steps are refactors that more arenas and bosses need anyway, so they came first and shipped in v1.1.0.

| Step | What it adds | Ships in |
|---|---|---|
| 1. Input seam | An `IPlayerInput` interface between the player and the Input System, so either a person or an agent can drive `PlayerController` and `PlayerAim`. Done. | v1.1.0 |
| 2. Arena container | An `Arena` root that owns an event channel; gameplay under it publishes there, and it forwards to the global bus unless it's an unwatched training arena. Done. | v1.1.0 |
| 3. In-place reset | Restart a run without reloading the scene; needed for training episodes and boss retries. Done. | v1.1.0 |
| 4. ML-Agents package | `com.unity.ml-agents` (the 4.x line for Unity 6, on Inference Engine) and a separate `AS.Agents` assembly, so the game's own assemblies never depend on it. The matching Python `mlagents` version is pinned in the same change. CI only has to resolve the package and never needs Python. | v1.2.0 |
| 5. Agent and training scene | `ArenaAgent` (ray sensors, move/aim/fire actions, kill and survival rewards), `AgentInput`, and Enemy, EnemyProjectile and Wall tags for the ray sensor. A stripped-down `Training` scene with no `GameManager`, UI, music, screen shake or hit-stop, since `GameManager` and `HitStop` both set the global `Time.timeScale` that training runs at 20; episodes call `Arena.ResetArena()` directly. Tests for the action mapping, the episode reset and the prefab tags. | v1.2.0 |
| 6. Python training and evaluation | An `ml/` folder with a pinned Python environment, a PPO config, a headless training build (**AS → Build → Training**), and TensorBoard. An evaluation run plays a fixed number of episodes each with the trained, a random and an idle agent and reports mean survival time and kills. | v1.2.0 |
| 7. Watch AI | The trained model shipped with the game, behind a Watch AI button on the title screen; any input returns to the title. An idle attract mode can come later. | v1.2.0 |
| 8. Release | README, wiki and roadmap updated, and the v1.2.0 tag. | v1.2.0 |

The ML part is done when, over the evaluation run, the trained agent beats both the random and the idle agent on mean survival time and on mean kills, and plays the real arena in the game.

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
