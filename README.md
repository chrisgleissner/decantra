# Decantra

[![Build](https://github.com/chrisgleissner/decantra/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/chrisgleissner/decantra/actions/workflows/build.yml)
[![iOS Build + Maestro](https://github.com/chrisgleissner/decantra/actions/workflows/ios.yml/badge.svg?branch=main)](https://github.com/chrisgleissner/decantra/actions/workflows/ios.yml)
[![WebGL Build + Deploy](https://github.com/chrisgleissner/decantra/actions/workflows/web.yml/badge.svg?branch=main)](https://github.com/chrisgleissner/decantra/actions/workflows/web.yml)
[![codecov](https://codecov.io/gh/chrisgleissner/decantra/graph/badge.svg)](https://codecov.io/gh/chrisgleissner/decantra)
[![License: GPL v2](https://img.shields.io/badge/License-GPL%20v2-blue.svg)](https://www.gnu.org/licenses/old-licenses/gpl-2.0.en.html)
[![Platform](https://img.shields.io/badge/platforms-Android%20%7C%20iOS%20%7C%20Web-blue)](https://github.com/chrisgleissner/decantra/releases)

Procedural bottle-sorting puzzle game built with Unity for Android, iOS and Web

<img src="./doc/img/logo.png" alt="Decantra Logo" width="200"/>

## ✨ Features

- Deterministic procedural level generation.
- Domain-first architecture with Unity presentation layer separation.
- Capacity-variant bottles and sink constraints for puzzle depth.
- CI pipelines for Android, iOS, and WebGL builds.

## 🧩 How to Play

- Drag one bottle onto another to pour liquid.
- You can only pour into an empty bottle or onto the same color.
- Black bottles can receive liquid but cannot be lifted.
- A level is complete when every non-empty bottle contains one color.

## 🚀 Quick Start

### Play in your Browser

To play in your browser, simply open [https://chrisgleissner.github.io/decantra/webgl/](https://chrisgleissner.github.io/decantra/webgl/)

### Install on Android

1. Download the latest APK from [Releases](https://github.com/chrisgleissner/decantra/releases) (`decantra-<version>-android.apk`).
2. Open the APK on your Android device.
3. Allow installs from unknown sources if prompted.
4. Tap **Install** and launch Decantra.

### Install on iOS

1. Download the latest IPA from [Releases](https://github.com/chrisgleissner/decantra/releases) (`decantra-<version>-ios.ipa`).
2. Set up [SideStore](https://docs.sidestore.io/).
3. In **SideStore → My Apps**, tap **+** and select the IPA.
4. Launch Decantra.

Notes:

- iOS support is experimental.
- iOS artifacts are generated in CI as unsigned IPA packages.
- SideStore refreshes apps every 7 days to renew the signature.

## ⚖️ License

This project is licensed under GPL v2. See [LICENSE](LICENSE) for details.
