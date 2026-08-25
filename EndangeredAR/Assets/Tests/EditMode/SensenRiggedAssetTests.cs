using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using EndangeredAR.AI;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class SensenRiggedAssetTests
    {
        private const string ModelPath = "Assets/Art/Characters/Sensen/Rigged/Models/sensen_rigged_100k.fbx";
        private const string TexturePath = "Assets/Art/Characters/Sensen/Rigged/Textures/sensen_basecolor_1024.png";
        private const string MaterialPath = "Assets/Art/Characters/Sensen/Rigged/Materials/SensenRigged.mat";
        private const string ControllerPath = "Assets/Animations/Sensen/SensenRigged.controller";
        private const string EatAnimationPath = "Assets/Animations/Sensen/Clips/sensen_eat_expressive.fbx";
        private const string PrefabPath = "Assets/Prefabs/Animals/SensenRigged.prefab";
        private const string DevelopmentBootstrapPath = "Assets/Scripts/Development/DevelopmentToolsBootstrap.cs";
        private const string DevelopmentPanelPath = "Assets/Scripts/Development/AnimalAnimationDebugPanel.cs";
        private const string GameViewAcceptancePath = "Assets/Editor/SensenRiggedGameViewAcceptance.cs";

        [Test]
        public void ProductAssets_UseOnlyTheApprovedRuntimeCandidate()
        {
            Assert.That(File.Exists(ModelPath), Is.True, "The approved 100k FBX must be present.");
            Assert.That(File.Exists(TexturePath), Is.True, "The approved 1024 base color must be present.");
            Assert.That(File.Exists(MaterialPath), Is.True);
            Assert.That(File.Exists(ControllerPath), Is.True);
            Assert.That(File.Exists(EatAnimationPath), Is.True, "Only the approved Expressive Eat animation may enter product Assets.");
            Assert.That(File.Exists(PrefabPath), Is.True);

            var prohibited = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.IndexOf("sensen", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(path => path.IndexOf("80k", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("150k", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("2m", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("8192", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            Assert.That(prohibited, Is.Empty, "Backup and source-quality candidates must remain outside product Assets.");

            var productEatFbxAssets = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.IndexOf("eat", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Assert.That(productEatFbxAssets, Is.EqualTo(new[] { EatAnimationPath }));
            using (var stream = File.OpenRead(EatAnimationPath))
            using (var sha256 = SHA256.Create())
            {
                var digest = string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
                Assert.That(digest, Is.EqualTo("dd3df2814327994baf46545254e796b161400fa87e327565054286d1a99ddbfc"));
            }
        }

        [Test]
        public void ModelImporter_PreservesGenericRigAndMobileGeometryContract()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));
            Assert.That(importer.importAnimation, Is.True);
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.That(model, Is.Not.Null);
            var renderer = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            var mesh = renderer.sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.triangles.Length / 3, Is.InRange(95000, 105000));
            Assert.That(mesh.triangles.Length / 3, Is.LessThan(2000000));
            Assert.That(mesh.vertexCount, Is.LessThan(70000));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.tangents.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(renderer.rootBone, Is.Not.Null);
            Assert.That(renderer.bones, Has.Length.GreaterThanOrEqualTo(18));
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));

            foreach (var requiredBone in new[] { "Hips", "Spine", "Neck", "Head", "LeftArm", "RightArm", "LeftLeg", "RightLeg" })
            {
                Assert.That(renderer.bones.Any(bone => bone != null && bone.name.EndsWith(requiredBone, StringComparison.Ordinal)),
                    Is.True, $"Required body bone '{requiredBone}' is missing.");
            }

            Assert.That(AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<MonoScript>(), Is.Empty);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Shader>(), Is.Empty);
        }

        [Test]
        public void TextureAndMaterial_UseTheApprovedBuiltInRuntimeSetup()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(1024));
            Assert.That(importer.textureCompression, Is.Not.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(1024));
            Assert.That(texture.height, Is.EqualTo(1024));
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Standard"));
            Assert.That(material.GetTexture("_MainTex"), Is.SameAs(texture));
        }

        [Test]
        public void AnimationClips_HaveSafeLoopAndEventSettings()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            var idle = FindClip(clips, "Idle");
            var taunt = FindClip(clips, "Taunt");

            Assert.That(idle.length, Is.InRange(2.9f, 3.1f));
            Assert.That(taunt.length, Is.InRange(2.7f, 2.95f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(idle).loopTime, Is.True);
            Assert.That(AnimationUtility.GetAnimationClipSettings(taunt).loopTime, Is.False);
            Assert.That(AnimationUtility.GetAnimationEvents(idle), Is.Empty);
            Assert.That(AnimationUtility.GetAnimationEvents(taunt), Is.Empty);

            AssertClipAnimatesBone(idle, "Head");
            AssertClipAnimatesBone(idle, "Spine");
            AssertClipAnimatesBone(taunt, "LeftArm");
            AssertClipAnimatesBone(taunt, "RightLeg");
        }

        [Test]
        public void EatAnimation_UsesTheFormalAvatarAndSafeImporterContract()
        {
            var importer = AssetImporter.GetAtPath(EatAnimationPath) as ModelImporter;
            var sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .Single(avatar => avatar.isValid);

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CopyFromOther));
            Assert.That(importer.sourceAvatar, Is.SameAs(sourceAvatar));
            Assert.That(importer.importAnimation, Is.True);
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(importer.clipAnimations, Has.Length.EqualTo(1));
            Assert.That(importer.clipAnimations[0].name, Is.EqualTo("Sensen_Eat"));
            Assert.That(importer.clipAnimations[0].loopTime, Is.False);
            Assert.That(importer.clipAnimations[0].lockRootHeightY, Is.True);
            Assert.That(importer.clipAnimations[0].lockRootPositionXZ, Is.True);
            Assert.That(importer.clipAnimations[0].lockRootRotation, Is.True);
        }

        [Test]
        public void EatAnimation_HasValidatedBindingsAndNoRootDriftOrEvents()
        {
            var clip = FindClip(
                AssetDatabase.LoadAllAssetsAtPath(EatAnimationPath).OfType<AnimationClip>().ToArray(),
                "Sensen_Eat");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            Assert.That(clip.length, Is.EqualTo(3.5f).Within(0.02f));
            Assert.That(clip.frameRate, Is.EqualTo(30f).Within(0.01f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False);
            Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);

            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings, Has.Length.EqualTo(350));
            var animatedPaths = bindings.Select(binding => binding.path).Distinct().ToArray();
            var drivenPaths = bindings
                .GroupBy(binding => binding.path)
                .Where(group => group.Any(binding => HasChangingCurve(clip, binding)))
                .Select(group => group.Key)
                .ToArray();
            Assert.That(drivenPaths, Has.Length.EqualTo(9));
            Assert.That(drivenPaths, Has.Some.EndsWith("mixamorig:Head"));
            Assert.That(drivenPaths, Has.Some.EndsWith("mixamorig:LeftHand"));

            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                foreach (var binding in bindings)
                {
                    Assert.That(animator.transform.Find(binding.path), Is.Not.Null, binding.path);
                }

                var startRootPosition = instance.transform.localPosition;
                var startRootRotation = instance.transform.localRotation;
                clip.SampleAnimation(animator.gameObject, 0f);
                var poseAtStart = CaptureBonePose(animator.transform, animatedPaths);
                clip.SampleAnimation(animator.gameObject, clip.length);
                var poseAtEnd = CaptureBonePose(animator.transform, animatedPaths);
                Assert.That(Vector3.Distance(instance.transform.localPosition, startRootPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(instance.transform.localRotation, startRootRotation), Is.LessThan(0.01f));
                Assert.That(MaxPoseDelta(poseAtStart, poseAtEnd), Is.LessThan(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void AnimatorController_DefaultsToIdleAndReturnsAfterTaunt()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters, Has.Exactly(1).Matches<AnimatorControllerParameter>(parameter =>
                parameter.name == "Taunt" && parameter.type == AnimatorControllerParameterType.Trigger));
            Assert.That(controller.parameters, Has.Exactly(1).Matches<AnimatorControllerParameter>(parameter =>
                parameter.name == "Eat" && parameter.type == AnimatorControllerParameterType.Trigger));

            var stateMachine = controller.layers.Single().stateMachine;
            var idle = stateMachine.states.Single(child => child.state.name == "Idle").state;
            var taunt = stateMachine.states.Single(child => child.state.name == "Taunt").state;
            var eat = stateMachine.states.Single(child => child.state.name == "Eat").state;
            Assert.That(stateMachine.defaultState, Is.SameAs(idle));
            Assert.That(idle.motion, Is.TypeOf<AnimationClip>());
            Assert.That(taunt.motion, Is.TypeOf<AnimationClip>());
            Assert.That(eat.motion, Is.TypeOf<AnimationClip>());
            Assert.That(((AnimationClip)eat.motion).name, Is.EqualTo("Sensen_Eat"));
            Assert.That(AnimationUtility.GetAnimationClipSettings((AnimationClip)eat.motion).loopTime, Is.False);
            Assert.That(idle.transitions, Has.Exactly(1).Matches<AnimatorStateTransition>(transition =>
                transition.destinationState == taunt &&
                transition.conditions.Any(condition => condition.parameter == "Taunt" &&
                                                      condition.mode == AnimatorConditionMode.If)));
            Assert.That(idle.transitions, Has.Exactly(1).Matches<AnimatorStateTransition>(transition =>
                transition.destinationState == eat &&
                transition.conditions.Any(condition => condition.parameter == "Eat" &&
                                                      condition.mode == AnimatorConditionMode.If)));
            Assert.That(taunt.transitions, Has.Exactly(1).Matches<AnimatorStateTransition>(transition =>
                transition.destinationState == idle && transition.hasExitTime));
            Assert.That(eat.transitions, Has.Exactly(1).Matches<AnimatorStateTransition>(transition =>
                transition.destinationState == idle && transition.hasExitTime));
            Assert.That(stateMachine.anyStateTransitions, Is.Empty);
            Assert.That(taunt.transitions.Any(transition => transition.destinationState == eat), Is.False);
            Assert.That(eat.transitions.Any(transition => transition.destinationState == taunt), Is.False);
        }

        [Test]
        public void RiggedPrefab_ContainsInternalAnimationWithoutOwningTheExperienceHost()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.name, Is.EqualTo("SensenRigged"));
            Assert.That(prefab.transform.Find("RiggedModelRoot"), Is.Not.Null);

            var animator = prefab.GetComponentInChildren<Animator>(true);
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.rootBone, Is.Not.Null);
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponent<AnimalGestureController>(), Is.Null,
                "Gesture ownership belongs to the outer animal experience host.");

            var modelController = prefab.GetComponent<AnimalModelController>();
            Assert.That(modelController, Is.Not.Null,
                "The rigged prefab must own its safe, fixed-command animation gateway.");
            var serializedController = new SerializedObject(modelController);
            Assert.That(serializedController.FindProperty("animator").objectReferenceValue, Is.SameAs(animator));
            Assert.That(serializedController.FindProperty("supportedAnimalId").stringValue, Is.EqualTo("sensen"));
        }

        [Test]
        public void AnimalModelController_ExposesOnlyTheStronglyTypedActionContract()
        {
            var resultType = Type.GetType("EndangeredAR.Animals.ActionRequestResult, EndangeredAR.Runtime");
            Assert.That(resultType, Is.Not.Null, "The action request result contract must exist in the runtime assembly.");

            var supportsAction = typeof(AnimalModelController).GetMethod(
                "SupportsAction",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(AIAction) },
                null);
            Assert.That(supportsAction, Is.Not.Null);
            Assert.That(supportsAction.ReturnType, Is.EqualTo(typeof(bool)));

            var tryPlayAction = typeof(AnimalModelController).GetMethod(
                "TryPlayAction",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(AIAction) },
                null);
            Assert.That(tryPlayAction, Is.Not.Null);
            Assert.That(tryPlayAction.ReturnType, Is.EqualTo(resultType));

            var currentAction = typeof(AnimalModelController).GetProperty(
                "CurrentAction",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(currentAction, Is.Not.Null);
            Assert.That(currentAction.PropertyType, Is.EqualTo(typeof(AIAction)));

            var tryPlayTaunt = typeof(AnimalModelController).GetMethod(
                "TryPlayTaunt",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(tryPlayTaunt, Is.Not.Null);
            Assert.That(tryPlayTaunt.ReturnType, Is.EqualTo(resultType));

            var mutableAnimatorEscapeHatches = typeof(AnimalModelController)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(member =>
                    member is FieldInfo field && field.FieldType == typeof(Animator) ||
                    member is PropertyInfo property && property.PropertyType == typeof(Animator) ||
                    member is MethodInfo method && method.ReturnType == typeof(Animator))
                .ToArray();
            Assert.That(mutableAnimatorEscapeHatches, Is.Empty,
                "The safe animation gateway must not expose its mutable Animator to callers.");

            var unsafeStringEntrypoints = typeof(AnimalModelController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)))
                .ToArray();
            Assert.That(unsafeStringEntrypoints, Is.Empty,
                "Runtime animation control must not accept arbitrary state or parameter names.");
            Assert.That(typeof(AnimalModelController).GetMethod("PlayIdle"), Is.Null);
            Assert.That(typeof(AnimalModelController).GetMethod("PlayHappy"), Is.Null);
            Assert.That(typeof(AnimalModelController).GetMethod("SetScale"), Is.Null,
                "Scale remains owned by AnimalGestureController on the outer AR host.");
        }

        [Test]
        public void RiggedPrefab_ControllerSupportsOnlyTheImplementedManualActions()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                var controller = instance.GetComponent<AnimalModelController>();
                var animator = instance.GetComponentInChildren<Animator>(true);
                animator.Update(0f);

                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.SupportsAction(AIAction.Taunt), Is.True);
                Assert.That(controller.SupportsAction(AIAction.Eat), Is.True);
                Assert.That(controller.SupportsAction(AIAction.None), Is.False);
                Assert.That(controller.SupportsAction((AIAction)999), Is.False);
                Assert.That(controller.CurrentAction, Is.EqualTo(AIAction.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void DevelopmentAnimationTools_AreBuildConditionedAndBusinessIndependent()
        {
            Assert.That(File.Exists(DevelopmentBootstrapPath), Is.True);
            Assert.That(File.Exists(DevelopmentPanelPath), Is.True);

            var bootstrapSource = File.ReadAllText(DevelopmentBootstrapPath);
            var panelSource = File.ReadAllText(DevelopmentPanelPath);
            Assert.That(bootstrapSource.TrimStart(), Does.StartWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(panelSource.TrimStart(), Does.StartWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(Type.GetType("EndangeredAR.Development.DevelopmentToolsBootstrap, EndangeredAR.Runtime"), Is.Not.Null);
            Assert.That(Type.GetType("EndangeredAR.Development.AnimalAnimationDebugPanel, EndangeredAR.Runtime"), Is.Not.Null);

            var combinedSource = bootstrapSource + panelSource;
            StringAssert.DoesNotContain("AIManager", combinedSource);
            StringAssert.DoesNotContain("AIResponse", combinedSource);
            StringAssert.DoesNotContain("MissionController", combinedSource);
            StringAssert.DoesNotContain("AnimalProgress", combinedSource);
            StringAssert.DoesNotContain("Chat", combinedSource);
            StringAssert.DoesNotContain("FindObjectsOfType<Animator", combinedSource);
            StringAssert.DoesNotContain("FindObjectsByType<Animator", combinedSource);
            StringAssert.DoesNotContain("Resources.FindObjectsOfTypeAll", combinedSource);
        }

        [Test]
        public void RiggedPrefab_NormalizesCentimeterSourceToProductScale()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);

            try
            {
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.bounds.size.y, Is.InRange(1.0f, 1.3f),
                    "The approved FBX is authored in centimeters and must be normalized inside the prefab, not by changing the AR Host.");
                Assert.That(renderer.bounds.min.y, Is.EqualTo(0f).Within(0.02f),
                    "The prefab should keep the character's feet near its local ground plane.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ImportedAnimations_MoveBonesButNotTheOuterHost()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            var host = new GameObject("AR Host Test");
            try
            {
                instance.transform.SetParent(host.transform, false);
                host.transform.SetPositionAndRotation(new Vector3(4f, 5f, 6f), Quaternion.Euler(7f, 8f, 9f));
                host.transform.localScale = Vector3.one * 1.45f;
                var hostPosition = host.transform.position;
                var hostRotation = host.transform.rotation;
                var hostScale = host.transform.localScale;
                var animator = instance.GetComponentInChildren<Animator>(true);
                var head = FindDescendant(animator.transform, "mixamorig:Head");
                var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
                var idle = FindClip(clips, "Idle");
                var before = head.localRotation;

                idle.SampleAnimation(animator.gameObject, idle.length * 0.5f);

                Assert.That(Quaternion.Angle(before, head.localRotation), Is.GreaterThan(0.01f));
                Assert.That(host.transform.position, Is.EqualTo(hostPosition));
                Assert.That(host.transform.rotation, Is.EqualTo(hostRotation));
                Assert.That(host.transform.localScale, Is.EqualTo(hostScale));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SensenDefinition_PrefersRiggedPrefabAndRetainsTheGlbFallback()
        {
            var definition = Resources.Load<AnimalDefinition>("Animals/Sensen");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.AnimalId, Is.EqualTo("sensen"));
            Assert.That(definition.ModelRelativePath, Is.EqualTo("Models/Sensen/sensen.glb"));

            var serialized = new SerializedObject(definition);
            var modelPrefab = serialized.FindProperty("modelPrefab");
            Assert.That(modelPrefab, Is.Not.Null, "AnimalDefinition needs an optional modelPrefab field.");
            Assert.That(modelPrefab.objectReferenceValue, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
        }

        [Test]
        public void EatGameViewAcceptance_UsesTheRiggedScanPath()
        {
            var source = File.ReadAllText(GameViewAcceptancePath);

            StringAssert.Contains("if (UsesRiggedModel(mode))", source);
            StringAssert.Contains("return mode == RiggedMode || mode == EatMode;", source,
                "Eat acceptance must load the rigged prefab rather than forcing the legacy GLB fallback.");
        }

        private static AnimationClip FindClip(AnimationClip[] clips, string suffix)
        {
            var clip = clips.SingleOrDefault(value => value.name.Equals(suffix, StringComparison.Ordinal));
            Assert.That(clip, Is.Not.Null, $"Animation clip ending with '{suffix}' was not found.");
            return clip;
        }

        private static void AssertClipAnimatesBone(AnimationClip clip, string boneName)
        {
            Assert.That(AnimationUtility.GetCurveBindings(clip), Has.Some.Matches<EditorCurveBinding>(binding =>
                binding.type == typeof(Transform) &&
                binding.path.IndexOf(boneName, StringComparison.Ordinal) >= 0 &&
                (binding.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal) ||
                 binding.propertyName.StartsWith("localEulerAngles", StringComparison.Ordinal))));
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
            Assert.That(found, Is.Not.Null, $"Expected descendant '{name}'.");
            return found;
        }

        private static System.Collections.Generic.Dictionary<string, Quaternion> CaptureBonePose(
            Transform root,
            string[] paths)
        {
            return paths.ToDictionary(path => path, path => root.Find(path).localRotation);
        }

        private static float MaxPoseDelta(
            System.Collections.Generic.Dictionary<string, Quaternion> first,
            System.Collections.Generic.Dictionary<string, Quaternion> second)
        {
            return first.Keys.Max(path => Quaternion.Angle(first[path], second[path]));
        }

        private static bool HasChangingCurve(AnimationClip clip, EditorCurveBinding binding)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.keys.Length < 2)
            {
                return false;
            }

            var first = curve.keys[0].value;
            return curve.keys.Any(key => Mathf.Abs(key.value - first) > 0.00001f);
        }
    }
}
