using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary tool: builds a shared AnimatorController (Idle/Walk/Run blend tree + Jump)
    // out of the retargeted Mixamo clips, for use by any animal configured as Humanoid.
    public static class AnimalLocomotionControllerBuilder
    {
        private const string OutputPath = "Assets/AnimalBattleRoyale/Resources/Animation/AnimalLocomotion.controller";
        private const string AnimFolder = "Assets/AnimalBattleRoyale/Art/SharedAnimations/";

        [MenuItem("AnimalBattleRoyale/Debug/Build Animal Locomotion Controller")]
        public static void Build()
        {
            AnimationClip idle = FindClip("Mixamo_Idle.fbx");
            AnimationClip walk = FindClip("Mixamo_Walk.fbx");
            AnimationClip run = FindClip("Mixamo_Run.fbx");
            AnimationClip jump = FindClip("Mixamo_Jump.fbx");
            if (idle == null || walk == null || run == null || jump == null)
            {
                Debug.LogError("[LocomotionBuilder] One or more Mixamo clips could not be found. " +
                    "Run 'Configure Mixamo Clips' first.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
                AssetDatabase.DeleteAsset(OutputPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("JumpTrigger", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine root = controller.layers[0].stateMachine;

            BlendTree blendTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, 1f);
            blendTree.AddChild(run, 2f);

            AnimatorState locomotionState = root.AddState("Locomotion");
            locomotionState.motion = blendTree;
            root.defaultState = locomotionState;

            AnimatorState jumpState = root.AddState("Jump");
            jumpState.motion = jump;
            // The user reports the jump clip's launch/land halves play in the wrong order
            // (looks like it lands immediately, then jumps) — playing it backward, starting
            // from the end, is the quick way to test whether the source clip is just
            // authored/exported in reverse.
            jumpState.speed = -1f;

            AnimatorStateTransition toJump = locomotionState.AddTransition(jumpState);
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "JumpTrigger");
            toJump.hasExitTime = false;
            toJump.duration = 0.02f;
            toJump.offset = 1f;
            toJump.canTransitionToSelf = false;

            AnimatorStateTransition toLocomotion = jumpState.AddTransition(locomotionState);
            toLocomotion.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            toLocomotion.hasExitTime = true;
            toLocomotion.exitTime = 0.5f;
            toLocomotion.duration = 0.15f;

            AssetDatabase.SaveAssets();
            Debug.Log($"[LocomotionBuilder] Controller built at {OutputPath}.");
        }

        private static AnimationClip FindClip(string fbxFileName)
        {
            string path = AnimFolder + fbxFileName;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__")) return clip;
            }
            return null;
        }
    }
}
