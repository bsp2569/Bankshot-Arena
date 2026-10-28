# Bankshot Arena

A small wave-survival FPS I made in Unity and C#. I wanted to get properly comfortable with the 3D math behind games, so instead of building a standard shooter I designed two mechanics that can't work without it: bullets that bounce off walls, and armor that only protects you from the front.

## How it plays

Enemies come at you in waves. Each wave there are more of them and they aim better.

**Your bullets ricochet.** Every shot can bounce off walls up to three times, and each bounce makes it hit harder. Kills after a bounce are also worth more points, so a two-bounce kill scores three times as much as a direct hit. The arena has a ring of angled walls in the middle, and you'll get better scores once you start using them to shoot around corners.

**Armor is temporary and one-sided.** There are a few glowing crystals around the map. Walk through one and you get the Aegis Plate for 8 seconds, or until it has absorbed 80 damage. It only covers the front of you, so if an enemy gets behind you it does nothing. It flickers just before it runs out, and the crystal comes back 15 seconds later.

**Blocked damage comes back.** Whatever the plate absorbs charges up your next shot, up to 2.5x damage. You can tell a shot is charged because the tracer turns purple. In practice this means that when you have armor, you want to turn and face the enemies, take the hits, and fire back.

**Enemies aim where you're going.** They work out where you'll be by the time their shot arrives and aim there. Early waves are bad at it, but by wave five or so running in a straight line will get you killed.

## Controls

WASD to move, mouse to look, Shift to sprint, Space to jump, left click to shoot, R to reload. Click the game window once to capture the mouse, and press Esc to let go of it. When you die, press R to restart.

## Running it

You'll need Unity 2022.3 LTS or Unity 6. Either the Built-in render pipeline or URP is fine.

1. Create a new 3D project.
2. In Package Manager, install **AI Navigation** (it's under Unity Registry). The enemies need it for pathfinding.
3. Go to Project Settings > Player > Other Settings and set **Active Input Handling** to **Both**. The scripts use the classic `Input` class, and new Unity 6 projects turn that off by default.
4. Copy the `Assets/BankshotArena` folder into your project's `Assets` folder.
5. From the menu bar, run **Tools > Bankshot Arena > Build Demo Scene**, then press Play.

That menu item builds the whole level for you: the arena, the navmesh, the player, enemies, pickups and the HUD. Everything is made from basic shapes, so you don't need to download any art.

## The math

Most of it lives in `Scripts/Core/GameMath.cs`, and I've written the reasoning for each formula in the comments there. Briefly:

- **Ricochets** reflect the bullet's direction off the wall using `r = d − 2(d·n)n`, where `n` is the direction the wall faces.
- **The armor's front arc** and **the enemies' field of view** both use the dot product. For two unit vectors, `dot(a, b)` is the cosine of the angle between them, so comparing it to `cos(70°)` tells you whether something is inside a 140° cone without calculating any angles.
- **Enemy aiming** solves a quadratic to find when a bullet fired now would meet you if you kept moving the same way. Each enemy then blends between aiming at you and aiming at that spot, and the blend shifts towards the second as waves go up.
- **Weapon spread** picks a random direction inside a small cone. The cone is sampled evenly so shots don't bunch up in the middle.
- **Enemies strafe** sideways using a cross product to find the direction perpendicular to you, and they turn smoothly at a fixed number of degrees per second.
- **Enemy spawn points** are evenly spaced around a circle, using `cos` and `sin`.
- **The red damage arrow** on the HUD converts the hit direction into the player's local space and uses `atan2` to get the angle to draw it at. It turns blue when your armor blocks the hit.

## Performance

I tried to avoid the usual things that make Unity games stutter:

- Bullet tracers come from a fixed pool that gets reused, so shooting doesn't create new objects.
- Enemy projectiles use a shared raycast buffer, so they don't allocate memory every frame. They also check the whole distance they'll travel each frame, which stops fast shots passing through thin walls.
- Enemies recalculate their path five times a second instead of every frame.
- Hit flashes use `MaterialPropertyBlock`, so enemies don't each get their own copy of a material.

## What's next

- Pool the enemy projectiles and enemies too, then measure the frame rate with large numbers of enemies before and after.
- Show a line while aiming that previews where your first bounce will go.
- Make bounces that only graze the wall lose less damage than head-on ones.
- Add tests for `GameMath`.
- Touch controls, to try it on a phone.

## Credits

I built this with Claude (Anthropic) as a coding assistant, then went through it, tuned it and extended it myself. Most of what I know about the math came from Freya Holmér's *Math for Game Devs* lectures and the Catlike Coding tutorials.
