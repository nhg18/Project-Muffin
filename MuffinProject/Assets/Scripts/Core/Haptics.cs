using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>
    /// 짧은 진동 (10-ui.md 4절 모바일 — 훑기 4ms · 집기 10ms · 크게 보기 15ms). 지원하는 기기에서만 — 안드로이드는 시스템 진동기를 ms 로 부르고, 그 밖(iOS · PC)은 하지 않는다.
    /// iOS 의 Handheld.Vibrate 는 길이를 못 정하고 너무 길어(약 0.4초) 쓰지 않는다 — 짧은 진동은 네이티브 플러그인이 있어야 한다.
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static bool _looked;
#endif

        public static void Vibrate(int milliseconds)
        {
            if (milliseconds <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaObject vibrator = Vibrator();
            if (vibrator == null) return;

            try
            {
                vibrator.Call("vibrate", (long)milliseconds);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[{nameof(Haptics)}] 진동 실패 — {e.Message}");
                _vibrator = null;
            }
#endif
        }

        // 빌드가 진동 권한(VIBRATE)을 넣게 — Unity 는 Handheld.Vibrate 를 부르는 코드가 있으면 권한을 더한다. 실제로는 부르지 않는다.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051")]
        private static void KeepVibratePermission() => Handheld.Vibrate();

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject Vibrator()
        {
            if (_looked) return _vibrator;

            _looked = true;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[{nameof(Haptics)}] 진동기를 못 찾음 — {e.Message}");
            }
            return _vibrator;
        }
#endif
    }
}
