"""Builds the cartoon Monkey FBX, Generic rig and gameplay animation clips.

The body is one organic metaball mesh with automatic weights (see organic.py):
a pear-shaped torso, long tapered arms, a light face mask and a curled tail,
all in the original saturated palette.
"""
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from organic import OrganicBody, cartoon_eye, detail_sphere, material, oriented_detail_sphere  # noqa: E402


OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1]) if "--" in sys.argv else os.path.abspath("MonkeyOutput")


def clean():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def mat(name, color):
    return material(name, color, roughness=0.65)


def rig():
    bpy.ops.object.armature_add(enter_editmode=True)
    obj = bpy.context.object
    obj.name = "Monkey_Rig"
    obj.data.name = "Monkey_RigData"
    bones = {"Root": obj.data.edit_bones[0]}
    bones["Root"].name, bones["Root"].head, bones["Root"].tail = "Root", (0, 0, 0), (0, .7, 0)
    def add(name, head, tail, parent="Root"):
        b = obj.data.edit_bones.new(name); b.head = head; b.tail = tail; b.parent = bones[parent]; bones[name] = b
    add("Spine", (0, .6, 0), (0, 1.15, 0))
    add("Head", (0, 1.28, .05), (0, 1.55, .1), "Spine")
    # Two-bone arms and legs give the animation real elbows and knees while
    # preserving Arm_L/Arm_R and Leg_L/Leg_R names used by Unity gameplay.
    add("Arm_L", (-.3, 1.08, 0), (-.52, .78, .03), "Spine")
    add("Forearm_L", (-.52, .78, .03), (-.66, .47, .08), "Arm_L")
    add("Hand_L", (-.66, .47, .08), (-.68, .32, .14), "Forearm_L")
    add("Arm_R", (.3, 1.08, 0), (.52, .78, .03), "Spine")
    add("Forearm_R", (.52, .78, .03), (.66, .47, .08), "Arm_R")
    add("Hand_R", (.66, .47, .08), (.68, .32, .14), "Forearm_R")
    add("Leg_L", (-.2, .55, 0), (-.24, .30, .03), "Root")
    add("Shin_L", (-.24, .30, .03), (-.29, .08, .08), "Leg_L")
    add("Foot_L", (-.29, .08, .08), (-.29, .06, .32), "Shin_L")
    add("Leg_R", (.2, .55, 0), (.24, .30, .03), "Root")
    add("Shin_R", (.24, .30, .03), (.29, .08, .08), "Leg_R")
    add("Foot_R", (.29, .08, .08), (.29, .06, .32), "Shin_R")
    add("Tail_1", (0, .75, -.18), (.35, .9, -.7), "Spine")
    add("Tail_2", (.35, .9, -.7), (.1, 1.08, -1.05), "Tail_1")
    bpy.ops.object.mode_set(mode="OBJECT")
    for b in obj.pose.bones: b.rotation_mode = "XYZ"
    return obj


def bind(obj, name, material, arm, bone):
    obj.name = name; obj.data.materials.append(material)
    for poly in obj.data.polygons: poly.use_smooth = True
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1, "REPLACE")
    modifier = obj.modifiers.new("MonkeyRig", "ARMATURE"); modifier.object = arm
    return obj


def sphere(name, pos, size, material, arm, bone):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=pos)
    obj = bpy.context.object; obj.scale = size; bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return bind(obj, name, material, arm, bone)


def limb(name, start, end, radius, material, arm, bone):
    from mathutils import Vector
    a, b = Vector(start), Vector(end); direction = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=direction.length, location=(a + b) * .5)
    obj = bpy.context.object; obj.rotation_mode = "QUATERNION"; obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return bind(obj, name, material, arm, bone)


def model(arm, brown, tan, dark, white, gold):
    body = OrganicBody("MonkeyBody", resolution=0.04)

    # Pear-shaped torso: wide hips, soft belly, narrower chest and shoulders.
    body.ball((0, 0.62, 0), 0.24)
    body.ball((0, 0.78, 0.02), 0.27)
    body.ball((0, 0.98, 0.01), 0.25)
    body.ball((-0.17, 1.08, 0), 0.14)
    body.ball((0.17, 1.08, 0), 0.14)
    # Head with brow and short muzzle.
    body.chain((0, 1.14, 0.02), (0, 1.28, 0.04), 0.15, 0.15, 3)
    body.ball((0, 1.46, 0.05), 0.25)
    body.ball((0, 1.56, 0.12), 0.14)
    body.ball((0, 1.36, 0.26), 0.135)

    for side in (-1, 1):
        shoulder = (side * 0.26, 1.10, 0)
        elbow = (side * 0.52, 0.78, 0.03)
        wrist = (side * 0.66, 0.47, 0.08)
        hip = (side * 0.17, 0.58, 0)
        knee = (side * 0.24, 0.30, 0.03)
        ankle = (side * 0.29, 0.10, 0.08)
        # Long tapered arms ending in mitt hands with a thumb bump.
        body.chain((side * 0.22, 1.10, 0), shoulder, 0.13, 0.11, 3)
        body.chain(shoulder, elbow, 0.11, 0.085, 5)
        body.chain(elbow, wrist, 0.085, 0.075, 5)
        body.ball((side * 0.67, 0.40, 0.12), 0.10)
        body.ball((side * 0.71, 0.36, 0.20), 0.055)
        # Shorter legs with long gripping feet.
        body.chain(hip, knee, 0.12, 0.095, 4)
        body.chain(knee, ankle, 0.095, 0.08, 4)
        body.chain(ankle, (side * 0.29, 0.08, 0.30), 0.08, 0.06, 4)
    # Expressive tail following one smooth S-curve.
    body.curve((0, 0.70, -0.16), (0.18, 0.78, -0.52), (0.50, 0.95, -0.78),
               (0.10, 1.08, -1.02), 0.06, 0.032, samples=20)

    body.finish("Monkey_Body", arm, brown, regions=(
        (tan, lambda c: 1.26 < c.y < 1.62 and c.z > 0.16),                 # face mask
        (tan, lambda c: 0.58 < c.y < 1.12 and c.z > 0.17 and abs(c.x) < 0.22),  # chest
        (tan, lambda c: c.y < 0.13),                                       # feet
        (tan, lambda c: abs(c.x) > 0.58 and c.y < 0.50),                   # hands
    ))

    # Round ears with a lighter inner disc, sunk into the skull sides.
    for side, label in ((-1, "L"), (1, "R")):
        oriented_detail_sphere("Ear_" + label, (side * 0.27, 1.50, 0.03),
                               (0.055, 0.105, 0.09), (0, side * 0.5, 0), brown, arm, "Head")
        oriented_detail_sphere("EarInner_" + label, (side * 0.29, 1.50, 0.05),
                               (0.035, 0.075, 0.065), (0, side * 0.5, 0), tan, arm, "Head")
    # Layered eyes with a highlight plus a small flat nose.
    spark = mat("Monkey_Spark", (1, 1, 0.96))
    for side, label in ((-1, "L"), (1, "R")):
        cartoon_eye("Eye_" + label, (side * 0.10, 1.51, 0.245), (side * 0.16, 0.03, 1.0),
                    0.075, arm, "Head", white, gold, dark, spark)
    detail_sphere("Nose", (0, 1.385, 0.385), (0.055, 0.04, 0.03), dark, arm, "Head")


def pose(arm, frame, rotations=None, root=None):
    rotations = rotations or {}
    for bone in arm.pose.bones:
        bone.location = (0, 0, 0); bone.rotation_euler = (0, 0, 0)
    for name, values in rotations.items(): arm.pose.bones[name].rotation_euler = tuple(math.radians(v) for v in values)
    if root: arm.pose.bones["Root"].location = root
    for bone in arm.pose.bones:
        bone.keyframe_insert(data_path="rotation_euler", frame=frame)
        if bone.name == "Root": bone.keyframe_insert(data_path="location", frame=frame)


def action(arm, name, end, frames):
    arm.animation_data_create(); arm.animation_data.action = bpy.data.actions.new(name)
    for frame, rotations, root in frames: pose(arm, frame, rotations, root)
    arm.animation_data.action.use_fake_user = True


def animations(arm):
    action(arm, "Monkey_Idle", 30, [(1, {"Tail_1": (0, 0, 7)}, None), (15, {"Tail_1": (0, 0, -7)}, None), (30, {"Tail_1": (0, 0, 7)}, None)])
    a = {"Leg_L": (28, 0, 0), "Shin_L": (-18, 0, 0), "Foot_L": (8, 0, 0),
         "Arm_R": (-32, 0, 0), "Forearm_R": (-12, 0, 0),
         "Leg_R": (-28, 0, 0), "Shin_R": (22, 0, 0), "Foot_R": (-8, 0, 0),
         "Arm_L": (32, 0, 0), "Forearm_L": (12, 0, 0), "Tail_1": (0, 0, 12)}
    b = {name: tuple(-v for v in value) for name, value in a.items()}
    action(arm, "Monkey_Walk", 30, [(1, a, None), (15, b, None), (30, a, None)])
    action(arm, "Monkey_Run", 20, [(1, {n: tuple(v * 1.5 for v in r) for n, r in a.items()}, None), (10, {n: tuple(v * 1.5 for v in r) for n, r in b.items()}, None), (20, {n: tuple(v * 1.5 for v in r) for n, r in a.items()}, None)])
    action(arm, "Monkey_Punch", 22, [
        (1, {"Arm_L": (18,0,-10), "Arm_R": (18,0,10), "Forearm_L": (-38,0,0), "Forearm_R": (-38,0,0)}, None),
        (7, {"Arm_L": (-46,0,-8), "Arm_R": (-46,0,8), "Forearm_L": (-62,0,0), "Forearm_R": (-62,0,0), "Hand_L": (-18,0,0), "Hand_R": (-18,0,0), "Spine": (-10,0,0)}, (0,.04,-.06)),
        (12, {"Arm_L": (86,0,-4), "Arm_R": (86,0,4), "Forearm_L": (8,0,0), "Forearm_R": (8,0,0), "Hand_L": (12,0,0), "Hand_R": (12,0,0), "Spine": (16,0,0)}, (0,.08,.20)),
        (22, {}, None)
    ])
    # The two raised arms make the Q leap read as an intentional grab before launch.
    action(arm, "Monkey_VineLeap", 26, [
        (1, {"Leg_L": (34,0,0), "Leg_R": (34,0,0), "Shin_L": (-42,0,0), "Shin_R": (-42,0,0)}, None),
        (8, {"Arm_L": (82,0,-18), "Arm_R": (-82,0,18), "Forearm_L": (18,0,0), "Forearm_R": (-18,0,0), "Spine": (12,0,0)}, (0,.10,.18)),
        (14, {"Arm_L": (156,0,-20), "Arm_R": (-156,0,20), "Forearm_L": (18,0,0), "Forearm_R": (-18,0,0), "Hand_L": (-18,0,0), "Hand_R": (18,0,0), "Spine": (18,0,0), "Tail_1": (0,0,38), "Leg_L": (24,0,0), "Leg_R": (-24,0,0)}, (0,.34,.62)),
        (20, {"Arm_L": (168,0,-16), "Arm_R": (-168,0,16), "Forearm_L": (10,0,0), "Forearm_R": (-10,0,0), "Hand_L": (-24,0,0), "Hand_R": (24,0,0), "Leg_L": (-18,0,0), "Leg_R": (18,0,0)}, (0,.42,.68)),
        (26, {}, None)
    ])
    action(arm, "Monkey_Slam", 24, [(1, {}, None), (8, {"Arm_L": (48,0,0), "Arm_R": (48,0,0)}, None), (14, {"Arm_L": (-78,0,0), "Arm_R": (-78,0,0), "Spine": (18,0,0)}, None), (24, {}, None)])
    arm.animation_data.action = bpy.data.actions["Monkey_Idle"]


def export(arm):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = arm
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Monkey_Source.blend"))
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Monkey.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, mesh_smooth_type="FACE", apply_scale_options="FBX_SCALE_UNITS")


clean()
BROWN, TAN, DARK, WHITE, GOLD = mat("Monkey_Brown", (.25,.09,.025)), mat("Monkey_Tan", (.84,.48,.20)), mat("Monkey_Dark", (.035,.014,.008)), mat("Monkey_White", (.96,.88,.66)), mat("Monkey_Gold", (1,.55,.06))
ARM = rig(); model(ARM, BROWN, TAN, DARK, WHITE, GOLD); animations(ARM); export(ARM)
