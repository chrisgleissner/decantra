# Status

**Project**: Decantra (Liquid Sort Puzzle)  
**Date**: 2026-01-29  
**Phase**: Phase 3 (Unity presentation & app wiring)

## Completed

- Created [`SPEC.md`](SPEC.md)
- Created [`PLANS.md`](PLANS.md)
- Created [`DECISIONS.md`](DECISIONS.md)
- Implemented domain model, rules, scoring, and persistence skeletons
- Added domain EditMode tests for bottle, level state, and scoring
- Implemented solver, state encoder, and level generator
- Added EditMode tests for solver and generator
- Added presentation layer scripts (controller, views) and PlayMode test skeleton
- Initialized Unity LTS project (6000.3.5f2) with URP packages and Code Coverage
- Configured app identifiers and main scene entry

## In Progress

- Phase 3: Unity presentation and app wiring (scene setup, UI, input, animations).
- Phase 4: Android build/install scripts and Unity batch build (scripts started).
- Phase 5: Coverage gate setup (test runner updated, gate script added).

## Next Up

- Validate play loop in Main scene and finish PlayMode wiring.
- Finalize Android build/install scripts and run a device install.
- Verify coverage gate passes at >= 80% domain coverage.

## Risks / Blockers

- Coverage gate setup still pending.
