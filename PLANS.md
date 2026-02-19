# PLANS

Last updated: 2026-02-19
Owner: GitHub Copilot (GPT-5.3-Codex)
Scope: Autosolve move orchestration defect (Android-first)

## Reproduction Steps (Real Device)

1. Build/install Android app and launch on connected device.
2. Open a level where auto-solve has at least one legal move.
3. Open trade-in dialog and trigger auto-solve.
4. Observe first move attempt.
5. Capture video and logs.

## Observed Behaviour

- Bottle animates toward target, then snaps back.
- Target highlight may not visibly activate.
- Pour may not start/complete even though source and target were selected by solver.
- Level progress can stall due move not being committed.

## Hypotheses (Ranked)

1. **Highest**: Autosolve drag animation is decoupled from gameplay release/commit path, creating race/failure windows.
2. **High**: Target activation/highlight is not part of authoritative move sequence, so release can occur without expected UI state.
3. **Medium**: Autosolve completion waits on `_inputLocked` polling + timeout instead of explicit pour completion event.
4. **Lower**: Move rejection paths are under-instrumented, making diagnosis difficult on Android.

## Instrumentation Plan

Add structured diagnostics behind a dedicated autosolve debug flag:

- `AutosolveStepStarted(stepId)`
- `SourceSelected(bottleId)`
- `TargetChosen(bottleId)`
- `TargetActivated(bottleId)`
- `ReleaseInvoked()`
- `PourStarted(amount)`
- `PourCompleted()`
- `MoveRejected(reason)`
- `StateTransition(oldState,newState)`

Keep hooks (events + structured logger), default diagnostics off.

## Refactor Plan

1. Introduce one authoritative coroutine in `GameController`, `PerformMove(...)`, which validates + orchestrates animation and gameplay commit.
2. Route autosolve path through `PerformMove(...)` only.

3. Ensure order is deterministic:

- validate legality
- source select/lift/drag animation
- target activate/highlight
- release invoke
- start real gameplay pour via existing logic
- wait for explicit pour completion event
- clear temporary visuals

1. Remove autosolve dependence on `_inputLocked` timeout polling for move completion.

## Test Plan

### Unit/EditMode

- Keep existing domain move-rule tests as authority checks.

### PlayMode

Add regression tests in `GameControllerPlayModeTests`:

1. Load deterministic level.
2. Execute exactly one autosolve-orchestrated move.
3. Assert source/target contents changed as expected.
4. Assert `PourStarted` and `PourCompleted` lifecycle events fired.
5. Assert no snap-back-without-state-change (state key changes after move).
6. Assert target activation hook fired before completion.

Use explicit event/state waits (bounded frame polling), avoid unbounded `WaitForSeconds` sleeps.

## Risk Register

- **KR-1**: Regress manual drag/tap move path while refactoring shared move code.
  - Mitigation: keep `TryStartMove` API stable; reuse internal method.
- **KR-2**: Introduce deadlock while waiting for completion event.
  - Mitigation: emit completion from one source (`AnimateMove` end), and guard null-state exits.
- **KR-3**: Android-only timing differences reveal hidden assumptions.
  - Mitigation: move to event-driven orchestration, remove timeout-driven success criteria.
- **KR-4**: Excessive debug noise in production logs.
  - Mitigation: diagnostics behind disabled-by-default flag.

## Definition of Done Checklist

- [x] Autosolve uses authoritative gameplay move execution path (no direct state mutation).
- [x] Target highlight/activation is visible before release.
- [x] Source/target bottle state changes correctly after each autosolve move.
- [x] Level progresses toward completion through real move application.
- [x] Snap-back-without-pour defect eliminated.
- [x] PlayMode autosolve regression tests added and passing.
- [x] EditMode tests passing.
- [ ] Android validation executed with recording + structured logs.

## Execution Journal (UTC)

- 2026-02-19T00:00:00Z — Started autosolve defect task; scoped requirements and constraints.
- 2026-02-19T00:05:00Z — Audited `GameController` autosolve path; identified drag/return and move execution decoupling.
- 2026-02-19T00:08:00Z — Verified connected Android device via `adb devices` (`2113b87f`).
- 2026-02-19T00:10:00Z — Updated plan with instrumentation + authoritative `PerformMove` refactor approach.
- 2026-02-19T12:05:00Z — Implemented `PerformMove(...)` orchestration and routed auto-solve execution through it.
- 2026-02-19T12:06:00Z — Added pour lifecycle + autosolve activation/release diagnostics hooks behind debug flag.
- 2026-02-19T12:09:00Z — Added PlayMode regression `AutoSolveSingleMove_TriggersPourLifecycleAndMutatesState`.
- 2026-02-19T12:12:00Z — Ran EditMode suite: 274/274 passed (`Logs/TestResults-latest.xml`).
- 2026-02-19T12:14:00Z — Ran full PlayMode suite: 85 passed, 2 ignored, 0 failed; new autosolve test passed (`Logs/PlayModeTestResults-latest.xml`).
