/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.IO;
using UnityEngine;

namespace Decantra.Presentation
{
    public sealed class RuntimeScreenshot : MonoBehaviour
    {
        [SerializeField] private string fileName = "screen-runtime.png";
        [SerializeField] private float delaySeconds = 1.0f;

        private void Start()
        {
            Debug.Log("RuntimeScreenshot: scheduled");
            Debug.Log($"RuntimeScreenshot path: {Application.persistentDataPath}");
            StartCoroutine(CaptureAfterDelay());
        }

        private IEnumerator CaptureAfterDelay()
        {
            yield return new WaitForSeconds(delaySeconds);
            yield return new WaitForEndOfFrame();
            var path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null)
            {
                Debug.Log("RuntimeScreenshot failed: texture null");
                yield break;
            }

            var bytes = texture.EncodeToPNG();
            Destroy(texture);

            File.WriteAllBytes(path, bytes);
            Debug.Log($"RuntimeScreenshot saved: {path}");
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"RuntimeScreenshot exists: {File.Exists(path)}");
        }
    }
}
