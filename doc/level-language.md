# Decantra Level Language (v1)

## Overview
This document specifies the canonical, versioned level language used for exporting and sharing Decantra levels and move history. The format is JSON and must be deterministic and stable.

## Identification
- `lang` (string): must be exactly `"decantra-level"`.
- `version` (integer): format version. Current version is `1`.

## Structure
### Top-level object
| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `lang` | string | yes | Must be `"decantra-level"`. |
| `version` | int | yes | Must be `1`. |
| `level` | int | yes | The level index. |
| `grid` | object | yes | Grid dimensions. |
| `initial` | object | yes | Initial state at level entry. |
| `moves` | array | yes | Successful moves only, chronological order. |

### Grid
`grid` is an object with:
- `grid.rows` (int) > 0
- `grid.cols` (int) > 0

### Initial cells
`initial.cells` is a 2D array sized `[rows][cols]` in **row-major** order.
- Each cell is either:
  - `null` (no bottle), or
  - a **bottle** object.

### Bottle object
A bottle object has:
- `capacity` (int): maximum volume.
- `layers` (array): ordered list of `[color, volume]` pairs from **bottom to top**.
- `flags` (array, optional): list of string flags. Current flag: `"sink"`.

Implicit empty space is `capacity - sum(layer.volume)`.

### Move history
`moves` is an array of move objects with shape:
- `{ "from": [row, col], "to": [row, col] }`

Moves are **successful moves only** and are applied in chronological order. The pour amount is **derived** from current game rules at replay time.

## Ordering rules
- Cells are **row-major**, left-to-right within a row, then top-to-bottom.
- `layers` are ordered **bottom-to-top**.
- The serialized JSON must use a stable field order: `lang`, `version`, `level`, `grid`, `initial`, `moves`.

## Determinism rules
- Export must be deterministic given the same initial state and move history.
- Replay must be deterministic using the current rule set for pours.

## Validation rules (reject conditions)
Reject a document if any of the following are true:
- `lang` is not `"decantra-level"`.
- `version` is not `1`.
- `grid.rows` or `grid.cols` are non-positive.
- `initial.cells` dimensions do not match `grid.rows` and `grid.cols`.
- Any bottle has `capacity <= 0`.
- Any layer has `volume <= 0` or missing color string.
- Sum of layer volumes exceeds bottle capacity.
- A bottle with `"sink"` flag has multiple colors.
- Any move has coordinates outside the grid.

## Backwards compatibility policy
- Future versions must increment `version` and remain backwards compatible where possible.
- Parsers must **reject** unknown `version` values.
- New optional fields may be added in later versions without breaking v1 parsing rules.

## Replay semantics
1. Load the `initial` grid.
2. For each move in `moves`:
   - Determine the pour amount according to current game rules.
   - Apply the pour to obtain the next state.
3. The resulting state must match the state achieved by the original play-through.

## Decantra volume convention
Decantra’s in-game slots are serialized using a fixed unit. The default is **simple integer unit (1-10)**. As a result, capacities and layer volumes range from 1 to 10.

## Example (verbatim)
```json
{
  "lang": "decantra-level",
  "version": 1,
  "level": 10,
  "grid": { "rows": 3, "cols": 3 },
  "initial": {
    "cells": [
      [
        { "capacity": 10, "layers": [["blue", 3], ["orange", 1]] },
        { "capacity": 10, "layers": [], "flags": ["sink"] },
        { "capacity": 10, "layers": [["purple", 7], ["yellow", 3]] }
      ],
      [
        { "capacity": 5, "layers": [["green", 1]] },
        null,
        null
      ],
      [ null, null, null ]
    ]
  },
  "moves": [
    { "from": [0, 2], "to": [0, 0] },
    { "from": [0, 0], "to": [1, 0] }
  ]
}
```
