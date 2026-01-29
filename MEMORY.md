# Memory

## Project Summary

Decantra is a Unity LTS 2D liquid sort puzzle game with pure C# domain logic, deterministic level generation, and optimal solver. Android is MVP; iOS-ready architecture with platform-agnostic gameplay.

## Key Requirements Snapshot

- Unity LTS, 2D URP, asmdef boundaries.
- 3x3 bottles, capacity 4, 6 colors.
- Deterministic reverse-construction generator + solver validation.
- Optimal BFS solver with hashing and pruning.
- >= 80% domain coverage gate using Unity Code Coverage.
- Android package id `uk.gleissner.decantra`, product name `Decantra`.
- One-command build/install/run via ADB.

## Current State

- Domain model, solver, generator, scoring, and persistence are implemented with EditMode tests.
- Presentation layer scripts and a Scene Setup menu are in place; UI, bottle grid, and controller wiring are underway.
- Android build/install scripts and Unity batch build entry point are implemented; device run still to validate.
- Coverage gate script added and wired into test runner; verification still pending.
