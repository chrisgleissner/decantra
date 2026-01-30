using UnityEngine;
using UnityEngine.UI;

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
                highScoreText.text = $"HIGH\n{highScore}";
            }

            if (maxLevelText != null)
            {
                maxLevelText.text = $"MAX LV\n{maxLevel}";
            }
        }
    }
}
