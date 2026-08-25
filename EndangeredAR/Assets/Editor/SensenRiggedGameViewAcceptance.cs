using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using EndangeredAR.AR;
using EndangeredAR.Models;
using EndangeredAR.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace EndangeredAR.Editor
{
    [InitializeOnLoad]
    public static class SensenRiggedGameViewAcceptance
    {
        private const string DemoScenePath = "Assets/Scenes/DemoScene.unity";
        private const string RunningKey = "EndangeredAR.R30.Acceptance.Running";
        private const string ModeKey = "EndangeredAR.R30.Acceptance.Mode";
        private const string RiggedMode = "rigged";
        private const string EatMode = "eat";
        private const string LegacyMode = "legacy";
        private const string ModelRootName = "Animal GLB Runtime Root";

        private static readonly List<float> FrameRates = new List<float>();

        private static int stage;
        private static double stageStartedAt;
        private static double loadStartedAt;
        private static AnimalModelLoader modelLoader;
        private static AnimalModelController animationController;
        private static Animator animator;
        private static SkinnedMeshRenderer skinnedRenderer;
        private static Renderer visibleRenderer;
        private static Transform hostTransform;
        private static Transform rootBone;
        private static Vector3 hostPosition;
        private static Quaternion hostRotation;
        private static Vector3 hostScale;
        private static Vector3 rootPosition;
        private static Quaternion rootRotation;
        private static bool idleObserved;
        private static bool tauntObserved;
        private static bool eatObserved;
        private static bool tauntCaptureRequested;
        private static double firstDisplaySeconds;
        private static double idleMedianFps;
        private static double tauntMedianFps;
        private static UnityEngine.Object transientDefinition;

        static SensenRiggedGameViewAcceptance()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (SessionState.GetBool(RunningKey, false) && EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Endangered AR/Debug/Sensen/Run Rigged Game View Acceptance")]
        public static void RunRiggedAcceptance()
        {
            Start(RiggedMode);
        }

        [MenuItem("Endangered AR/Debug/Sensen/Run Eat Game View Acceptance")]
        public static void RunEatAcceptance()
        {
            Start(EatMode);
        }

        [MenuItem("Endangered AR/Debug/Sensen/Run Legacy GLB Baseline")]
        public static void RunLegacyBaseline()
        {
            Start(LegacyMode);
        }

        private static void Start(string mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before starting R3.0 Game View acceptance.");
            }

            SessionState.SetBool(RunningKey, true);
            SessionState.SetString(ModeKey, mode);
            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ResetRuntimeState();
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(RunningKey, false);
                if (transientDefinition != null)
                {
                    UnityEngine.Object.DestroyImmediate(transientDefinition);
                    transientDefinition = null;
                }
            }
        }

        private static void ResetRuntimeState()
        {
            stage = 0;
            stageStartedAt = EditorApplication.timeSinceStartup;
            FrameRates.Clear();
            modelLoader = null;
            animationController = null;
            animator = null;
            skinnedRenderer = null;
            visibleRenderer = null;
            hostTransform = null;
            rootBone = null;
            idleObserved = false;
            tauntObserved = false;
            eatObserved = false;
            tauntCaptureRequested = false;
            firstDisplaySeconds = 0d;
            idleMedianFps = 0d;
            tauntMedianFps = 0d;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            try
            {
                if (EditorApplication.timeSinceStartup - stageStartedAt > 20d)
                {
                    Complete(false, $"Timed out in acceptance stage {stage}.");
                    return;
                }

                switch (stage)
                {
                    case 0:
                        BeginLoad();
                        break;
                    case 1:
                        WaitForVisibleModel();
                        break;
                    case 2:
                        SampleIdleOrLegacy();
                        break;
                    case 3:
                        SampleTaunt();
                        break;
                    case 4:
                        VerifyReturnToIdle();
                        break;
                    case 5:
                        FinishLegacyCapture();
                        break;
                }
            }
            catch (Exception exception)
            {
                Complete(false, exception.ToString());
            }
        }

        private static void BeginLoad()
        {
            if (EditorApplication.timeSinceStartup - stageStartedAt < 0.75d)
            {
                return;
            }

            modelLoader = UnityEngine.Object.FindObjectsOfType<AnimalModelLoader>(true)
                .SingleOrDefault();
            if (modelLoader == null)
            {
                throw new InvalidOperationException("DemoScene has no AnimalModelLoader.");
            }

            loadStartedAt = EditorApplication.timeSinceStartup;
            var mode = SessionState.GetString(ModeKey, RiggedMode);
            if (UsesRiggedModel(mode))
            {
                var scanner = UnityEngine.Object.FindObjectOfType<ARImageScanController>();
                if (scanner == null)
                {
                    throw new InvalidOperationException("DemoScene has no ARImageScanController.");
                }

                scanner.SimulateMarkerDetected("sensen");
            }
            else
            {
                var canonical = Resources.Load<AnimalDefinition>("Animals/Sensen");
                if (canonical == null)
                {
                    throw new InvalidOperationException("Canonical Sensen definition could not be loaded.");
                }

                var legacyDefinition = UnityEngine.Object.Instantiate(canonical);
                transientDefinition = legacyDefinition;
                var prefabField = typeof(AnimalDefinition).GetField(
                    "modelPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (prefabField == null)
                {
                    throw new InvalidOperationException("AnimalDefinition.modelPrefab field was not found.");
                }

                prefabField.SetValue(legacyDefinition, null);
                modelLoader.gameObject.SetActive(true);
                modelLoader.transform.position = legacyDefinition.ExperiencePosition;
                modelLoader.Configure(legacyDefinition);
                var controller = UnityEngine.Object.FindObjectOfType<DemoAppController>(true);
                var enterModelView = typeof(DemoAppController).GetMethod(
                    "EnterModelView",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (controller == null || enterModelView == null)
                {
                    throw new InvalidOperationException("DemoScene model view could not be opened for the legacy baseline.");
                }

                enterModelView.Invoke(controller, null);
            }

            Advance(1);
        }

        private static bool UsesRiggedModel(string mode)
        {
            return mode == RiggedMode || mode == EatMode;
        }

        private static void WaitForVisibleModel()
        {
            var root = modelLoader.transform.Find(ModelRootName);
            if (root == null)
            {
                return;
            }

            visibleRenderer = root.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
            if (visibleRenderer == null)
            {
                return;
            }

            firstDisplaySeconds = EditorApplication.timeSinceStartup - loadStartedAt;
            hostTransform = modelLoader.transform;
            hostPosition = hostTransform.position;
            hostRotation = hostTransform.rotation;
            hostScale = hostTransform.localScale;
            animator = root.GetComponentInChildren<Animator>(true);
            animationController = root.GetComponentInChildren<AnimalModelController>(true);
            skinnedRenderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            rootBone = skinnedRenderer == null ? null : skinnedRenderer.rootBone;
            if (rootBone != null)
            {
                rootPosition = rootBone.localPosition;
                rootRotation = rootBone.localRotation;
            }

            Advance(2);
        }

        private static void SampleIdleOrLegacy()
        {
            SampleFrameRate();
            if (EditorApplication.timeSinceStartup - stageStartedAt < 3d)
            {
                if (animator != null)
                {
                    idleObserved |= animator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
                }

                return;
            }

            var mode = SessionState.GetString(ModeKey, RiggedMode);
            Capture(mode == RiggedMode
                ? "rigged-sensen-idle"
                : mode == EatMode
                    ? "sensen-eat-idle"
                    : "legacy-sensen");
            if (mode == LegacyMode)
            {
                Advance(5);
                return;
            }

            if (animator == null)
            {
                throw new InvalidOperationException("Rigged model has no Animator at runtime.");
            }

            idleMedianFps = Median(FrameRates);
            if (mode == EatMode)
            {
                if (animationController == null)
                {
                    throw new InvalidOperationException("Rigged model has no AnimalModelController at runtime.");
                }

                var result = animationController.TryPlayAction(AIAction.Eat);
                if (result != ActionRequestResult.Played)
                {
                    throw new InvalidOperationException($"Eat action request was rejected with {result}.");
                }
            }
            else
            {
                animator.SetTrigger("Taunt");
            }

            FrameRates.Clear();
            Advance(3);
        }

        private static void SampleTaunt()
        {
            SampleFrameRate();
            var mode = SessionState.GetString(ModeKey, RiggedMode);
            if (mode == EatMode)
            {
                eatObserved |= animator.GetCurrentAnimatorStateInfo(0).IsName("Eat");
            }
            else
            {
                tauntObserved |= animator.GetCurrentAnimatorStateInfo(0).IsName("Taunt");
            }

            var elapsed = EditorApplication.timeSinceStartup - stageStartedAt;
            if (elapsed >= 1.2d && !tauntCaptureRequested)
            {
                Capture(mode == EatMode ? "sensen-eat-keyframe" : "rigged-sensen-taunt");
                tauntCaptureRequested = true;
            }

            if (elapsed >= (mode == EatMode ? 4.25d : 3.5d))
            {
                tauntMedianFps = Median(FrameRates);
                Advance(4);
            }
        }

        private static void FinishLegacyCapture()
        {
            if (EditorApplication.timeSinceStartup - stageStartedAt >= 1d)
            {
                Complete(true, "Legacy GLB baseline captured.");
            }
        }

        private static void VerifyReturnToIdle()
        {
            var mode = SessionState.GetString(ModeKey, RiggedMode);
            idleObserved |= animator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
            var actionObserved = mode == EatMode ? eatObserved : tauntObserved;
            if (!idleObserved || !actionObserved)
            {
                if (EditorApplication.timeSinceStartup - stageStartedAt < 2d)
                {
                    return;
                }

                Complete(false, mode == EatMode
                    ? "Animator did not observe both Idle and Eat states."
                    : "Animator did not observe both Idle and Taunt states.");
                return;
            }

            var hostStable = IsHostWithinBreathingEnvelope();
            var rootStable = rootBone == null ||
                             (Approximately(rootBone.localPosition, rootPosition) &&
                              Approximately(rootBone.localRotation, rootRotation));
            var fallbackHidden = !HasVisibleFallbackRenderer();
            if (mode == EatMode)
            {
                Capture("sensen-eat-return-idle");
            }

            Complete(hostStable && rootStable && fallbackHidden, hostStable && rootStable && fallbackHidden
                ? mode == EatMode
                    ? "Rigged Idle/Eat cycle completed without Animator drift; the legacy fallback stayed hidden."
                    : "Rigged Idle/Taunt cycle completed without Animator drift; the legacy fallback stayed hidden."
                : $"Runtime presentation failed (hostStable={hostStable}, rootStable={rootStable}, fallbackHidden={fallbackHidden}).");
        }

        private static void SampleFrameRate()
        {
            if (Time.unscaledDeltaTime > 0.00001f)
            {
                FrameRates.Add(1f / Time.unscaledDeltaTime);
            }
        }

        private static void Capture(string suffix)
        {
            var directory = VerificationDirectory();
            Directory.CreateDirectory(directory);
            var mode = SessionState.GetString(ModeKey, RiggedMode);
            var prefix = mode == EatMode ? "2026-08-24-r3.2b1" : "2026-08-22-r3.0";
            ScreenCapture.CaptureScreenshot(Path.Combine(
                directory,
                $"{prefix}-{suffix}.png"));
        }

        private static void Complete(bool passed, string message)
        {
            if (stage == int.MaxValue)
            {
                return;
            }

            var mode = SessionState.GetString(ModeKey, RiggedMode);
            var medianFps = mode == RiggedMode ? tauntMedianFps : Median(FrameRates);
            var mesh = skinnedRenderer == null
                ? visibleRenderer?.GetComponent<MeshFilter>()?.sharedMesh
                : skinnedRenderer.sharedMesh;
            var texture = visibleRenderer == null || visibleRenderer.sharedMaterial == null
                ? null
                : visibleRenderer.sharedMaterial.mainTexture;
            var report = string.Join("\n", new[]
            {
                $"mode={mode}",
                $"passed={passed}",
                $"message={message.Replace('\n', ' ')}",
                $"firstDisplaySeconds={Format(firstDisplaySeconds)}",
                $"medianFps={Format(medianFps)}",
                $"idleMedianFps={Format(idleMedianFps)}",
                $"tauntMedianFps={Format(tauntMedianFps)}",
                $"vertexCount={(mesh == null ? 0 : mesh.vertexCount)}",
                $"triangleCount={(mesh == null ? 0 : mesh.triangles.Length / 3)}",
                $"boneCount={(skinnedRenderer == null || skinnedRenderer.bones == null ? 0 : skinnedRenderer.bones.Length)}",
                $"meshRuntimeBytes={(mesh == null ? 0 : Profiler.GetRuntimeMemorySizeLong(mesh))}",
                $"textureRuntimeBytes={(texture == null ? 0 : Profiler.GetRuntimeMemorySizeLong(texture))}",
                $"totalAllocatedBytes={Profiler.GetTotalAllocatedMemoryLong()}",
                $"monoUsedBytes={Profiler.GetMonoUsedSizeLong()}",
                $"idleObserved={idleObserved}",
                $"tauntObserved={tauntObserved}",
                $"eatObserved={eatObserved}",
                $"rendererBoundsSize={(visibleRenderer == null ? Vector3.zero : visibleRenderer.bounds.size)}",
                $"hostVerticalDelta={(hostTransform == null ? 0f : Mathf.Abs(hostTransform.position.y - hostPosition.y)).ToString("0.000000", CultureInfo.InvariantCulture)}",
                $"hostStable={IsHostWithinBreathingEnvelope()}",
                $"rootStable={(rootBone == null || (Approximately(rootBone.localPosition, rootPosition) && Approximately(rootBone.localRotation, rootRotation)))}",
                $"fallbackHidden={!HasVisibleFallbackRenderer()}",
                $"utc={DateTime.UtcNow:O}"
            }) + "\n";

            var reportDirectory = Environment.GetEnvironmentVariable("TEST_RESULTS_DIR");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = Path.GetTempPath();
            }

            Directory.CreateDirectory(reportDirectory);
            var reportFileName = mode == EatMode
                ? "animalsar-r32b1-eat-gameview-report.txt"
                : $"animalsar-r30-{mode}-gameview-report.txt";
            var reportPath = Path.Combine(reportDirectory, reportFileName);
            File.WriteAllText(reportPath, report);
            Debug.Log($"{(mode == EatMode ? "R3.2B1" : "R3.0")} {mode} Game View acceptance completed: passed={passed}; {message}");
            stage = int.MaxValue;
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
        }

        private static string VerificationDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/verification"));
        }

        private static void Advance(int nextStage)
        {
            stage = nextStage;
            stageStartedAt = EditorApplication.timeSinceStartup;
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude <= 0.000001f;
        }

        private static bool Approximately(Quaternion left, Quaternion right)
        {
            return Quaternion.Angle(left, right) <= 0.01f;
        }

        private static bool IsHostWithinBreathingEnvelope()
        {
            if (hostTransform == null)
            {
                return false;
            }

            var current = hostTransform.position;
            var horizontalStable = Mathf.Abs(current.x - hostPosition.x) <= 0.001f &&
                                   Mathf.Abs(current.z - hostPosition.z) <= 0.001f;
            const float breathingPeakToPeak = 0.055f;
            return horizontalStable &&
                   Mathf.Abs(current.y - hostPosition.y) <= breathingPeakToPeak &&
                   Approximately(hostTransform.rotation, hostRotation) &&
                   Approximately(hostTransform.localScale, hostScale);
        }

        private static bool HasVisibleFallbackRenderer()
        {
            if (modelLoader == null)
            {
                return false;
            }

            var modelRoot = modelLoader.transform.Find(ModelRootName);
            return modelLoader.GetComponentsInChildren<Renderer>(true).Any(rendererComponent =>
                rendererComponent != null &&
                rendererComponent.enabled &&
                rendererComponent.gameObject.activeInHierarchy &&
                (modelRoot == null || (rendererComponent.transform != modelRoot && !rendererComponent.transform.IsChildOf(modelRoot))));
        }

        private static double Median(List<float> values)
        {
            if (values.Count == 0)
            {
                return 0d;
            }

            var ordered = values.OrderBy(value => value).ToArray();
            var middle = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[middle - 1] + ordered[middle]) * 0.5d
                : ordered[middle];
        }

        private static string Format(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
