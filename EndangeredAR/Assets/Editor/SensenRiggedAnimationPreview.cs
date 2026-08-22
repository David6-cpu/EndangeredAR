using System.IO;
using System.Linq;
using EndangeredAR.AR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndangeredAR.Editor
{
    public static class SensenRiggedAnimationPreview
    {
        private const string ControllerPath = "Assets/Animations/Sensen/SensenRigged.controller";
        private const string DemoScenePath = "Assets/Scenes/DemoScene.unity";

        [MenuItem("Endangered AR/Debug/Sensen/Open Demo Scene")]
        public static void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Endangered AR/Debug/Sensen/Play Idle")]
        private static void PlayIdle()
        {
            var animator = FindAnimator();
            if (animator == null)
            {
                Debug.LogWarning("Sensen animation preview requires Play Mode with the rigged model visible.");
                return;
            }

            animator.ResetTrigger("Taunt");
            animator.Play("Idle", 0, 0f);
        }

        [MenuItem("Endangered AR/Debug/Sensen/Play Taunt")]
        private static void PlayTaunt()
        {
            var animator = FindAnimator();
            if (animator == null)
            {
                Debug.LogWarning("Sensen animation preview requires Play Mode with the rigged model visible.");
                return;
            }

            animator.SetTrigger("Taunt");
        }

        [MenuItem("Endangered AR/Debug/Sensen/Simulate Sensen Scan")]
        private static void SimulateSensenScan()
        {
            var scanController = UnityEngine.Object.FindObjectOfType<ARImageScanController>();
            if (!Application.isPlaying || scanController == null)
            {
                Debug.LogWarning("Sensen scan preview requires DemoScene in Play Mode.");
                return;
            }

            scanController.SimulateMarkerDetected("sensen");
        }

        [MenuItem("Endangered AR/Debug/Sensen/Capture Game View")]
        private static void CaptureGameView()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Game View capture requires Play Mode.");
                return;
            }

            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/verification"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "2026-08-22-r3.0-rigged-sensen-game-view.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"R3.0 Game View capture requested: {path}");
        }

        private static Animator FindAnimator()
        {
            return UnityEngine.Object.FindObjectsOfType<Animator>(true)
                .FirstOrDefault(animator =>
                    animator.runtimeAnimatorController != null &&
                    AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == ControllerPath);
        }
    }
}
