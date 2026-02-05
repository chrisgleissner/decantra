/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using UnityEngine.UI;
using UnityEngine;

namespace Decantra.Presentation.View
{
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private Text levelText;
        [SerializeField] private Text movesText;
        [SerializeField] private Text optimalText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text highScoreText;
        [SerializeField] private Text maxLevelText;
        [SerializeField] private Text titleText;

        private Coroutine _scoreEffectRoutine;

        public void Render(
            int levelIndex,
            int movesUsed,
            int movesAllowed,
            int optimalMoves,
            int score,
            int highScore,
            int maxLevel,
            int difficulty100)
        {
            if (titleText != null)
            {
                titleText.text = "Decantra";
            }

            if (levelText != null)
            {
                int clampedDifficulty = Mathf.Clamp(difficulty100, 0, 100);
                string circles = ResolveDifficultyCircles(clampedDifficulty);

                // Use smaller size for circles without affecting the level number.
                // Using a percentage keeps behavior stable across different base font sizes.
                levelText.text = $"LEVEL\n{levelIndex} <size=50%>{circles}</size>";
            }

            if (movesText != null)
            {
                movesText.text = $"MOVES\n{movesUsed} / {movesAllowed}";
            }

            if (optimalText != null)
            {
                var parent = optimalText.transform.parent;
                if (parent != null && parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(false);
                }
            }

            if (scoreText != null)
            {
                scoreText.text = $"SCORE\n{score}";
            }

            if (highScoreText != null)
            {
                highScoreText.text = $"HIGH SCORE\n{highScore}";
            }

            if (maxLevelText != null)
            {
                maxLevelText.text = $"MAX LEVEL\n{maxLevel}";
            }
        }

        private static string ResolveDifficultyCircles(int difficulty100)
        {
            // Keep behavior identical to prior implementation.
            if (difficulty100 <= 65)
            {
                return "●○○";
            }

            if (difficulty100 <= 85)
            {
                return "●●○";
            }

            return "●●●";
        }

        public void AnimateScoreUpdate()
        {
            if (scoreText == null) return;

            if (_scoreEffectRoutine != null)
            {
                StopCoroutine(_scoreEffectRoutine);
            }

            _scoreEffectRoutine = StartCoroutine(ScoreEffect());
        }

        private IEnumerator ScoreEffect()
        {
            float duration = 0.6f;
            float time = 0f;

            // Preserve prior behavior: the animation always assumes a "neutral" original state.
            // If your design ever sets scoreText scale/color elsewhere, consider capturing the
            // real originals (scoreText.rectTransform.localScale / scoreText.color) instead.
            Vector3 originalScale = Vector3.one;
            Color originalColor = new Color(1f, 0.98f, 0.92f, 1f);

            while (time < duration)
            {
                if (scoreText == null)
                {
                    _scoreEffectRoutine = null;
                    yield break;
                }

                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float curve = Mathf.Sin(t * Mathf.PI);
                float scale = 1f + 0.35f * curve;

                scoreText.rectTransform.localScale = originalScale * scale;
                scoreText.color = Color.Lerp(originalColor, new Color(1f, 1f, 0.6f, 1f), curve);

                yield return null;
            }

            if (scoreText != null)
            {
                scoreText.rectTransform.localScale = originalScale;
                scoreText.color = originalColor;
            }

            _scoreEffectRoutine = null;
        }
    }
}
