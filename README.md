# Decantra

[![Build](https://github.com/chrisgleissner/decantra/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/chrisgleissner/decantra/actions/workflows/build.yml)
[![iOS Build + Maestro](https://github.com/chrisgleissner/decantra/actions/workflows/ios.yml/badge.svg?branch=main)](https://github.com/chrisgleissner/decantra/actions/workflows/ios.yml)
[![WebGL Build + Deploy](https://github.com/chrisgleissner/decantra/actions/workflows/web.yml/badge.svg?branch=main)](https://github.com/chrisgleissner/decantra/actions/workflows/web.yml)
[![codecov](https://codecov.io/gh/chrisgleissner/decantra/graph/badge.svg)](https://codecov.io/gh/chrisgleissner/decantra)
[![License: GPL v2](https://img.shields.io/badge/License-GPL%20v2-blue.svg)](https://www.gnu.org/licenses/old-licenses/gpl-2.0.en.html)
[![Platform](https://img.shields.io/badge/platforms-Android%20%7C%20iOS%20%7C%20Web-blue)](https://github.com/chrisgleissner/decantra/releases)

A bottle-sorting puzzle game with procedurally generated levels that never run out. Available on Android, iOS, and Web.

<img src="./doc/img/logo.png" alt="Decantra Logo" width="200"/>

## How to Play

Sort the colored liquids by pouring from one bottle to another until each bottle holds a single color.

- **Pour** by dragging one bottle onto another.
- You can only pour onto **the same color** or into an **empty bottle**.
- **Black bottles** (sinks) accept liquid but can't be picked up - plan around them.
- A level is solved when every non-empty bottle contains exactly one color.

Early levels ease you in. Later levels introduce more colors, varying bottle sizes, and multiple sinks that demand careful sequencing. Every level is procedurally generated, so there's always a fresh puzzle waiting.

<table>
  <tr>
    <td><img src="doc/play-store-assets/screenshots/phone/screenshot-03-level-01.png" alt="Level 1" width="240"/></td>
    <td><img src="doc/play-store-assets/screenshots/phone/screenshot-08-level-10.png" alt="Level 10" width="240"/></td>
    <td><img src="doc/play-store-assets/screenshots/phone/screenshot-04-level-12.png" alt="Level 12" width="240"/></td>
  </tr>
  <tr>
    <td><img src="doc/play-store-assets/screenshots/phone/screenshot-09-level-20.png" alt="Level 20" width="240"/></td>
    <td><img src="doc/play-store-assets/screenshots/phone/screenshot-05-level-24.png" alt="Level 24" width="240"/></td>
    <td><img src="doc/play-store-assets/screenshots/phone/screenshot-07-level-36.png" alt="Level 36" width="240"/></td>
  </tr>
</table>

## Play Now

The fastest way to try Decantra is in your browser - no install required:

**[Play Decantra in your browser](https://chrisgleissner.github.io/decantra/webgl/)**

### Android

1. Grab the latest APK from [Releases](https://github.com/chrisgleissner/decantra/releases) (`decantra-<version>-android.apk`).
2. Open it on your device and allow installs from unknown sources if prompted.
3. Tap **Install** and you're in.

### iOS

1. Download the latest IPA from [Releases](https://github.com/chrisgleissner/decantra/releases) (`decantra-<version>-ios.ipa`).
2. Install via [SideStore](https://docs.sidestore.io/) (**My Apps → + → select the IPA**).
3. Launch and play.

## Features

- **Endless levels** - procedural generation means no two puzzles are alike and you never hit a dead end.
- **Increasing difficulty** - more colors, different bottle capacities, and sink bottles that constrain your options.
- **Three platforms** - Android, iOS, and Web from a single Unity codebase.

## License

Licensed under GPL v2. See [LICENSE](LICENSE) for details.
