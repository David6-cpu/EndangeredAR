#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.Development
{
    public static class DevelopmentToolsBootstrap
    {
        private const string RootName = "EndangeredAR Development Tools";
        private static GameObject rootInstance;
        private static AnimalAnimationDebugPanel animationInstance;
        private static CharacterMemoryDebugPanel memoryInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            rootInstance = null;
            animationInstance = null;
            memoryInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureInitialized();
        }

        private static AnimalAnimationDebugPanel EnsureInitialized()
        {
            if (animationInstance != null && memoryInstance != null)
            {
                return animationInstance;
            }

            animationInstance = animationInstance != null
                ? animationInstance
                : Object.FindFirstObjectByType<AnimalAnimationDebugPanel>(FindObjectsInactive.Include);
            memoryInstance = memoryInstance != null
                ? memoryInstance
                : Object.FindFirstObjectByType<CharacterMemoryDebugPanel>(FindObjectsInactive.Include);
            rootInstance = animationInstance != null
                ? animationInstance.gameObject
                : memoryInstance != null
                    ? memoryInstance.gameObject
                    : GameObject.Find(RootName);

            if (rootInstance == null)
            {
                rootInstance = CreateRoot();
            }

            animationInstance = animationInstance != null
                ? animationInstance
                : rootInstance.GetComponent<AnimalAnimationDebugPanel>() ??
                  rootInstance.AddComponent<AnimalAnimationDebugPanel>();
            memoryInstance = memoryInstance != null
                ? memoryInstance
                : rootInstance.GetComponent<CharacterMemoryDebugPanel>() ??
                  rootInstance.AddComponent<CharacterMemoryDebugPanel>();
            return animationInstance;
        }

        private static GameObject CreateRoot()
        {
            var root = new GameObject(RootName, typeof(RectTransform));
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(root);
            }

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1290f, 2796f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            return root;
        }
    }
}
#endif
