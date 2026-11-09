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

Instant restarts and the menus the game was missing. Also shipped the groundwork for the ML-Agents player (steps 1 to 3 of v1.2.0), CI, and the movement input fix.

- [x] Instant restart: a run resets in place instead of reloading the scene (step 3 of the ML-Agents player).
- [x] Quit button in the pause menu as well.
- [x] Settings menu: music and effects volume (through the existing Music and SFX mixer groups), fullscreen toggle.
- [x] Title screen with Play, Settings and Quit, plus the best score.

## Done: v1.2.0, the ML-Agents player

A reinforcement-learning agent trained with [Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents) (PPO) to play the arena, shipped as a Watch AI mode on the title screen. The full plan, agent design and training setup are on the wiki's [ML Agent](https://github.com/crismand/arena-shooter/wiki/ML-Agent) page.

The first three steps are refactors that more arenas and bosses need anyway, so they came first and shipped in v1.1.0.

| Step | What it adds | Ships in |
|---|---|---|
| 1. Input seam | An `IPlayerInput` interface between the player and the Input System, so either a person or an agent can drive `PlayerController` and `PlayerAim`. Done. | v1.1.0 |
| 2. Arena container | An `Arena` root that owns an event channel; gameplay under it publishes there, and it forwards to the global bus unless it's an unwatched training arena. Done. | v1.1.0 |
| 3. In-place reset | Restart a run without reloading the scene; needed for training episodes and boss retries. Done. | v1.1.0 |
| 4. ML-Agents package | `com.unity.ml-agents` (the 4.x line for Unity 6, on Inference Engine) and a separate `AS.Agents` assembly, so the game's own assemblies never depend on it. The matching Python `mlagents` version is pinned in the same change. CI only has to resolve the package and never needs Python. Done. | v1.2.0 |
| 5. Agent and training scene | `ArenaAgent` (ray sensors, move/aim/fire actions, kill and survival rewards), `AgentInput`, and Enemy, EnemyProjectile and Wall tags for the ray sensor. A stripped-down `Training` scene with no `GameManager`, UI, music, screen shake or hit-stop, since `GameManager` and `HitStop` both set the global `Time.timeScale` that training runs at 20; episodes call `Arena.ResetArena()` directly. Tests for the action mapping, the episode reset and the prefab tags. Done. | v1.2.0 |
| 6. Python training and evaluation | An `ml/` folder with a pinned Python environment, a PPO config, a headless training build (**AS → Build → Training**), and TensorBoard. An evaluation run plays a fixed number of episodes each with the trained, a random and an idle agent and reports mean survival time and kills. Done. | v1.2.0 |
| 7. Watch AI | The trained model shipped with the game, behind a Watch AI button on the title screen; any input returns to the title. An idle attract mode can come later. Done. | v1.2.0 |
| 8. Release | README, wiki and roadmap updated, and the v1.2.0 tag. Done. | v1.2.0 |

The pass mark: over the evaluation run, the trained agent beats both the random and the idle agent on mean survival time and on mean kills, and plays the real arena in the game. It passed: over 50 episodes each, the trained agent survived 48.6 s on average with 11.5 kills, against 13.7 s and 2.6 kills for a random agent and 9.3 s and none for an idle one.

Later milestones for the agent: a playtester that reports difficulty per wave, room or boss; self-play duels; learning from recorded human play; a curriculum across several arenas.

## Done: v1.3.0, more game and a smarter agent

Two new enemies, three new pickups and a boss, then a new agent trained on the bigger game. The game content comes first so that the agent learns the game as it ships. Each step is a branch off `dev`.

| Step | What it adds |
|---|---|
| 1. Dasher and Splitter | **Dasher**: stops, telegraphs (flashes and turns to face the player), then charges in a straight line and skids to a stop; it can't steer mid-charge, so sidestepping beats it. **Splitter**: a slow, tough blob that breaks into three small fast Splitlings when killed; the children count toward the wave, so it isn't cleared until they're dead too. New authored waves 5 and 6 introduce them before endless scaling takes over. |
| 2. New pickups | **Health**: restores one point at once, up to the maximum. **Bomb**: on pickup, damages every enemy in the arena and clears enemy bullets, with a big shake and flash. **Homing**: a timed weapon override whose bullets steer toward the nearest enemy. Drop weights per pickup, so Health and Bomb stay rarer than the weapon boosts. |
| 3. Boss | Every fifth wave is a boss wave: one large boss with two attack phases (turning rings of slow bullets, then fewer rings plus summoned Chasers below half health, with aimed three-way bursts from the second boss on). A boss health bar on the HUD, a warning banner when it arrives, and a guaranteed pickup drop. Endless scaling grows the boss's health on later boss waves. |
| 4. Agent m3 | What watching `m1` showed, plus the new content: a `BufferSensor` listing every enemy (type, position, velocity, health, whether it's charging) and pickup (type, position), plus the number of enemies left and the wave, with the rays kept for walls and bullets. Rewards: clearing a wave pays (a boss wave pays double), each step costs a little while enemies remain (replacing the survival reward, which made waiting pay), collecting a pickup pays a little, and hits and deaths cost more than they did for `m1`. Three `m2` runs stalled around wave 2: the first two with these observations (the second with the bigger hit, death and wave rewards), and a third that started episodes at random waves 1-5, which made it worse at the early waves. `m3` adds a second `BufferSensor` with the 24 nearest enemy bullets (position and velocity), since the rays missed most of a spread; trained fresh for 10M steps, it got a wave further than `m2`. |
| 5. Release | README, wiki (Gameplay, ML Agent, Releases) and roadmap updated, Watch AI on `m3`, and the v1.3.0 tag. Done. |

The pass mark was: over 50 evaluation episodes in the v1.3.0 arena, beat `m1` on mean survival time and mean kills, reach the first boss wave in most episodes, and keep beating the random and idle agents. `m3` ships having met part of it. It beats `m1` on kills (21.1 against 12.9) and waves reached (3.0 against 2.0), and both baselines. It doesn't beat `m1` on survival (83 s against 152 s), because `m1` lasts by keeping out of the way rather than clearing waves. It never reaches the boss. The boss stays the target for the next agent.

## Next: v1.4.0, replay value, feel and agent m4

More reason to replay a run, a game that feels better to play, and an agent that aims. The game changes leave enemies, pickups and the arena as they are, so the agent sees the same game and `m4` can continue from `m3`'s model. They also don't depend on the agent, so they can be built while `m4` trains. Each step is a branch off `dev`.

### Game

| Step | What |
|---|---|
| 1. Score combo | Kills in quick succession raise a score multiplier (for example up to x5, dropping back after about 2 s without a kill), shown on the HUD next to the score. Bomb kills count. The agent never sees the score, so its training is unaffected. |
| 2. Top-10 board | A local top-10 instead of the single best score: three initials entered at game over (keyboard or gamepad) when a score makes the board, and the board on the title screen. Saved like the best score is now, which it replaces. |
| 3. Game feel | Squash and stretch when enemies spawn and die, a muzzle flash on the player's shots, and gamepad rumble on hits taken and on death, with a rumble toggle in Settings. |
| 4. Watch AI overlay | While watching, a panel shows the agent's kills, wave and accuracy (hits per shot), and a "From the boss" option starts the watch at wave 5, using the start-wave support `ResetArena` already has, so you don't wait through four waves to see the boss fight. |

### Agent m4: aiming

Watching `m3` in v1.3.0: it moves well and aims well once it has fixed on a target, but between targets its aim swings around and it fires nonstop, which looks much like the random agent. That fits how it was trained. Aim only matters when a shot could hit something, firing costs nothing, and only kills pay, which is rare feedback (a Tank takes several hits). These changes only touch rewards, not observations or actions, so the model's inputs stay the same and `m4` can continue from `m3` (`-InitializeFrom m3_001`) instead of training fresh.

| Step | What |
|---|---|
| 5. Hit reward | A small reward for each of the player's shots that hits an enemy (around +0.05, against +0.3 per kill), so aiming gets feedback on every hit and not only on kills. Counted from the player's projectiles, not from `EnemyDamaged`, which a Bomb pickup also raises. |
| 6. Shot cost | A small cost per shot fired (around −0.001: holding fire with the Blaster for the 83 s `m3` lasts costs about 0.7), so firing at nothing stops paying and it learns to hold fire between targets. Per shot rather than per step, so Rapid and Spread cost what they fire. |
| 7. Steadier aim | Optional, if steps 5 and 6 leave the aim restless: a small cost on large aim changes between decisions. |
| 8. Train and compare | `m4_001` from `m3_001` with the new rewards, evaluated over 50 episodes from wave 1 against `m3`'s saved results (`evaluate.py --against`). `evaluate.py` also reports accuracy (hits per shot), which needs a hits and shots stat from the agent. |

The pass mark: over 50 evaluation episodes, beat `m3` on accuracy without doing worse on kills or waves reached, and keep beating the random and idle agents. Reaching the boss on wave 5 stays the bigger target. If accuracy improves but waves don't, the next things to try are training longer and a start-wave curriculum that only moves on once waves 1-4 are reliable.

### Release

| Step | What |
|---|---|
| 9. Release | README, wiki (Gameplay, ML Agent, Releases) and roadmap updated, Watch AI on `m4` if it passes, and the v1.4.0 tag. Also settle the local `SENTIS_ANALYTICS_ENABLED` change in ProjectSettings that has stayed uncommitted since v1.3.0: commit it or revert it. |

## Ideas for next

Nothing below is committed to yet. These are candidates, roughly in the order they would pay off.

### More game
- [ ] More arenas, moved between without reloading the whole game. A new arena changes what the agent sees, so it fits a release whose agent trains fresh.

### Presentation
- [ ] Replace the placeholder shapes, sounds and music with real art and audio.

### Reach and tooling
- [ ] WebGL build that runs in a browser (for example on itch.io). Check first that Watch AI's model runs on the web.
- [x] GitHub Actions that run the EditMode and PlayMode tests on pushes to `dev` and pull requests into `main`, and attach a Windows build to tagged releases.
