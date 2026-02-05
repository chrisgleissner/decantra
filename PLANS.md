# PLANS - Level Difficulty Indicator

## Execution Plan
- [x] 1. Audit current HUD layout + difficulty sources (HudView, GameController, DifficultyScorer, SceneBootstrap).
- [x] 2. Implement deterministic difficulty capture from DifficultyScorer outputs in runtime flow.
- [x] 3. Render 3-circle indicator next to the level number and keep group centered via text layout.
- [ ] 4. Verify visuals across easy/medium/hard and multi-digit levels.
- [ ] 5. Run EditMode/PlayMode tests.
- [ ] 6. Run Android build to confirm buildability.

## Notes
- Visual verification and build/test steps require Unity editor and Android build tooling.
- Tests/build attempted; failures observed in BackgroundGeneratorTests.cs (CS0221, CS0103) during build bootstrap.
