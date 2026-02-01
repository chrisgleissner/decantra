/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

        public void Render(int levelIndex, int movesUsed, int movesAllowed, int optimalMoves, int score, int highScore, int maxLevel)
        {
            if (titleText != null)
            {
                titleText.text = "Decantra";
            }

            if (levelText != null)
            {
                levelText.text = $"LEVEL\n{levelIndex}";
            }

            if (movesText != null)
            {
                movesText.text = $"MOVES\n{movesUsed}/{movesAllowed}";
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
                highScoreText.text = $"BEST\n{highScore}";
            }

            if (maxLevelText != null)
            {
                maxLevelText.text = $"MAX\n{maxLevel}";
            }
        }

        public void AnimateScoreUpdate()
        {
            if (scoreText == null) return;
            StopAllCoroutines();
            StartCoroutine(ScoreEffect());
        }

        private IEnumerator ScoreEffect()
        {
            float duration = 0.6f;
            float time = 0f;
            Vector3 originalScale = Vector3.one;
            Color originalColor = new Color(1f, 0.98f, 0.92f, 1f);

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float curve = Mathf.Sin(t * Mathf.PI);
                float scale = 1f + 0.35f * curve;

                scoreText.rectTransform.localScale = originalScale * scale;
                scoreText.color = Color.Lerp(originalColor, new Color(1f, 1f, 0.6f, 1f), curve);

                yield return null;
            }
            scoreText.rectTransform.localScale = originalScale;
            scoreText.color = originalColor;
        }
    }
}
