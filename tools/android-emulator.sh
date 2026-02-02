#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

AVD_NAME="DecantraPhone"
API_LEVEL="35"
BUILD_TOOLS="35.0.0"
DEVICE_PROFILE="pixel_6"
WITH_PREREQS=1
WITH_SDK=1
WITH_AVD=1
WITH_EMULATOR=1
EMULATOR_GPU="swiftshader_indirect"
EMULATOR_ARGS="-netdelay none -netspeed full -no-snapshot -no-metrics"
EMULATOR_WAIT_TIMEOUT="180"
DEVICE_SIZE="1080x2400"
DEVICE_DENSITY="420"
LOCALE="en-US"

resolve_sdk_dir() {
  if [[ -n "${ANDROID_SDK_ROOT:-}" ]]; then
    echo "$ANDROID_SDK_ROOT"
    return
  fi
  if [[ -n "${ANDROID_HOME:-}" ]]; then
    echo "$ANDROID_HOME"
    return
  fi
  if [[ -d "$HOME/Android/Sdk" ]]; then
    echo "$HOME/Android/Sdk"
    return
  fi
  echo "$HOME/Android/Sdk"
}

configure_android_sdk_env() {
  local sdk_dir="$1"
  export ANDROID_SDK_ROOT="$sdk_dir"
  export ANDROID_HOME="$sdk_dir"
  export PATH="$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$PATH"
}

SDK_DIR="$(resolve_sdk_dir)"

usage() {
  cat <<EOF
Usage: tools/android-emulator.sh [options]

Options:
  --sdk-dir PATH         Android SDK root (default: $SDK_DIR)
  --avd-name NAME        AVD name (default: $AVD_NAME)
  --api LEVEL            Android API level (default: $API_LEVEL)
  --build-tools VERSION  Build tools version (default: $BUILD_TOOLS)
  --device PROFILE       AVD device profile (default: $DEVICE_PROFILE)
  --no-prereqs           Skip apt prerequisite installation
  --no-sdk               Skip SDK download/installation
  --no-avd               Skip AVD creation
  --no-emulator          Skip emulator launch
  --emulator-gpu MODE    Emulator GPU mode (default: $EMULATOR_GPU)
  --emulator-args "ARGS" Extra emulator args
  --wait-timeout SEC     Boot wait timeout (default: $EMULATOR_WAIT_TIMEOUT)
  -h, --help             Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sdk-dir) SDK_DIR="$2"; shift 2;;
    --avd-name) AVD_NAME="$2"; shift 2;;
    --api) API_LEVEL="$2"; shift 2;;
    --build-tools) BUILD_TOOLS="$2"; shift 2;;
    --device) DEVICE_PROFILE="$2"; shift 2;;
    --no-prereqs) WITH_PREREQS=0; shift;;
    --no-sdk) WITH_SDK=0; shift;;
    --no-avd) WITH_AVD=0; shift;;
    --no-emulator) WITH_EMULATOR=0; shift;;
    --emulator-gpu) EMULATOR_GPU="$2"; shift 2;;
    --emulator-args) EMULATOR_ARGS="$2"; shift 2;;
    --wait-timeout) EMULATOR_WAIT_TIMEOUT="$2"; shift 2;;
    -h|--help) usage; exit 0;;
    *) echo "Unknown option: $1" >&2; usage; exit 1;;
  esac
done

configure_android_sdk_env "$SDK_DIR"

install_prereqs() {
  if ! command -v sudo >/dev/null 2>&1; then
    echo "sudo not available; skipping prerequisite installation." >&2
    return 0
  fi
  sudo apt update
  sudo apt install -y openjdk-17-jdk unzip wget curl git \
    libgl1-mesa-dev libpulse0 libx11-6 libxcb1 libxcomposite1 libxdamage1 \
    libxext6 libxfixes3 libxrender1 libxi6 libxkbcommon0 libxkbcommon-x11-0 \
    libnss3 libnspr4 libdrm2 libgbm1 libasound2t64 libxtst6 libx11-xcb1 \
    qemu-kvm libvirt-daemon-system libvirt-clients bridge-utils
  sudo usermod -aG kvm,libvirt "$USER" || true
}

check_prereqs() {
  local missing=()

  for cmd in java unzip wget curl git; do
    command -v "$cmd" >/dev/null 2>&1 || missing+=("$cmd")
  done

  if [[ ${#missing[@]} -gt 0 ]]; then
    echo "Missing prerequisites: ${missing[*]}" >&2
    return 1
  fi

  if [[ ! -x "/dev/kvm" ]]; then
    echo "Warning: /dev/kvm not available. Emulator may run slowly." >&2
  fi

  return 0
}

install_sdk_tools() {
  mkdir -p "$ANDROID_HOME/cmdline-tools"
  cd "$ANDROID_HOME"
  if [[ ! -f cmdline-tools.zip ]]; then
    curl -o cmdline-tools.zip https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip
  fi
  if [[ ! -d "$ANDROID_HOME/cmdline-tools/latest" ]]; then
    unzip -q cmdline-tools.zip -d /tmp/android-cmdline
    mv /tmp/android-cmdline/cmdline-tools "$ANDROID_HOME/cmdline-tools/latest"
  fi

  yes | sdkmanager --licenses || true
  sdkmanager \
    "platform-tools" \
    "platforms;android-${API_LEVEL}" \
    "build-tools;${BUILD_TOOLS}" \
    "emulator" \
    "system-images;android-${API_LEVEL};google_apis;x86_64"
}

ensure_avd() {
  if ! avdmanager list avd | grep -q "Name: $AVD_NAME"; then
    printf "no\n" | avdmanager create avd -n "$AVD_NAME" -k "system-images;android-${API_LEVEL};google_apis;x86_64" -d "$DEVICE_PROFILE"
  fi
}

start_emulator() {
  adb start-server
  local extra_args=""
  if [[ -z "${DISPLAY:-}" ]]; then
    extra_args="-no-window -no-audio -no-boot-anim"
  fi
  if ! adb devices | grep -q "emulator-"; then
    nohup setsid emulator -avd "$AVD_NAME" -gpu "$EMULATOR_GPU" $EMULATOR_ARGS $extra_args </dev/null >/tmp/decantra-emu.log 2>&1 &
    disown || true
  fi
}

get_emulator_id() {
  adb devices | awk 'NR>1 && $2=="device" && $1 ~ /^emulator-/ {print $1}' | head -n 1
}

wait_for_boot() {
  local emulator_id=""
  local attempts=0
  local boot_completed=""

  timeout 30 adb wait-for-device || {
    echo "adb wait-for-device timed out after 30s" >&2
    exit 1
  }

  while [[ $attempts -lt ${EMULATOR_WAIT_TIMEOUT} ]]; do
    emulator_id="$(get_emulator_id)"
    if [[ -n "$emulator_id" ]]; then
      boot_completed="$(timeout 10 adb -s "$emulator_id" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')"
      if [[ "$boot_completed" == "1" ]]; then
        return 0
      fi
    fi
    sleep 1
    attempts=$((attempts + 1))
  done

  echo "Emulator did not finish booting within ${EMULATOR_WAIT_TIMEOUT}s." >&2
  exit 1
}

configure_emulator() {
  local emulator_id
  emulator_id="$(get_emulator_id)"
  if [[ -z "$emulator_id" ]]; then
    echo "No emulator found after boot." >&2
    exit 1
  fi

  adb -s "$emulator_id" shell settings put global window_animation_scale 0
  adb -s "$emulator_id" shell settings put global transition_animation_scale 0
  adb -s "$emulator_id" shell settings put global animator_duration_scale 0
  adb -s "$emulator_id" shell wm size "$DEVICE_SIZE" || true
  adb -s "$emulator_id" shell wm density "$DEVICE_DENSITY" || true
  adb -s "$emulator_id" shell settings put system system_locales "$LOCALE" || true
  adb -s "$emulator_id" shell input keyevent 82 || true
}

if [[ $WITH_PREREQS -eq 1 ]]; then
  if ! check_prereqs; then
    install_prereqs
  fi
fi

if [[ $WITH_SDK -eq 1 ]]; then
  install_sdk_tools
fi

if [[ $WITH_AVD -eq 1 ]]; then
  ensure_avd
fi

if [[ $WITH_EMULATOR -eq 1 ]]; then
  start_emulator
  wait_for_boot
  configure_emulator
fi

echo "Android emulator ready."