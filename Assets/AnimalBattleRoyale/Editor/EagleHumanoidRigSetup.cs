using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Eagle's rig doesn't follow the Tripo biped naming convention shared by Tiger/Ant/
    // Monkey/Cow (see HumanoidRigSetup) — it's auto-rigged with generic "tripo::"/"bone_N"
    // names. EagleBoneIdentityProbe measured each candidate bone's position to tell wings
    // from legs: the two "tripo::0_*_Limb" chains span from hip height down to the ground
    // (legs), while the "bone_5.."/"bone_12.." chains start near head height and fold down to
    // about mid-body without reaching the ground (wings) — matching the reference screenshot
    // where the folded wings sit above the visibly separate clawed feet.
    public static class EagleHumanoidRigSetup
    {
        private static readonly Dictionary<string, string> EagleBoneMap = new Dictionary<string, string>
        {
            { "Hips", "tripo::Spine_0" },
            { "Spine", "tripo::Spine_1" },
            { "Neck", "tripo::Head_0" },
            { "Head", "tripo::Head_1" },
            { "LeftUpperArm", "bone_5" },
            { "LeftLowerArm", "bone_6" },
            { "LeftHand", "bone_7" },
            { "RightUpperArm", "bone_12" },
            { "RightLowerArm", "bone_13" },
            { "RightHand", "bone_14" },
            { "LeftUpperLeg", "tripo::0_Left_Limb_0" },
            { "LeftLowerLeg", "tripo::0_Left_Limb_1" },
            { "LeftFoot", "tripo::0_Left_Limb_2" },
            { "LeftToes", "tripo::0_Left_Limb_3" },
            { "RightUpperLeg", "tripo::0_Right_Limb_0" },
            { "RightLowerLeg", "tripo::0_Right_Limb_1" },
            { "RightFoot", "tripo::0_Right_Limb_2" },
            { "RightToes", "tripo::0_Right_Limb_3" },
        };

        private static readonly string[] EagleRequiredBones =
        {
            "tripo::Spine_0", "tripo::Spine_1", "tripo::Head_1",
            "bone_5", "bone_6", "bone_7", "bone_12", "bone_13", "bone_14",
            "tripo::0_Left_Limb_0", "tripo::0_Left_Limb_1", "tripo::0_Left_Limb_2",
            "tripo::0_Right_Limb_0", "tripo::0_Right_Limb_1", "tripo::0_Right_Limb_2",
        };

        [MenuItem("AnimalBattleRoyale/Debug/Configure Eagle As Humanoid (Experimental)")]
        public static void ConfigureEagle() => HumanoidRigSetup.Configure("Eagle",
            "Assets/AnimalBattleRoyale/Art/Characters/Eagle/Models/Eagle3D_Rigged.fbx",
            EagleBoneMap, EagleRequiredBones);
    }
}
