# Files

## Spec Kit

- [`SPEC.md`](SPEC.md): Feature specification for Decantra.
- [`PLANS.md`](PLANS.md): Implementation plan and phases.
- [`DECISIONS.md`](DECISIONS.md): Architecture and technical decisions.
- [`STATUS.md`](STATUS.md): Current project status and progress.
- [`MEMORY.md`](MEMORY.md): Durable project memory.
- [`FILES.md`](FILES.md): File index and purpose.

## Tools

- [`tools/test.sh`](tools/test.sh): Unity batchmode EditMode test runner.
- [`tools/build_android.sh`](tools/build_android.sh): Builds debug Android APK via Unity batchmode.
- [`tools/install_android.sh`](tools/install_android.sh): Installs APK on first connected ADB device.
- [`tools/dev_install_run.sh`](tools/dev_install_run.sh): Builds, installs, and launches the app on device.
- [`tools/coverage_gate.sh`](tools/coverage_gate.sh): Fails if coverage is below the required threshold.
- [`local-build.sh`](local-build.sh): Runs tests, builds APK, and optionally installs/launches on device.

## App (Editor)

- [`Assets/Decantra/App/Editor/AndroidBuild.cs`](Assets/Decantra/App/Editor/AndroidBuild.cs): Unity batchmode Android build entry point.
