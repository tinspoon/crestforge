using UnityEngine;

namespace Crestforge.Utilities
{
    /// <summary>
    /// Provides light haptic feedback on mobile devices.
    /// Uses Android's VibrationEffect for short, subtle vibrations.
    /// </summary>
    public static class HapticFeedback
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static bool initialized = false;

        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HapticFeedback] Failed to initialize: {e.Message}");
            }
        }

        /// <summary>
        /// Trigger a light haptic tick (very short vibration)
        /// </summary>
        public static void LightTap()
        {
            Initialize();
            if (vibrator == null) return;

            try
            {
                // API 26+ (Android 8.0+): Use VibrationEffect
                if (GetSDKInt() >= 26)
                {
                    using (AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        // EFFECT_TICK = 2 (light tick feedback)
                        AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createPredefined", 2);
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    // Fallback for older devices: 10ms vibration
                    vibrator.Call("vibrate", 10L);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HapticFeedback] LightTap failed: {e.Message}");
            }
        }

        /// <summary>
        /// Trigger a medium haptic click
        /// </summary>
        public static void MediumTap()
        {
            Initialize();
            if (vibrator == null) return;

            try
            {
                if (GetSDKInt() >= 26)
                {
                    using (AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        // EFFECT_CLICK = 0 (standard click feedback)
                        AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createPredefined", 0);
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", 20L);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HapticFeedback] MediumTap failed: {e.Message}");
            }
        }

        private static int GetSDKInt()
        {
            using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return version.GetStatic<int>("SDK_INT");
            }
        }
#else
        /// <summary>
        /// Trigger a light haptic tick (no-op on non-Android platforms)
        /// </summary>
        public static void LightTap()
        {
            // No haptics in editor or non-Android platforms
        }

        /// <summary>
        /// Trigger a medium haptic click (no-op on non-Android platforms)
        /// </summary>
        public static void MediumTap()
        {
            // No haptics in editor or non-Android platforms
        }
#endif
    }
}
