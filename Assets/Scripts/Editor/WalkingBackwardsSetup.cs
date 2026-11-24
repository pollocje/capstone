using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools > Players > Set Up Walking Backwards
//  1. Imports player1@Walking Backwards.fbx with the same rig and loop settings as player1@Walking.
//  2. Adds it to the Locomotion blend tree at Speed -4. PlayerAnimationDriver makes Speed negative
//     when the player moves backwards, so forward walking/running is unchanged.
// Safe to re-run.
public static class WalkingBackwardsSetup
{
    const string ControllerPath = "Assets/Animations/PlayerAnimator.controller";
    const string WalkingPath = "Assets/Animations/player1@Walking.fbx";
    const string BackwardsPath = "Assets/Animations/player1@Walking Backwards.fbx";
    const float BackwardsThreshold = -4f; // same speed as Walking (4), just backwards

    [MenuItem("Tools/Players/Set Up Walking Backwards")]
    public static void Run()
    {
        if (!ConfigureImporter()) return;

        var clip = AssetDatabase.LoadAllAssetsAtPath(BackwardsPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null)
        {
            Debug.LogError($"[WalkingBackwardsSetup] No animation clip in {BackwardsPath}.");
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var state = controller.layers[0].stateMachine.states
            .Select(s => s.state)
            .FirstOrDefault(s => s.name == "Locomotion");
        if (state == null || state.motion is not BlendTree tree)
        {
            Debug.LogError("[WalkingBackwardsSetup] No Locomotion blend tree in the base layer.");
            return;
        }

        // Replace any old entry, then keep the children in threshold order (1D blend trees need that)
        var children = tree.children.Where(c => c.motion != clip).ToList();
        children.Add(new ChildMotion { motion = clip, threshold = BackwardsThreshold, timeScale = 1f });
        var sorted = children.OrderBy(c => c.threshold).ToArray();

        // Set the range before the children: changing min/max rescales the thresholds already there
        tree.useAutomaticThresholds = false;
        tree.minThreshold = sorted.First().threshold;
        tree.maxThreshold = sorted.Last().threshold;
        tree.children = sorted;

        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[WalkingBackwardsSetup] Done: '{clip.name}' added to Locomotion at Speed {BackwardsThreshold}.");
    }

    // Copy the rig and loop settings from the forward walk so both play the same way
    static bool ConfigureImporter()
    {
        if (AssetImporter.GetAtPath(WalkingPath) is not ModelImporter walking ||
            AssetImporter.GetAtPath(BackwardsPath) is not ModelImporter backwards)
        {
            Debug.LogError($"[WalkingBackwardsSetup] {WalkingPath} or {BackwardsPath} not found.");
            return false;
        }

        backwards.animationType = walking.animationType;
        backwards.avatarSetup = walking.avatarSetup;
        backwards.importAnimation = true;
        backwards.SaveAndReimport(); // so defaultClipAnimations has the humanoid take

        var source = walking.clipAnimations.Length > 0 ? walking.clipAnimations[0] : walking.defaultClipAnimations[0];
        var clips = backwards.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = "Walking Backwards";
            clip.loopTime = source.loopTime;
            clip.loopPose = source.loopPose;
            clip.lockRootRotation = source.lockRootRotation;
            clip.lockRootHeightY = source.lockRootHeightY;
            clip.lockRootPositionXZ = source.lockRootPositionXZ;
            clip.keepOriginalOrientation = source.keepOriginalOrientation;
            clip.keepOriginalPositionY = source.keepOriginalPositionY;
            clip.keepOriginalPositionXZ = source.keepOriginalPositionXZ;
            clip.heightFromFeet = source.heightFromFeet;
            clip.rotationOffset = source.rotationOffset;
            clip.cycleOffset = source.cycleOffset;
            clip.mirror = false;
        }
        backwards.clipAnimations = clips;
        backwards.SaveAndReimport();
        return true;
    }
}
