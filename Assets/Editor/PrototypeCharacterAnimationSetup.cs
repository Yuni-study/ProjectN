using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
internal static class PrototypeCharacterAnimationSetup
{
    private const string ModelPath = "Assets/Resources/Art/Quaternius/UltimateModularWomen/SciFi.fbx";
    private const string ControllerPath = "Assets/Resources/Art/Quaternius/UltimateModularWomen/CharacterCombat.controller";
    private const string MaskPath = "Assets/Resources/Art/Quaternius/UltimateModularWomen/CharacterUpperBody.mask";
    private const string VersionKey = "ProjectN.CharacterCombatControllerVersion";
    private const int ControllerVersion = 5;
    private static bool isBuilding;

    static PrototypeCharacterAnimationSetup()
    {
        EditorApplication.delayCall += EnsureController;
    }

    internal static void EnsureController()
    {
        RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        AnimatorController existingController = existing as AnimatorController;
        bool controllerIsCurrent = existingController != null
            && existingController.layers.Length > 1
            && existingController.layers[0].stateMachine != null
            && existingController.layers[0].stateMachine.defaultState != null
            && existingController.layers[1].stateMachine != null
            && existingController.layers[1].stateMachine.defaultState != null
            && existingController.layers[1].avatarMask != null
            && EditorPrefs.GetInt(VersionKey) >= ControllerVersion;
        if (isBuilding || !System.IO.File.Exists(ModelPath) || controllerIsCurrent)
            return;

        isBuilding = true;
        if (existing != null) AssetDatabase.DeleteAsset(ControllerPath);
        if (AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath) != null) AssetDatabase.DeleteAsset(MaskPath);
        ConfigureModelImporter();
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        if (clips.Length == 0)
        {
            isBuilding = false;
            return;
        }

        AnimationClip FindClip(string suffix) => clips.FirstOrDefault(clip => clip.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        AnimationClip idle = FindClip("Idle_Neutral");
        AnimationClip walk = FindClip("Walk");
        AnimationClip run = FindClip("Run");
        AnimationClip carry = FindClip("Idle_Gun");
        AnimationClip aim = FindClip("Idle_Gun_Pointing");
        AnimationClip fire = FindClip("Idle_Gun_Shoot") ?? FindClip("Gun_Shoot");
        if (idle == null || walk == null || run == null || carry == null || aim == null || fire == null)
        {
            Debug.LogError("Character animation setup failed: required Quaternius combat clips are missing.");
            isBuilding = false;
            return;
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (controller == null || controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
        {
            Debug.LogError("Character animation setup failed: Unity could not create the base Animator layer.");
            isBuilding = false;
            return;
        }
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Aim", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = machine.AddState("Locomotion");
        machine.defaultState = locomotion;
        locomotion.motion = CreateLocomotionTree(controller, idle, walk, run);
        if (!CreateWeaponPoseLayer(controller, carry, aim, fire))
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AssetDatabase.DeleteAsset(MaskPath);
            isBuilding = false;
            return;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, ControllerVersion);
        Debug.Log("Generated prototype armed locomotion and shoulder-aim animation controller.");
        isBuilding = false;
    }

    private static void ConfigureModelImporter()
    {
        string[] modelPaths =
        {
            ModelPath,
            "Assets/Resources/Art/Quaternius/UltimateModularWomen/Soldier.fbx",
            "Assets/Resources/Art/Quaternius/UltimateModularWomen/Suit.fbx"
        };
        foreach (string path in modelPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;
            bool changed = importer.animationType != ModelImporterAnimationType.Generic
                || importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar
                || importer.optimizeGameObjects
                || !importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.optimizeGameObjects = false;
            importer.importAnimation = true;
            ModelImporterClipAnimation[] animations = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation animation in animations)
            {
                bool loop = animation.name.EndsWith("Idle", StringComparison.OrdinalIgnoreCase)
                    || animation.name.EndsWith("Idle_Gun", StringComparison.OrdinalIgnoreCase)
                    || animation.name.EndsWith("Idle_Neutral", StringComparison.OrdinalIgnoreCase)
                    || animation.name.EndsWith("Idle_Gun_Pointing", StringComparison.OrdinalIgnoreCase)
                    || animation.name.EndsWith("Walk", StringComparison.OrdinalIgnoreCase)
                    || animation.name.EndsWith("Run", StringComparison.OrdinalIgnoreCase);
                if (animation.loopTime != loop)
                {
                    animation.loopTime = loop;
                    changed = true;
                }
            }
            if (changed)
            {
                importer.clipAnimations = animations;
                importer.SaveAndReimport();
            }
        }
    }

    private static bool CreateWeaponPoseLayer(AnimatorController controller, AnimationClip carry,
        AnimationClip aim, AnimationClip fire)
    {
        AvatarMask mask = new AvatarMask();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Transform chest = model != null ? FindNamedTransform(model.transform, "Chest") : null;
        if (chest == null)
        {
            Debug.LogError("Character animation setup failed: could not find the chest bone for the upper-body mask.");
            return false;
        }
        mask.AddTransformPath(model.transform, true);
        string chestPath = AnimationUtility.CalculateTransformPath(chest, model.transform);
        for (int i = 0; i < mask.transformCount; i++)
        {
            string path = mask.GetTransformPath(i);
            mask.SetTransformActive(i, path == chestPath || path.StartsWith(chestPath + "/", StringComparison.Ordinal));
        }
        AssetDatabase.CreateAsset(mask, MaskPath);

        controller.AddLayer("Weapon Pose");
        AnimatorControllerLayer[] layers = controller.layers;
        layers[1].defaultWeight = 1f;
        layers[1].avatarMask = mask;
        controller.layers = layers;
        AnimatorStateMachine machine = layers[1].stateMachine;
        AnimatorState carryState = machine.AddState("Carry Weapon");
        carryState.motion = carry;
        machine.defaultState = carryState;
        AnimatorState aimState = machine.AddState("Shoulder Aim");
        aimState.motion = aim;
        AnimatorState fireState = machine.AddState("Fire");
        fireState.motion = fire;
        AddTransition(carryState, aimState, false, AnimatorConditionMode.If, 0f, "Aim");
        AddTransition(aimState, carryState, false, AnimatorConditionMode.IfNot, 0f, "Aim");
        AddTransition(carryState, fireState, false, AnimatorConditionMode.If, 0f, "Fire");
        AddTransition(aimState, fireState, false, AnimatorConditionMode.If, 0f, "Fire");
        AddTransition(fireState, aimState, true, AnimatorConditionMode.If, 0f, "Aim");
        AddTransition(fireState, carryState, true, AnimatorConditionMode.IfNot, 0f, "Aim");
        return true;
    }

    private static Transform FindNamedTransform(Transform parent, string targetName)
    {
        if (parent.name == targetName || parent.name.EndsWith("|" + targetName, StringComparison.Ordinal)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindNamedTransform(parent.GetChild(i), targetName);
            if (result != null) return result;
        }
        return null;
    }

    private static BlendTree CreateLocomotionTree(AnimatorController controller, AnimationClip idle,
        AnimationClip walk, AnimationClip run)
    {
        BlendTree tree = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed" };
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 0.45f);
        tree.AddChild(run, 1f);
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, bool exitTime,
        AnimatorConditionMode mode, float threshold, string parameter,
        AnimatorConditionMode? secondMode = null, float secondThreshold = 0f, string secondParameter = null)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = exitTime;
        transition.exitTime = 0.9f;
        transition.duration = 0.08f;
        transition.AddCondition(mode, threshold, parameter);
        if (secondMode.HasValue) transition.AddCondition(secondMode.Value, secondThreshold, secondParameter);
    }
}

internal sealed class PrototypeCharacterModelPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (importedAssets.Any(path => path.StartsWith(
                "Assets/Resources/Art/Quaternius/UltimateModularWomen/", StringComparison.Ordinal)
            && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)))
        {
            EditorApplication.delayCall += PrototypeCharacterAnimationSetup.EnsureController;
        }
    }
}
