# FrameShift 2 — Surrealism Transposed

An augmented reality installation that transforms Mac Zimmermann's painting cycle *Die Tageszeiten* (1954) into an immersive 3D experience using [Tilt Five](https://www.tiltfive.com/) AR glasses.

## About

FrameShift 2 transposes surrealism into the modern world of augmented reality. Instead of simply viewing a painting on a wall, viewers use AR technology to look *behind* the canvas and even *out of* the painting back into the real world. The installation deliberately plays with the boundary between dream and reality by placing the viewer directly into the virtual atelier of the artist Mac Zimmermann.

Through a custom-built picture frame, viewers experience the day cycle as a living 3D model while simultaneously catching a glimpse "behind the scenes" of the surrealist world — transforming a static painting into an interactive experiential space.

The project was inspired by a visit to the special exhibition *STRANGE!* at the Sammlung Scharf-Gerstenberg (near Schloss Charlottenburg, Berlin) and emerged from a collaboration between students of Communication Design and Computer Science in Culture and Health at [HTW Berlin](https://www.htw-berlin.de/).

## The Artist: Mac Zimmermann

Mac Zimmermann (1912--1995) was a German painter, graphic artist, illustrator, and stage designer. His works are characterized by surrealism and fantastical realism, depicting dreamlike spaces and symbolic figures. He lived and worked primarily in Berlin and Munich and became an important figure in post-war German art. In 1950 he co-founded the Deutscher Künstlerbund and participated regularly in its exhibitions from 1951 to 1979.

## Concept

The installation is built around Zimmermann's painting cycle *Die Tageszeiten* — four paintings depicting the times of day (morning, noon, evening, night).

Three core ideas drive the experience:

1. **Unifying the cycle** — All four paintings are combined into a single, living 3D scene. The subjects from each painting are placed on rotating turntables, cycling through the times of day along a central portal axis.
2. **Looking through the canvas** — A physical picture frame holds the retro-reflective Tilt Five board on its back, with an art print of the original painting on the front. The AR experience plays behind the frame, creating the illusion of depth beyond the painting. The reflective board is not just technology — it metaphorically *becomes* the painting, breaking the "fourth wall."
3. **A glimpse behind the scenes** — The background reveals Mac Zimmermann's virtual atelier, complete with period-appropriate furniture, sketches, and the artist himself navigating the studio, allowing viewers to peek into the creative world behind the artwork.

## Technology

### Tilt Five AR System

The Tilt Five glasses work by projecting images from a built-in beamer onto a retro-reflective surface — similar to how cat-eye reflectors work. The system is designed for horizontal tabletop use, but FrameShift 2 *hacks* this convention: the reflective board is mounted vertically behind a physical picture frame, creating the perfect illusion of a deep wall painting with a portal effect. This unconventional vertical setup was developed in coordination with the Tilt Five support team.

Key capabilities leveraged:
- **Occlusion** — Objects can appear in front of and behind the board plane
- **Portal effect** — Depth extends beyond the physical surface, resembling a window into another world
- **Head tracking** — The perspective shifts dynamically as the viewer moves

### Unity

The experience is built in Unity 6 (6000.0.60f1) using the Universal Render Pipeline (URP) and the Tilt Five Unity Plugin (SDK v1.4.2). The scene architecture uses additive scene loading:

- **ParentScene** — Entry point that additively loads the other scenes
- **ForegroundScene** — The 3D painting interpretation (turntables, models, lighting)
- **BackgroundScene** — The virtual atelier environment

### 3D Asset Pipeline

Figures from the paintings were primarily generated using [MeshyAI](https://www.meshy.ai/) and refined in Blender. Since the source paintings are 2D with only a single perspective, significant manual correction was needed — fixing flat model structures, adding missing geometry (arms, necks), correcting AI generation artifacts, and creating effects like flowing veils.

## Features

### Foreground — The Living Painting

- Four turntables arranged concentrically: evening (outer), noon (middle), morning (inner), night (top)
- A configurable **time object** controls the rotation speed of the turntables and the scene lighting, simulating a full day cycle
- Time-driven placement ensures the correct time-of-day models face forward at the right moment (e.g., morning models face front at "sunrise")
- A **directional light** adjusts intensity and color based on the time of day, creating a puppet-theater-like atmosphere with a visible model sun tracing the sky
- A transparent veil darkens at night; a particle system adds atmosphere

### Background — The Atelier

- A reconstructed virtual studio of Mac Zimmermann, furnished with period-appropriate 1960s-style objects (easel, books, lamps, furniture, newspaper, sketches) and decorated with the artist's paintings on the walls
- An **interactive mirror** extends the viewer's perspective beyond the limits of the Tilt Five field of view:
  - A hidden camera behind the mirror plane looks back into the room
  - The camera tracks the viewer's head position and adjusts its perspective accordingly
  - The camera feed is projected onto the mirror surface, revealing parts of the scene only visible through the mirror
  - This highlights the strengths of the Tilt Five technology and its ability to work with other perspective-expanding techniques
- A rigged and animated Mac Zimmermann character, navigating the atelier via NavMesh with multiple animations (walking, sitting, looking around, standing up)

## Setup & Deployment

### Prerequisites

- **Unity 6** (6000.0.60f1) with Universal Render Pipeline
- **Tilt Five SDK** v1.4.2 and matching Tilt Five Driver version
- Tilt Five AR glasses and retro-reflective board

### Getting Started

1. Clone the repository
2. Open the project in Unity 6 (6000.0.60f1)
3. Ensure the Tilt Five SDK and Driver are installed with matching versions

### Configuring for Vertical Board Use

Since the Tilt Five system is designed for horizontal tabletop use, a configuration change is required for the vertical wall-mounted setup:

1. Navigate to the Tilt Five installation folder
2. Go to `SDK/Utils` and open the `gameboard_transform` utility
3. Under **Euler Angles**, set the **Pitch Angle** to **90 degrees**
4. Click **Apply**

> **Note:** A Tilt Five headset must be connected for the setting to take effect.

### Running the Application

1. Open `Assets/Scenes/ParentScene.unity` as the startup scene
2. Enter Play Mode or build the project
3. The ParentScene will automatically load the ForegroundScene and BackgroundScene additively

## Viewing Guide

For the best experience with the Tilt Five installation:

- **Optimal distance:** Stand about 1--2 meters in front of the frame.
- **Image cutoff at the top?** The Tilt Five projector is designed to project downward. If the image is cut off at the top, tilt your head slightly back or gently lift the glasses with your hand. This is simply the beamer reaching its projection boundary.
- **Tracking initialization:** After putting on the glasses, it may take a moment for the visual tracking and internal sensors (accelerometer, gyroscope) to synchronize. If the image zooms out erratically or shakes, hold your head still for a few seconds until tracking stabilizes.
- **Lost tracking?** If the tracking drops out (e.g., too steep an angle, too close, or too far from the board), stand directly in front at ~1--2 m distance, look down at your feet, and slowly raise your head. Tracking should re-engage immediately.

## Project Structure

```
.
├── Assets/
│   ├── Foreground/          # Painting interpretation: models, materials, time/rotation scripts
│   │   └── Scripts/         # Fg_time, Fg_daynight, Fg_rotate, Fg_turntablerotation, NightOpacityFadeSimple
│   ├── Background/          # Atelier environment: furniture, mirror, character, lighting
│   │   ├── Characters/      # Mac Zimmermann rigged model and animations
│   │   └── Mirror/          # Interactive mirror implementation (MirrorMovement.cs)
│   ├── Scenes/              # ParentScene, ForegroundScene, BackgroundScene
│   │   └── BackgroundScene/ # TourGuide.cs (character navigation) and NavMesh
│   ├── Scripts/             # SceneLoader.cs (additive scene loading)
│   ├── Script/              # RotateAnchor.cs (utility rotation)
│   ├── Tilt Five/           # Tilt Five SDK v1.4.2
│   └── Settings/            # URP and rendering configuration
├── Packages/                # Unity package manifest and dependencies
├── ProjectSettings/         # Unity project settings
├── CONTRIBUTING.md          # Contribution guidelines
├── LICENSE                  # CC BY-NC 4.0
└── README.md
```

## Technical Architecture

```
ParentScene (Entry Point)
    │
    └── SceneLoader.cs
          │
          ├──► ForegroundScene (Additive)
          │     ├── Time System
          │     │    ├── Fg_time — master clock (configurable day duration)
          │     │    └── Fg_daynight — sun orbit, color gradient, intensity curve
          │     ├── Turntables
          │     │    ├── Fg_turntablerotation — UTC-synced rotation
          │     │    └── Fg_rotate — time-based rotation per day
          │     └── Night Effects
          │          └── NightOpacityFadeSimple — alpha fade for night veil
          │
          └──► BackgroundScene (Additive)
                ├── Atelier (Room prefab with furniture & decorations)
                ├── Interactive Mirror
                │    └── MirrorMovement.cs — reflected camera tracking
                └── Mac Zimmermann Character
                     ├── NavMeshAgent — pathfinding
                     ├── Animator — 8 animation states
                     └── TourGuide.cs — waypoint tour orchestration
```

## Development

The project was managed using **Scrum**:
- Weekly full-team meetings on Tuesdays
- Dailies with each sub-team on Fridays and Sundays
- Organized via GitHub Projects and GitHub Issues, fully linking the Kanban board, issues, and pull requests

The team was split into two groups — **Foreground** (painting transposition) and **Background** (atelier) — with a Scrum Master coordinating integration and merging. Two protected base branches (`dev/foreground`, `dev/background`) prevented merge conflicts and enforced the four-eyes principle via pull request reviews. Conception and hardware setup were done jointly; detailed design and Unity implementation happened within each group.

See [CONTRIBUTING.md](./CONTRIBUTING.md) for contribution guidelines.

## Knowledge Base

Additional resources, research, and documentation are collected in the [Wiki](https://github.com/Strehk/uni-frame-shift-2/wiki).

## Academic Context

FrameShift 2 is a student project at [HTW Berlin — University of Applied Sciences](https://www.htw-berlin.de/), developed in cooperation between the degree programs Communication Design and Computer Science in Culture and Health.

Supervised by:
- [Prof. Dr.-Ing. Johann Habakuk Israel](https://www.htw-berlin.de/hochschule/personen/person/?eid=4743)
- [Prof. Andreas Ingerl](https://www.htw-berlin.de/hochschule/personen/person/?eid=4388)

### Team

Jaro A., Alexandar A., Lisa A., Oliver F., Lorenz G., Marlon K., Emily K., Thien Vu David N., Alicia N., Gordian R., Tade S.

## License

![CC BY-NC 4.0](https://licensebuttons.net/l/by-nc/4.0/88x31.png)

This project is licensed under the [Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)](./LICENSE) License.
