"""Creates the playable Tiger asset for Animal Battle Royale.

Run with Blender 4.x:
  Blender --background --python Tools/Blender/create_tiger.py -- <output-directory>

The body is a single organic mesh (metaball surface) skinned with automatic
weights, so shoulders, haunches and neck flow into each other like a real cat.
Colors stay in the approved saturated cartoon palette; stripes are shrinkwrapped
onto the fur so they follow the anatomy.
"""

import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from organic import (  # noqa: E402
    OrganicBody, cartoon_eye, claw, detail_sphere, material,
    oriented_detail_sphere, tapered_limb,
)


def output_directory():
    args = sys.argv
    if "--" in args:
        return os.path.abspath(args[args.index("--") + 1])
    return os.path.abspath("TigerOutput")


OUT = output_directory()
FBX_PATH = os.path.join(OUT, "Tiger.fbx")
BLEND_PATH = os.path.join(OUT, "Tiger_Source.blend")


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.actions, bpy.data.meshes, bpy.data.armatures):
        for item in list(collection):
            if item.users == 0:
                collection.remove(item)


ORANGE = material("Tiger_Orange", (0.95, 0.29, 0.035), roughness=0.68)
CREAM = material("Tiger_Cream", (0.96, 0.78, 0.47), roughness=0.72)
BLACK = material("Tiger_Black", (0.025, 0.012, 0.008), roughness=0.55)
WHITE = material("Tiger_EyeWhite", (0.98, 0.92, 0.74), roughness=0.25)
AMBER = material("Tiger_Amber", (1.0, 0.49, 0.045), roughness=0.2)
PUPIL = material("Tiger_Pupil", (0.01, 0.008, 0.006), roughness=0.18)
SPARK = material("Tiger_Spark", (1.0, 1.0, 0.96), roughness=0.1)
PINK = material("Tiger_Nose", (0.36, 0.055, 0.038), roughness=0.35)
INNER_EAR = material("Tiger_InnerEar", (0.82, 0.55, 0.38), roughness=0.7)


def create_rig():
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    rig = bpy.context.object
    rig.name = "Tiger_Rig"
    rig.data.name = "Tiger_RigData"
    root = rig.data.edit_bones[0]
    root.name = "Root"
    root.head = (0, 0, 0)
    root.tail = (0, 0.65, 0)

    bones = {"Root": root}

    def bone(name, head, tail, parent="Root"):
        item = rig.data.edit_bones.new(name)
        item.head = head
        item.tail = tail
        item.parent = bones[parent]
        bones[name] = item
        return item

    bone("Spine", (0, 0.62, -0.28), (0, 0.85, 0.35))
    bone("Chest", (0, 0.84, 0.28), (0, 1.04, 0.62), "Spine")
    bone("Neck", (0, 1.02, 0.58), (0, 1.16, 0.84), "Chest")
    bone("Head", (0, 1.14, 0.83), (0, 1.30, 1.15), "Neck")
    bone("Jaw", (0, 1.04, 1.14), (0, 0.94, 1.38), "Head")
    bone("Ear_L", (-0.34, 1.34, 0.88), (-0.52, 1.53, 0.9), "Head")
    bone("Ear_R", (0.34, 1.34, 0.88), (0.52, 1.53, 0.9), "Head")
    leg_bones = [
        ("FL", (-0.36, 0.62, 0.45), (-0.39, 0.36, 0.50), (-0.40, 0.12, 0.58), "Chest"),
        ("FR", (0.36, 0.62, 0.45), (0.39, 0.36, 0.50), (0.40, 0.12, 0.58), "Chest"),
        ("BL", (-0.36, 0.62, -0.54), (-0.39, 0.36, -0.58), (-0.40, 0.12, -0.48), "Spine"),
        ("BR", (0.36, 0.62, -0.54), (0.39, 0.36, -0.58), (0.40, 0.12, -0.48), "Spine"),
    ]
    for label, hip, knee, ankle, parent in leg_bones:
        bone("Leg_" + label, hip, knee, parent)
        bone("LowerLeg_" + label, knee, ankle, "Leg_" + label)
        bone("Paw_" + label, ankle, (ankle[0], 0.10, ankle[2] + 0.28), "LowerLeg_" + label)
        bone("Claw_" + label, (ankle[0], 0.10, ankle[2] + 0.18), (ankle[0], 0.07, ankle[2] + 0.42), "Paw_" + label)
    bone("Tail_1", (0, 0.82, -0.87), (0.12, 0.92, -1.25), "Spine")
    bone("Tail_2", (0.12, 0.92, -1.24), (0.32, 1.02, -1.56), "Tail_1")
    bone("Tail_3", (0.32, 1.02, -1.55), (0.46, 1.00, -1.80), "Tail_2")
    bpy.ops.object.mode_set(mode="OBJECT")
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    return rig


def build_tiger(rig):
    body = OrganicBody("TigerBody", resolution=0.045)

    # Hindquarters: wide haunches that read as powerful cat legs.
    body.ball((0, 0.80, -0.52), 0.36)
    body.ball((-0.24, 0.72, -0.56), 0.28)
    body.ball((0.24, 0.72, -0.56), 0.28)
    # Barrel ribcage sloping up into a deep chest — the classic feline topline.
    body.ball((0, 0.76, -0.18), 0.40)
    body.ball((-0.14, 0.76, -0.15), 0.30)
    body.ball((0.14, 0.76, -0.15), 0.30)
    body.ball((0, 0.84, 0.16), 0.40)
    body.ball((0, 0.90, 0.34), 0.38)
    body.ball((-0.18, 0.90, 0.32), 0.26)
    body.ball((0.18, 0.90, 0.32), 0.26)
    # Neck ruff blending into the skull.
    body.chain((0, 1.00, 0.55), (0, 1.16, 0.80), 0.24, 0.20, 4)
    body.ball((0, 1.28, 0.95), 0.25)
    body.ball((0, 1.40, 1.04), 0.15)
    body.ball((-0.14, 1.24, 1.06), 0.14)
    body.ball((0.14, 1.24, 1.06), 0.14)
    # Muzzle and jaw.
    body.chain((0, 1.20, 1.20), (0, 1.17, 1.40), 0.125, 0.10, 3)
    body.ball((0, 1.08, 1.22), 0.11)

    # Legs flow out of the torso; tapered chains give knees and slim ankles.
    legs = [
        ("FL", (-0.34, 0.72, 0.44), (-0.38, 0.38, 0.50), (-0.39, 0.16, 0.56)),
        ("FR", (0.34, 0.72, 0.44), (0.38, 0.38, 0.50), (0.39, 0.16, 0.56)),
        ("BL", (-0.34, 0.70, -0.54), (-0.38, 0.38, -0.58), (-0.39, 0.16, -0.50)),
        ("BR", (0.34, 0.70, -0.54), (0.38, 0.38, -0.58), (0.39, 0.16, -0.50)),
    ]
    for label, hip, knee, ankle in legs:
        body.chain(hip, knee, 0.155, 0.115, 4)
        body.chain(knee, ankle, 0.115, 0.10, 3)
        paw_center = (ankle[0], 0.11, ankle[2] + 0.14)
        body.ball(paw_center, 0.115)
        # Toes overlap the paw ball so they fuse into one island; separated
        # islands confuse the automatic weights and drift during animations.
        for toe_index in (-1, 0, 1):
            body.ball((ankle[0] + toe_index * 0.062, 0.10, ankle[2] + 0.235), 0.062)

    # Tail with a slight S-curve, thinning to a dark tip. Dense chains keep
    # the surface tubular instead of turning into a string of beads.
    body.chain((0, 0.86, -0.80), (0.12, 0.95, -1.24), 0.09, 0.075, 10)
    body.chain((0.12, 0.95, -1.24), (0.32, 1.03, -1.55), 0.075, 0.06, 8)
    body.chain((0.32, 1.03, -1.55), (0.46, 1.00, -1.78), 0.06, 0.05, 6)

    # Stripes are painted straight onto the body polygons, so they always
    # follow the anatomy. Flank stripes sweep backwards like real tiger fur.
    body_bands = (-0.62, -0.38, -0.14, 0.10, 0.34)

    def body_stripe(c):
        if c.y < 0.70 or not (-0.80 < c.z < 0.54):
            return False
        swept_z = c.z + 0.20 * abs(c.x)
        return any(abs(swept_z - band) < 0.045 for band in body_bands)

    def head_stripe(c):
        return (c.y > 1.42 and 0.80 < c.z < 1.14 and abs(c.x) < 0.22
                and any(abs(c.z - band) < 0.028 for band in (0.86, 0.96, 1.06)))

    def tail_ring(c):
        return c.z < -0.94 and c.y > 0.72 and (
            abs(c.z + 1.12) < 0.05 or abs(c.z + 1.38) < 0.05)

    mesh = body.finish("Tiger_Body", rig, ORANGE, regions=(
        (BLACK, lambda c: c.z < -1.62 and c.y > 0.7),                      # tail tip
        (BLACK, tail_ring),
        (CREAM, lambda c: c.z > 1.22 and c.y < 1.32),                      # muzzle
        (CREAM, lambda c: c.y < 0.20),                                     # paws
        (CREAM, lambda c: c.y < 0.62 and -0.55 < c.z < 0.42 and abs(c.x) < 0.26),  # belly
        (CREAM, lambda c: c.y < 1.04 and c.z > 0.46 and abs(c.x) < 0.26),  # chest bib
        (BLACK, body_stripe),
        (BLACK, head_stripe),
    ))

    # Ears: fur-colored shells with a lighter inner ear, planted on the skull.
    for side, label in ((-1, "L"), (1, "R")):
        oriented_detail_sphere("Ear_" + label, (side * 0.175, 1.43, 0.90),
                               (0.115, 0.125, 0.06), (0.35, side * 0.45, 0),
                               ORANGE, rig, "Ear_" + label)
        oriented_detail_sphere("EarInner_" + label, (side * 0.17, 1.435, 0.932),
                               (0.07, 0.085, 0.045), (0.35, side * 0.45, 0),
                               INNER_EAR, rig, "Ear_" + label)

    # Face: layered cartoon eyes with a highlight, nose leather and whiskers.
    for side in (-1, 1):
        cartoon_eye("Eye_" + ("L" if side < 0 else "R"),
                    (side * 0.115, 1.345, 1.16), (side * 0.20, 0.05, 1.0),
                    0.078, rig, "Head", WHITE, AMBER, PUPIL, SPARK)
    detail_sphere("Nose", (0, 1.235, 1.47), (0.07, 0.048, 0.042), PINK, rig, "Head")
    for side in (-1, 1):
        for row, (dy, dz) in enumerate(((0.02, 0.0), (-0.015, 0.01), (-0.05, 0.0))):
            tapered_limb("Whisker_%d_%d" % (side, row),
                         (side * 0.10, 1.17 + dy, 1.42 + dz),
                         (side * 0.34, 1.13 + dy * 2.4, 1.50 + dz), 0.006, 0.001,
                         BLACK, rig, "Head", vertices=6)

    # Claws poke out of the toes.
    for label, _, _, ankle in legs:
        for toe_index in (-1, 0, 1):
            claw("Claw_%s_%d" % (label, toe_index),
                 (ankle[0] + toe_index * 0.075, 0.075, ankle[2] + 0.345),
                 0.085, 0.024, BLACK, rig, "Claw_" + label)

    return mesh


def reset_pose(rig):
    for bone in rig.pose.bones:
        bone.location = (0, 0, 0)
        bone.rotation_euler = (0, 0, 0)
        bone.scale = (1, 1, 1)


def key_pose(rig, frame, rotations=None, root_location=None):
    reset_pose(rig)
    rotations = rotations or {}
    for name, rotation in rotations.items():
        rig.pose.bones[name].rotation_euler = tuple(math.radians(value) for value in rotation)
    if root_location is not None:
        rig.pose.bones["Root"].location = root_location
    for bone in rig.pose.bones:
        bone.keyframe_insert(data_path="rotation_euler", frame=frame)
        if bone.name == "Root":
            bone.keyframe_insert(data_path="location", frame=frame)


def make_action(rig, name, end_frame, poses):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    for frame, rotations, root_location in poses:
        key_pose(rig, frame, rotations, root_location)
    action.frame_range = (1, end_frame)


def create_animations(rig):
    make_action(rig, "Tiger_Idle", 36, [
        (1, {"Spine": (0, 0, -2), "Head": (2, 0, 0), "Tail_1": (3, 0, 7), "Tail_2": (1, 0, 9)}, None),
        (18, {"Spine": (0, 0, 2), "Head": (-2, 0, 0), "Tail_1": (-3, 0, -7), "Tail_2": (-1, 0, -9)}, None),
        (36, {"Spine": (0, 0, -2), "Head": (2, 0, 0), "Tail_1": (3, 0, 7), "Tail_2": (1, 0, 9)}, None),
    ])
    walk_a = {"Leg_FL": (24,0,0), "LowerLeg_FL": (-16,0,0), "Paw_FL": (8,0,0),
              "Leg_BR": (24,0,0), "LowerLeg_BR": (-16,0,0), "Paw_BR": (8,0,0),
              "Leg_FR": (-24,0,0), "LowerLeg_FR": (18,0,0), "Paw_FR": (-8,0,0),
              "Leg_BL": (-24,0,0), "LowerLeg_BL": (18,0,0), "Paw_BL": (-8,0,0),
              "Tail_1": (0,0,8), "Tail_2": (0,0,11), "Head": (-3,0,0)}
    walk_b = {name: tuple(-value for value in rotation) for name, rotation in walk_a.items()}
    make_action(rig, "Tiger_Walk", 30, [(1, walk_a, None), (15, walk_b, None), (30, walk_a, None)])
    run_a = {name: tuple(value * 1.55 for value in rotation) for name, rotation in walk_a.items()}
    run_b = {name: tuple(value * 1.55 for value in rotation) for name, rotation in walk_b.items()}
    make_action(rig, "Tiger_Run", 20, [(1, run_a, None), (10, run_b, None), (20, run_a, None)])
    make_action(rig, "Tiger_Claw", 24, [
        (1, {"Leg_FL": (0, 0, 0), "Leg_FR": (0, 0, 0), "Spine": (0, 0, 0)}, None),
        (8, {"Leg_FL": (42,0,0), "Leg_FR": (42,0,0), "LowerLeg_FL": (-58,0,0), "LowerLeg_FR": (-58,0,0), "Paw_FL": (24,0,0), "Paw_FR": (24,0,0), "Spine": (-10,0,0)}, None),
        (14, {"Leg_FL": (-68,0,0), "Leg_FR": (-68,0,0), "LowerLeg_FL": (22,0,0), "LowerLeg_FR": (22,0,0), "Paw_FL": (-20,0,0), "Paw_FR": (-20,0,0), "Claw_FL": (-42,0,0), "Claw_FR": (-42,0,0), "Spine": (18,0,0), "Head": (-8,0,0)}, (0,.08,.18)),
        (24, {"Leg_FL": (0, 0, 0), "Leg_FR": (0, 0, 0), "Spine": (0, 0, 0)}, None),
    ])
    make_action(rig, "Tiger_Pounce", 28, [
        (1, {"Leg_FL": (24,0,0), "Leg_FR": (24,0,0), "LowerLeg_FL": (-38,0,0), "LowerLeg_FR": (-38,0,0), "Leg_BL": (-24,0,0), "Leg_BR": (-24,0,0), "LowerLeg_BL": (42,0,0), "LowerLeg_BR": (42,0,0)}, (0,0,0)),
        (10, {"Spine": (-14,0,0), "Head": (12,0,0), "Leg_FL": (-42,0,0), "Leg_FR": (-42,0,0), "LowerLeg_FL": (18,0,0), "LowerLeg_FR": (18,0,0), "Claw_FL": (-28,0,0), "Claw_FR": (-28,0,0)}, (0,.22,.32)),
        (18, {"Spine": (9,0,0), "Leg_BL": (34,0,0), "Leg_BR": (34,0,0), "LowerLeg_BL": (-28,0,0), "LowerLeg_BR": (-28,0,0), "Leg_FL": (-22,0,0), "Leg_FR": (-22,0,0)}, (0,.02,.72)),
        (28, {}, (0, 0, 0)),
    ])
    make_action(rig, "Tiger_Roar", 32, [
        (1, {"Head": (0, 0, 0), "Jaw": (0, 0, 0), "Tail_1": (0, 0, 0)}, None),
        (10, {"Head": (24, 0, 0), "Jaw": (-35, 0, 0), "Spine": (-8, 0, 0)}, None),
        (22, {"Head": (24, 0, 0), "Jaw": (-35, 0, 0), "Spine": (-8, 0, 0)}, None),
        (32, {}, None),
    ])
    reset_pose(rig)
    rig.animation_data.action = bpy.data.actions["Tiger_Idle"]


def export(rig):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"ARMATURE", "MESH"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        mesh_smooth_type="FACE",
        apply_scale_options="FBX_SCALE_UNITS",
    )
    print("Created", FBX_PATH)
    print("Created", BLEND_PATH)


if __name__ == "__main__":
    clean_scene()
    tiger_rig = create_rig()
    build_tiger(tiger_rig)
    create_animations(tiger_rig)
    export(tiger_rig)
