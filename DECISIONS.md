# Decisions Log

## 2026-01-29

### D-001: Unity LTS + URP 2D
- **Decision**: Use latest Unity LTS with 2D URP.
- **Reason**: Required by spec; ensures long-term support and consistent pipeline.
- **Implications**: URP setup required; assets and shaders must be URP-compatible.

### D-002: Pure C# domain core with asmdef boundaries
- **Decision**: Domain logic implemented as pure C# assemblies with no UnityEngine dependencies.
- **Reason**: Testability, coverage enforcement, and platform agnosticism.
- **Implications**: Presentation layer uses adapters to bind domain to Unity scenes.

### D-003: Optimal solver via BFS with hashing
- **Decision**: Use BFS with state hashing and pruning to compute optimal moves.
- **Reason**: Guarantees optimality for MVP and deterministic results.
- **Implications**: Must maintain efficient state encoding and pruning rules.

### D-004: Level generation via reverse construction + solver validation
- **Decision**: Generate levels by applying valid reverse moves from solved state using deterministic RNG, then validate with solver.
- **Reason**: Ensures solvability and reproducibility by seed.
- **Implications**: Requires deterministic RNG and bounds on difficulty.
