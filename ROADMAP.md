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

## Ideas for next

Nothing below is committed to yet. These are candidates, roughly in the order they would pay off.

### Small improvements
- [ ] Quit button in the pause menu as well.
- [ ] Settings menu: music and effects volume (through the existing Music and SFX mixer groups), fullscreen toggle.
- [ ] Title screen with Play, Settings and Quit, plus the best score.
- [ ] Gamepad rumble on hits and deaths.

### More game
- [ ] New enemies: a Dasher that charges in straight lines, a Splitter that breaks into smaller enemies.
- [ ] A boss every few waves.
- [ ] New pickups: a health pickup, a screen-clearing bomb, homing shots.
- [ ] Score multiplier for kills in quick succession.
- [ ] Local top-10 leaderboard instead of a single best score.

### Presentation
- [ ] Replace the placeholder shapes, sounds and music with real art and audio.
- [ ] Simple animation: squash and stretch on spawn and death, a muzzle flash.

### Reach and tooling
- [ ] WebGL build that runs in a browser (for example on itch.io).
- [x] GitHub Actions that run the EditMode and PlayMode tests on pushes to `dev` and pull requests into `main`, and attach a Windows build to tagged releases.
