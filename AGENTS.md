# AGENTS

Concise guide for agents working on Decantra. Use these sources of truth: [PLANS.md](PLANS.md)

## Facts (verify before changes)

- Unity Editor version: [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)
- Android app id + product name: [ProjectSettings/ProjectSettings.asset](ProjectSettings/ProjectSettings.asset)
- Packages: [Packages/manifest.json](Packages/manifest.json)

## Architecture boundaries (do not break)

- Domain: [Assets/Decantra/Domain](Assets/Decantra/Domain) is pure C# (no UnityEngine).
- Presentation: [Assets/Decantra/Presentation](Assets/Decantra/Presentation) is Unity-only.
- App services: [Assets/Decantra/App](Assets/Decantra/App).
- Tests: [Assets/Decantra/Tests](Assets/Decantra/Tests) with EditMode + PlayMode.

## Build/run (editor)

- Open in the required Unity version.
- Use menu item “Decantra/Setup Scene” to wire the scene.
- Press Play to run deterministic level load.

## Testing + gatekeepers

- Run EditMode + PlayMode tests via Unity Test Runner.
- Coverage target: ≥80% for domain logic (Code Coverage package).
- PR gate: tests green + coverage target met + maintainer review.

## Coding standards (observed)

- `sealed` classes, guard clauses, PascalCase publics, `_privateFields`.
- Deterministic logic in Domain (seeded RNG).
- Avoid hot-path allocations; clone state deliberately.

## External docs (quick links)

- Unity Test Framework: <https://docs.unity3d.com/Packages/com.unity.test-framework@latest>
- Code Coverage: <https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@latest>
- URP 2D: <https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest>

## Tools

- [`tools/test.sh`](tools/test.sh): Unity batchmode EditMode test runner.
- [`tools/build_android.sh`](tools/build_android.sh): Builds debug Android APK via Unity batchmode.
- [`tools/install_android.sh`](tools/install_android.sh): Installs APK on first connected ADB device.
- [`tools/dev_install_run.sh`](tools/dev_install_run.sh): Builds, installs, and launches the app on device.
- [`tools/coverage_gate.sh`](tools/coverage_gate.sh): Fails if coverage is below the required threshold.
- [`build.sh`](build.sh): Runs tests, builds APK, and optionally installs/launches on device.

## App (Editor)

- [`Assets/Decantra/App/Editor/AndroidBuild.cs`](Assets/Decantra/App/Editor/AndroidBuild.cs): Unity batchmode Android build entry point.