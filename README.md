# Decantra

Decantra is a mobile-first bottle sorting puzzle built in Unity. Pour colors between bottles to group each bottle into a single color or empty.

## How to Play

- Drag a bottle onto another to pour.
- You can only pour onto the same color or an empty segment.
- Complete a level when every bottle is empty or a single color.

## Requirements

- Unity 6000.3.5f2
- Android SDK + NDK r27c + JDK 17 (auto-configured by the build script)
- A connected Android device for install/run (optional)

## Quick Start

- Build a debug APK:
  - `./local-build.sh`
- Build and install on a device:
  - `./local-build.sh --install`
- Skip tests:
  - `./local-build.sh --skip-tests`

## Developer docs

- Build/install/sharing instructions: [doc/developer.md](doc/developer.md)

## Project Structure

- Domain logic: Assets/Decantra/Domain
- App services: Assets/Decantra/App
- Presentation (Unity): Assets/Decantra/Presentation
- Tests: Assets/Decantra/Tests

## Notes

- Use the menu item “Decantra/Setup Scene” in the Unity Editor if you want to run directly in the editor.
- The build script will detect installed Android tooling and only install missing components.
