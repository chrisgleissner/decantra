/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;

namespace Decantra.Presentation.Services
{
    public interface IShareService
    {
        void ShareText(string text);
    }

    public static class ShareServiceFactory
    {
        public static IShareService CreateDefault()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidShareService();
#else
            return new EditorShareService();
#endif
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    public sealed class AndroidShareService : IShareService
    {
        public void ShareText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share Level"))
                {
                    currentActivity.Call("startActivity", chooser);
                }
            }
        }
    }
#else
    public sealed class EditorShareService : IShareService
    {
        public static string LastSharedText { get; private set; }
        public static int ShareCount { get; private set; }

        public void ShareText(string text)
        {
            LastSharedText = text;
            ShareCount++;
            Debug.Log("ShareService: Captured share payload for tests.");
        }

        public static void Reset()
        {
            LastSharedText = null;
            ShareCount = 0;
        }
    }
#endif
}
