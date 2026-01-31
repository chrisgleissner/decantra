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
                levelText.text = $"LEVEL\n<size=72>{levelIndex}</size>";
            }

            if (movesText != null)
            {
                movesText.text = $"MOVES\n<size=52>{movesUsed}/{movesAllowed}</size>";
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
                scoreText.text = $"SCORE\n<size=52>{score}</size>";
            }

            if (highScoreText != null)
            {
                highScoreText.text = $"BEST\n<size=44>{highScore}</size>";
            }

            if (maxLevelText != null)
            {
                maxLevelText.text = $"MAX\n<size=44>{maxLevel}</size>";
            }
        }
    }
}
