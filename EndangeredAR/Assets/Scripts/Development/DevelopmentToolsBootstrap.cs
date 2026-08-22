#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.Development
{
    public static class DevelopmentToolsBootstrap
    {
        private const string RootName = "EndangeredAR Development Tools";
        private static AnimalAnimationDebugPanel instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureInitialized();
        }

        private static AnimalAnimationDebugPanel EnsureInitialized()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = Object.FindFirstObjectByType<AnimalAnimationDebugPanel>(FindObjectsInactive.Include);
            if (instance != null)
            {
                return instance;
            }

            var root = new GameObject(RootName, typeof(RectTransform));
            Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1290f, 2796f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            instance = root.AddComponent<AnimalAnimationDebugPanel>();
            return instance;
        }
    }
}
#endif
