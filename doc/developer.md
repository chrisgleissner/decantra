# Developer Guide

## Build the APK (detailed)

### Prerequisites
- Unity 6000.3.5f2 installed.
- Android SDK, Android NDK r27c, and JDK 17 installed.
- Environment variables (required by the build script):
  - `UNITY_PATH` → absolute path to the Unity editor binary (e.g. `/usr/local/bin/unity`).
  - `JAVA_HOME` → JDK 17 home.
  - `ANDROID_SDK_ROOT` and/or `ANDROID_HOME` → Android SDK root.
  - `ANDROID_NDK_ROOT` → NDK root (r27c).

### Build command (debug APK)
- From the project root:
  - `UNITY_PATH=/usr/local/bin/unity \
     UNITY_BUILD_TIMEOUT=10m \
     JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64 \
     ANDROID_SDK_ROOT=~/Android/Sdk \
     ANDROID_HOME=~/Android/Sdk \
     ANDROID_NDK_ROOT=~/Android/Sdk/ndk/27.2.12479018 \
     ./tools/build_android.sh`

The APK will be produced at:
- `Builds/Android/Decantra.apk`

## Install the APK on an Android device

### ADB install (recommended)
1. Enable Developer Options and USB Debugging on the device.
2. Connect the device via USB and verify it appears in:
   - `adb devices`
3. Install the APK:
   - `adb install -r --no-streaming Builds/Android/Decantra.apk`

### Manual install (no ADB)
1. Copy `Builds/Android/Decantra.apk` to the device.
2. Enable "Install unknown apps" for your file manager/browser.
3. Tap the APK to install.
