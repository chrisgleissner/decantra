# Feature Specification: Decantra (Liquid Sort Puzzle)

**Feature Branch**: `000-decantra-core`  
**Created**: 2026-01-29  
**Status**: Draft  
**Input**: User description: "MASTER IMPLEMENTATION PROMPT"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Play a solvable level end-to-end (Priority: P1)

As a player, I can load a level, make valid pour moves with animations, and complete the level to win.

**Why this priority**: Core gameplay loop defines MVP value.

**Independent Test**: Can be fully tested by simulating a level state, making moves, and validating win state.

**Acceptance Scenarios**:

1. **Given** a generated level, **When** I perform a valid sequence of moves, **Then** the game declares a win and locks input during animations.
2. **Given** an invalid move, **When** I tap source and target, **Then** the move is rejected with feedback and no state change.

---

### User Story 2 - See optimal/allowed moves and score (Priority: P1)

As a player, I can see moves used, allowed moves, optimal moves, and score updates during play.

**Why this priority**: Required by acceptance and scoring design.

**Independent Test**: Can be fully tested with solver integration and HUD bindings.

**Acceptance Scenarios**:

1. **Given** a level start, **When** HUD loads, **Then** it shows Level, Moves Used/Allowed, and Optimal moves.
2. **Given** moves are executed, **When** the move count changes, **Then** HUD updates and score recalculates.

---

### User Story 3 - One-command Android build/install/run (Priority: P1)

As a developer, I can run a single command to build a debug APK, install on the first device, and launch the app.

**Why this priority**: Explicit non-negotiable outcome.

**Independent Test**: Can be fully tested by executing the shell script with a connected ADB device.

**Acceptance Scenarios**:

1. **Given** a connected device, **When** I run the script, **Then** the APK installs and the app launches.
2. **Given** no connected device, **When** I run the script, **Then** it fails with a clear message.

---

### Edge Cases

- What happens when no valid moves remain but the level is unsolved?
- How does the system handle solver timeouts on complex seeds?
- What happens when moves used exceeds allowed moves mid-animation?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST generate 3x3 (9) bottles with capacity 4 and 6 colors.
- **FR-002**: System MUST enforce move rules and pour maximum contiguous units.
- **FR-003**: System MUST lock input during animations.
- **FR-004**: System MUST include optimal solver and display optimal moves.
- **FR-005**: System MUST generate solvable levels by reverse construction with deterministic seeds.
- **FR-006**: System MUST persist current level seed, score, and highest unlocked level.
- **FR-007**: System MUST provide Android build/install/run scripts.
- **FR-008**: System MUST enforce >= 80% domain coverage gate in CI/build.

### Key Entities

- **Bottle**: Ordered stack of color units with capacity constraints.
- **LevelState**: Full game state including bottles, moves used, and seed.
- **Move**: Source/target pair and poured unit count.
- **Solver**: Optimal move calculator with deterministic output.
- **Score**: Derived from difficulty and proximity to optimal.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Domain logic tests maintain >= 80% code coverage.
- **SC-002**: A level can be solved end-to-end on Android with animations.
- **SC-003**: One-command script builds, installs, and launches the APK on a connected device.
- **SC-004**: Levels are reproducible by seed and validated by solver.
