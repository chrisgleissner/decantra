# Decantra

<img src="./doc/img/logo.png" alt="Decantra Logo" width="100"/>

Decantra is a mobile-first bottle-sorting puzzle game built in Unity.

## How to Play

- Drag one bottle onto another to pour liquid.
- You can only pour onto the same colour or into an empty segment.
- A level is complete when all bottles are either empty or contain exactly one colour.
- As levels progress, the number of bottles and colours increases.
- Bottles may have different capacity.
- You cannot lift bottles with a dark bottom.

## Requirements

- Unity 6000.3.5f2
- Android SDK + NDK r27c + JDK 17 (automatically handled by the build script)
- A connected Android device for install and run (optional)

## Quick Start

- Build a debug APK:
  - `./build`
- Build and install directly on a connected device:
  - `./build --install`
- Build without running tests:
  - `./build --skip-tests`
- Capture Play Store screenshots:
  - `./build --screenshots`

## Developer Documentation

- Build, install, and distribution details: [doc/developer.md](doc/developer.md)

## Project Structure

- Domain logic: `Assets/Decantra/Domain`
- Application services: `Assets/Decantra/App`
- Presentation layer (Unity): `Assets/Decantra/Presentation`
- Tests: `Assets/Decantra/Tests`

## Notes

- To run the game directly in the Unity Editor, use the menu item `Decantra/Setup Scene`.
- The build script automatically detects existing Android tooling and installs only what is missing.
