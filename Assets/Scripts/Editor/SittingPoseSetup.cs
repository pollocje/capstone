using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools > Players > Set Up Sitting Pose
// Wires the per-character Sitting clips (Assets/Animations/player1..4@Sitting.fbx) into the player:
//  1. Imports each FBX as Humanoid with a looping, baked-in-place clip (it's a stationary pose).
//  2. Adds a "Seated" bool + "Seated" state to PlayerAnimator (Any State -> Seated, Seated -> Locomotion),
//     and stops the emote from firing while seated.
//  3. Fills PlayerEmote.sitClips / sitPlaceholder on the player prefab so each slot gets its own clip.
// VehicleEnterExit flips the Seated bool for driver/passenger riders. Safe to re-run.
public static class SittingPoseSetup
{
    const string AnimDir = "Assets/Animations";
    const string ControllerPath = "Assets/Animations/PlayerAnimator.controller";
    const string PlayerPrefabPath = "Assets/Prefabs/MainPlayer Variant.prefab";
    const string SeatedParam = "Seated";
    const string SeatedStateName = "Seated";
    const int SlotCount = 4;

    [MenuItem("Tools/Players/Set Up Sitting Pose")]
    public static void Run()
    {
        var clips = new AnimationClip[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            string path = $"{AnimDir}/player{i + 1}@Sitting.fbx";
            if (!ConfigureImporter(path)) return;
            clips[i] = LoadClip(path);
            if (clips[i] == null)
            {
                Debug.LogError($"[SittingPoseSetup] No animation clip found in {path}.");
                return;
            }
        }

        if (!ConfigureController(clips[0])) return;
        WirePrefab(clips);

        AssetDatabase.SaveAssets();
        Debug.Log("[SittingPoseSetup] Done: sitting clips imported, PlayerAnimator has a Seated state, PlayerEmote wired.");
    }

    static bool ConfigureImporter(string path)
    {
        if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
        {
            Debug.LogError($"[SittingPoseSetup] {path} not found (or not a model).");
            return false;
        }

        // Same rig setup as the emote clips: Humanoid, avatar built from this file.
        bool rigChanged = importer.animationType != ModelImporterAnimationType.Human ||
                          importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                          !importer.importAnimation;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        if (rigChanged) importer.SaveAndReimport(); // so defaultClipAnimations reflects the humanoid take

        var clipDefs = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (var clip in clipDefs)
        {
            clip.loopTime = true;
            // Bake root motion into the pose and keep the original placement: the model must stay
            // put in the seat, not drift or turn as the loop plays.
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clipDefs;
        importer.SaveAndReimport();
        return true;
    }

    static AnimationClip LoadClip(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    static bool ConfigureController(AnimationClip placeholder)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[SittingPoseSetup] {ControllerPath} not found.");
            return false;
        }

        if (!controller.parameters.Any(p => p.name == SeatedParam))
            controller.AddParameter(SeatedParam, AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;
        var locomotion = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion");
        if (locomotion == null)
        {
            Debug.LogError("[SittingPoseSetup] PlayerAnimator has no Locomotion state.");
            return false;
        }

        var seated = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == SeatedStateName)
                     ?? sm.AddState(SeatedStateName, new Vector3(350, 320, 0));
        seated.motion = placeholder;
        seated.writeDefaultValues = true;

        // Any State -> Seated (rebuilt each run so re-running doesn't stack duplicates).
        foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == seated).ToArray())
            sm.RemoveAnyStateTransition(t);
        var enter = sm.AddAnyStateTransition(seated);
        enter.AddCondition(AnimatorConditionMode.If, 0, SeatedParam);
        enter.hasExitTime = false;
        enter.duration = 0.2f;
        enter.canTransitionToSelf = false; // otherwise it restarts the state every frame the bool is on

        // Seated -> Locomotion once the bool drops.
        foreach (var t in seated.transitions.ToArray())
            seated.RemoveTransition(t);
        var exit = seated.AddTransition(locomotion);
        exit.AddCondition(AnimatorConditionMode.IfNot, 0, SeatedParam);
        exit.hasExitTime = false;
        exit.duration = 0.2f;

        // No emoting from the truck seat: Any State -> Emote also requires not seated.
        foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState != null && t.destinationState.name == "Emote"))
            if (!t.conditions.Any(c => c.parameter == SeatedParam))
                t.AddCondition(AnimatorConditionMode.IfNot, 0, SeatedParam);

        EditorUtility.SetDirty(controller);
        return true;
    }

    static void WirePrefab(AnimationClip[] clips)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var emote = root.GetComponentInChildren<PlayerEmote>(true);
            if (emote == null)
            {
                Debug.LogWarning($"[SittingPoseSetup] No PlayerEmote in {PlayerPrefabPath}; assign sitClips by hand.");
                return;
            }

            emote.sitClips = clips;
            emote.sitPlaceholder = clips[0];
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
