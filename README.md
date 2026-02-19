# FrameShift 2 — Surrealism Transposed

An augmented reality installation that transforms Mac Zimmermann's painting cycle *Die Tageszeiten* (1954) into an immersive 3D experience using the [Tilt Five](https://www.tiltfive.com/) AR glasses.

## About

FrameShift 2 transposes surrealism into the modern world of augmented reality. By breaking the "fourth wall" of a painting, the installation lets viewers look *through* the canvas and *behind* the frame — straight into the virtual atelier of the artist Mac Zimmermann.

Surrealism plays with the boundary between dream and reality. Augmented reality is, technically speaking, exactly that: a blend of real environment and digital illusion. This makes AR a natural medium for bringing surrealist art to life.

The project was inspired by a visit to the special exhibition *STRANGE!* at the Sammlung Scharf-Gerstenberg (near Schloss Charlottenburg, Berlin) and emerged from a collaboration between students of Communication Design and Computer Science in Culture and Health at [HTW Berlin](https://www.htw-berlin.de/).

## Concept

The installation is built around Zimmermann's painting cycle *Die Tageszeiten* — four paintings depicting the times of day (morning, noon, evening, night).

Three core ideas drive the experience:

1. **Unifying the cycle** — All four paintings are combined into a single, living 3D scene. The subjects from each painting are placed on rotating turntables, cycling through the times of day.
2. **Looking through the canvas** — A physical picture frame holds the retro-reflective Tilt5 board on its back, with an art print of the original painting on the front. The AR experience plays behind the frame, creating the illusion of depth beyond the painting.
3. **A glimpse behind the scenes** — The background reveals Mac Zimmermann's virtual atelier, complete with furniture, sketches, and the artist himself, allowing viewers to peek into the creative world behind the artwork.

## Technology

### Tilt Five AR System

The Tilt5 glasses work by projecting images from a built-in beamer onto a retro-reflective surface — similar to how cat-eye reflectors work. The system is designed for tabletop use, but FrameShift 2 *hacks* this convention: the reflective board is mounted vertically behind a physical picture frame, creating the perfect illusion of a deep wall painting with a portal effect. This unconventional setup was developed in coordination with the Tilt5 support team.

Key capabilities leveraged:
- **Occlusion** — Objects can appear in front of and behind the board plane
- **Portal effect** — Depth extends beyond the physical surface
- **Head tracking** — The perspective shifts as the viewer moves

### Unity

The experience is built in Unity 6 (6000.0.60f1) using the Universal Render Pipeline (URP) and the Tilt5 Unity Plugin. The scene architecture uses additive scene loading:

- **ParentScene** — Entry point that additively loads the other scenes
- **ForegroundScene** — The 3D painting interpretation (turntables, models, lighting)
- **BackgroundScene** — The virtual atelier environment

### 3D Asset Pipeline

Figures from the paintings were primarily generated using [MeshyAI](https://www.meshy.ai/) and refined in Blender. Since the source paintings are 2D with only a single perspective, significant manual correction was needed — fixing flat model structures, adding missing geometry (arms, necks), and creating effects like flowing veils.

## Features

### Foreground — The Living Painting

- Four turntables arranged concentrically: evening (outer), noon (middle), morning (inner), night (top)
- A configurable **time object** controls the rotation speed of the turntables and the scene lighting, simulating a full day cycle
- Time-driven placement ensures the correct time-of-day models face forward at the right moment (e.g., morning models face front at "sunrise")
- A **directional light** adjusts intensity and color based on the time of day
- Transparent veils darken at night; a particle system adds atmosphere

### Background — The Atelier

- A reconstructed virtual studio of Mac Zimmermann, furnished with period-appropriate objects (easel, books, lamps, furniture, newspaper, sketches)
- An **interactive mirror** extends the viewer's perspective beyond the limits of the Tilt5 field of view:
  - A hidden camera behind the mirror plane looks back into the room
  - The camera tracks the viewer's head position and adjusts its perspective accordingly
  - The camera feed is projected onto the mirror surface, revealing parts of the scene only visible through the mirror
- A rigged and animated Mac Zimmermann character, navigating the atelier via NavMesh

## Viewing Guide

For the best experience with the Tilt5 installation:

- **Optimal distance:** Stand about 1–2 meters in front of the frame.
- **Image cutoff at the top?** The Tilt5 projector is designed to project downward. If the image is cut off at the top, tilt your head slightly back or gently lift the glasses with your hand.
- **Tracking initialization:** After putting on the glasses, it may take a moment for the visual tracking and internal sensors (accelerometer, gyroscope) to synchronize. If the image zooms out erratically or shakes, hold your head still for a few seconds.
- **Lost tracking?** If the tracking drops out (e.g., too steep an angle, too close, or too far from the board), stand directly in front at ~1–2 m distance, look down at your feet, and slowly raise your head. Tracking should re-engage immediately.

## Project Structure

```
.
├── Assets/
│   ├── Foreground/          # Painting interpretation: models, materials, time/rotation scripts
│   ├── Background/          # Atelier environment: furniture, mirror, character, lighting
│   ├── Scenes/              # ParentScene, ForegroundScene, BackgroundScene
│   ├── Scripts/             # Scene loader and shared utilities
│   ├── Tilt Five/           # Tilt5 SDK v1.4.2
│   └── Settings/            # Project configuration assets
├── Packages/                # Unity package manifest and dependencies
├── ProjectSettings/         # Unity project settings
├── CONTRIBUTING.md          # Contribution guidelines
├── LICENSE                  # CC BY-NC 4.0
└── README.md
```

## Development

The project was managed using **Scrum**:
- Weekly full-team meetings on Tuesdays
- Dailies with each sub-team on Fridays and Sundays
- Organized via [GitHub Projects](https://github.com/Strehk/uni-frame-shift-2) and GitHub Issues, fully linking the Kanban board, issues, and pull requests

The team was split into two groups — **Foreground** (painting transposition) and **Background** (atelier) — with a Scrum Master coordinating integration. Two protected base branches prevented merge conflicts and enforced the four-eyes principle via pull request reviews.

See [CONTRIBUTING.md](./CONTRIBUTING.md) for contribution guidelines.

## Knowledge Base

Additional resources, research, and documentation are collected in the [Wiki](https://github.com/Strehk/uni-frame-shift-2/wiki).

## Academic Context

FrameShift 2 is a student project at [HTW Berlin — University of Applied Sciences](https://www.htw-berlin.de/), developed in cooperation between the degree programs Communication Design and Computer Science in Culture and Health.

Supervised by:
- [Prof. Dr.-Ing. Johann Habakuk Israel](https://www.htw-berlin.de/hochschule/personen/person/?eid=4743)
- [Prof. Andreas Ingerl](https://www.htw-berlin.de/hochschule/personen/person/?eid=4388)

## License

![CC BY-NC 4.0](https://licensebuttons.net/l/by-nc/4.0/88x31.png)

This project is licensed under the [Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)](./LICENSE) License.
