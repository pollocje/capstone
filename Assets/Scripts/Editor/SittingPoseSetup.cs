using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools > Players > Set Up Sitting Pose
// Wires the per-character truck sitting clips into the player:
//   Cab (driver/passenger):  Assets/Animations/player1..4@Sitting.fbx   -> "Seated" bool/state
//   Truck bed (both seats):  Assets/Animations/player1..4@TrunkSeat.fbx -> "BedSeated" bool/state
//  1. Imports each FBX as Humanoid with a looping, baked-in-place clip (they're stationary poses).
//  2. Adds the bool + state to PlayerAnimator (Any State -> pose, pose -> Locomotion),
//     and stops the emote from firing while in either pose.
//  3. Fills PlayerEmote.sitClips / trunkSitClips (+ placeholders) on the player prefab so each slot gets its own clip.
// VehicleEnterExit flips Seated for cab riders and BedSeated for truck-bed riders. Safe to re-run.
public static class SittingPoseSetup
{
    const string AnimDir = "Assets/Animations";
    const string ControllerPath = "Assets/Animations/PlayerAnimator.controller";
    const string PlayerPrefabPath = "Assets/Prefabs/MainPlayer Variant.prefab";
    const int SlotCount = 4;

    // (clip file suffix, animator bool, animator state, graph position)
    static readonly (string clip, string param, string state, Vector3 pos)[] Poses =
    {
        ("Sitting",   "Seated",    "Seated",    new Vector3(350, 320, 0)),
        ("TrunkSeat", "BedSeated", "BedSeated", new Vector3(350, 400, 0)),
    };

    [MenuItem("Tools/Players/Set Up Sitting Pose")]
    public static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[SittingPoseSetup] {ControllerPath} not found.");
            return;
        }

        var clipsPerPose = new AnimationClip[Poses.Length][];
        for (int p = 0; p < Poses.Length; p++)
        {
            clipsPerPose[p] = LoadPoseClips(Poses[p].clip);
            if (clipsPerPose[p] == null) return;
            if (!ConfigureState(controller, clipsPerPose[p][0], Poses[p].param, Poses[p].state, Poses[p].pos)) return;
        }
        EditorUtility.SetDirty(controller);

        WirePrefab(clipsPerPose[0], clipsPerPose[1]);

        AssetDatabase.SaveAssets();
        Debug.Log("[SittingPoseSetup] Done: Sitting + TrunkSeat clips imported, PlayerAnimator has Seated + BedSeated states, PlayerEmote wired.");
    }

    static AnimationClip[] LoadPoseClips(string suffix)
    {
        var clips = new AnimationClip[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            string path = $"{AnimDir}/player{i + 1}@{suffix}.fbx";
            if (!ConfigureImporter(path)) return null;
            clips[i] = LoadClip(path);
            if (clips[i] == null)
            {
                Debug.LogError($"[SittingPoseSetup] No animation clip found in {path}.");
                return null;
            }
        }
        return clips;
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

    static bool ConfigureState(AnimatorController controller, AnimationClip placeholder, string param, string stateName, Vector3 graphPos)
    {
        if (!controller.parameters.Any(p => p.name == param))
            controller.AddParameter(param, AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;
        var locomotion = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion");
        if (locomotion == null)
        {
            Debug.LogError("[SittingPoseSetup] PlayerAnimator has no Locomotion state.");
            return false;
        }

        var seated = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName)
                     ?? sm.AddState(stateName, graphPos);
        seated.motion = placeholder;
        seated.writeDefaultValues = true;

        // Any State -> pose (rebuilt each run so re-running doesn't stack duplicates).
        foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == seated).ToArray())
            sm.RemoveAnyStateTransition(t);
        var enter = sm.AddAnyStateTransition(seated);
        enter.AddCondition(AnimatorConditionMode.If, 0, param);
        enter.hasExitTime = false;
        enter.duration = 0.2f;
        enter.canTransitionToSelf = false; // otherwise it restarts the state every frame the bool is on

        // pose -> Locomotion once the bool drops.
        foreach (var t in seated.transitions.ToArray())
            seated.RemoveTransition(t);
        var exit = seated.AddTransition(locomotion);
        exit.AddCondition(AnimatorConditionMode.IfNot, 0, param);
        exit.hasExitTime = false;
        exit.duration = 0.2f;

        // No emoting from a truck seat: Any State -> Emote also requires not in this pose.
        foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState != null && t.destinationState.name == "Emote"))
            if (!t.conditions.Any(c => c.parameter == param))
                t.AddCondition(AnimatorConditionMode.IfNot, 0, param);

        return true;
    }

    static void WirePrefab(AnimationClip[] sitClips, AnimationClip[] trunkClips)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var emote = root.GetComponentInChildren<PlayerEmote>(true);
            if (emote == null)
            {
                Debug.LogWarning($"[SittingPoseSetup] No PlayerEmote in {PlayerPrefabPath}; assign sitClips / trunkSitClips by hand.");
                return;
            }

            emote.sitClips = sitClips;
            emote.sitPlaceholder = sitClips[0];
            emote.trunkSitClips = trunkClips;
            emote.trunkSitPlaceholder = trunkClips[0];
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
