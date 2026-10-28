# Bankshot Arena

A wave-survival FPS in **Unity + C#** where the core mechanics are built on 3D math. Your bullets **ricochet**, and a **temporary, directional armor plate** turns the damage you absorb into a charged return shot.

> Built with an AI coding assistant (Claude), then studied, tuned and extended by me. Every system below is something I can explain line by line.

---

## The twist

| Mechanic | What it does | Why it changes play |
|---|---|---|
| **Ricochet rounds** | Shots reflect off walls up to 3 times. Each bounce adds **+50% damage**, and a kill scores **x(1 + bounces)**. | The arena has a ring of angled deflector walls. Bank shots around cover are the high-skill, high-score play. |
| **Aegis Plate (temporary armor)** | A pickup gives armor for **8 s or 80 points**, whichever runs out first. It flickers before it expires and the pickup respawns after 15 s. | Armor pickups become control points worth fighting over. |
| **Directional coverage** | The plate only blocks hits from your **front 140°**. Hits from the sides or back go straight through. | Armor doesn't make you invincible. Flanking still kills you. |
| **Absorb & Return** | Damage the plate soaks fills a **Charge** meter. Your next shot spends it for **up to 2.5x damage** (purple tracer). | Armor changes your behavior: turn *into* the fire, tank it, then punish. |
| **Enemies lead their shots** | Enemies solve an intercept equation to aim where you'll *be*. Accuracy rises every wave. | Running in a straight line stops working. Strafing and changing direction matter. |

## Controls

| Key | Action |
|---|---|
| WASD | Move |
| Mouse | Look |
| Shift | Sprint |
| Space | Jump |
| Left click | Fire (click once to capture the mouse) |
| R | Reload / restart after death |
| Esc | Release mouse |

## Setup (about 5 minutes)

1. **Unity 2022.3 LTS or Unity 6** (URP or Built-in both work). Create a new **3D** project.
2. **Window > Package Manager > Unity Registry > AI Navigation** > Install.
3. **Edit > Project Settings > Player > Other Settings > Active Input Handling** > set to **Both**. The scripts use the classic `Input` class, and Unity 6 projects default to the new Input System only. Unity restarts.
4. Copy the `Assets/BankshotArena` folder into your project's `Assets` folder.
5. Menu **Tools > Bankshot Arena > Build Demo Scene**. This builds the arena, bakes the NavMesh, and creates the player, enemy and projectile prefabs, armor pickups, spawner and HUD. It saves everything to `Assets/BankshotArena/Generated`.
6. Press **Play**.

No art assets needed: everything is primitives, so the repo stays tiny.

## Project structure

```
Assets/BankshotArena/
├── Scripts/
│   ├── Core/       GameMath.cs (all the math), GameManager.cs, IDamageable.cs
│   ├── Player/     PlayerController.cs, PlayerHealth.cs, TemporaryArmor.cs
│   ├── Weapons/    RicochetWeapon.cs, TracerPool.cs
│   ├── Enemies/    EnemyAI.cs, EnemyHealth.cs, EnemyProjectile.cs, WaveSpawner.cs
│   ├── Pickups/    ArmorPickup.cs
│   └── UI/         HUD.cs
└── Editor/         BankshotSceneBuilder.cs (one-click scene setup)
```

## Where the 3D math lives

All of it is in `GameMath.cs`, with comments deriving each formula.

| Feature | Math | Used in |
|---|---|---|
| Ricochet | Reflection: `r = d - 2(d·n)n` | `RicochetWeapon` |
| Armor front arc, enemy vision cone, "am I facing the player" | Dot product: `dot(a,b) >= cos(θ)` (no `Acos` per frame) | `TemporaryArmor`, `EnemyAI` |
| Weapon spread | Uniform sampling on a cone cap + `Quaternion.LookRotation` | `GameMath.RandomDirectionInCone` |
| Enemy predictive aim | Quadratic intercept: `(V·V − s²)t² + 2(D·V)t + D·D = 0` | `EnemyAI.Fire` |
| Aim skill per wave | `Vector3.Slerp` between direct and predicted aim | `EnemyAI.Fire` |
| Enemy strafing | Cross product: `up × toPlayer` = sideways direction | `EnemyAI` |
| Smooth turning | `Quaternion.RotateTowards` (deg/sec × deltaTime) | `EnemyAI.FaceTowards` |
| Spawn ring | Polar to Cartesian: `(r cos θ, 0, r sin θ)` | `WaveSpawner` |
| Damage direction arrow | World → local space, then `atan2(x, z)` | `HUD` |
| Jump | `v = √(2gh)` | `PlayerController` |
| Mouse look | Yaw on body, clamped pitch on camera pivot (no gimbal flip) | `PlayerController` |

## Performance decisions

The JD mentions *reducing load times and improving frame rates*. These are the choices made for that:

- **Object pool for tracers**: a fixed set of `LineRenderer`s reused round-robin, so firing allocates nothing.
- **Zero-allocation physics**: `RaycastNonAlloc` with a shared buffer for enemy projectiles, and a reused `List` for the bullet path.
- **Continuous collision for projectiles**: each frame raycasts along the distance travelled, so fast bolts can't tunnel through thin walls.
- **Throttled pathfinding**: enemies re-path every 0.2 s instead of calling `SetDestination` every frame.
- **`MaterialPropertyBlock` for hit flashes**: no per-enemy material copies.
- **Frame-rate independence**: all movement and timers scale by `Time.deltaTime`.

## Roadmap (things I'd add next)

- [ ] Pool enemy projectiles and enemies too, and benchmark FPS at 50 / 200 / 500 enemies before and after (Unity Profiler screenshots in this README).
- [ ] Bullet trajectory **preview line** while aiming, showing the first bounce.
- [ ] Damage falloff based on **incidence angle** (`GameMath.IncidenceAngle`): grazing bounces keep more energy.
- [ ] Unit tests for `GameMath` (Unity Test Framework).
- [ ] Mobile touch controls (dual virtual sticks).

## Credits

Code written with the help of Claude (Anthropic) as an AI pair programmer. Math references: Freya Holmér's *Math for Game Devs* lectures and Catlike Coding tutorials.
