"""Validates bones, gameplay actions and mesh bindings in an opened character .blend."""
import bpy
import os


RULES = {
    "Ant": {
        "bones": ["Root", "Thorax", "Head", "Mandible_L", "Mandible_R", "Leg_F_L", "LowerLeg_F_L", "Leg_M_R", "LowerLeg_M_R", "Leg_B_L", "LowerLeg_B_L"],
        "actions": ["Ant_Idle", "Ant_Walk", "Ant_Run", "Ant_Bite", "Ant_Throw", "Ant_Burrow", "Ant_Shield"],
    },
    "Monkey": {
        "bones": ["Root", "Spine", "Head", "Arm_L", "Forearm_L", "Hand_L", "Arm_R", "Forearm_R", "Hand_R", "Leg_L", "Shin_L", "Foot_L", "Leg_R", "Shin_R", "Foot_R"],
        "actions": ["Monkey_Idle", "Monkey_Walk", "Monkey_Run", "Monkey_Punch", "Monkey_VineLeap", "Monkey_Slam"],
    },
    "Tiger": {
        "bones": ["Root", "Spine", "Chest", "Head", "Leg_FL", "LowerLeg_FL", "Paw_FL", "Claw_FL", "Leg_FR", "LowerLeg_FR", "Paw_FR", "Claw_FR"],
        "actions": ["Tiger_Idle", "Tiger_Walk", "Tiger_Run", "Tiger_Claw", "Tiger_Pounce", "Tiger_Roar"],
    },
    "Eagle": {
        "bones": ["Root", "Body", "Head", "Wing_L", "WingTip_L", "Wing_R", "WingTip_R", "Leg_L", "LowerLeg_L", "Talon_L", "Leg_R", "LowerLeg_R", "Talon_R"],
        "actions": ["Eagle_Idle", "Eagle_Fly", "Eagle_Dive", "Eagle_Gust", "Eagle_Perch"],
    },
}


blend_name = os.path.basename(bpy.data.filepath)
animal = next((name for name in RULES if blend_name.startswith(name + "_")), None)
if animal is None:
    raise RuntimeError("Could not determine character from " + blend_name)

rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
if len(rigs) != 1:
    raise RuntimeError(f"{animal}: expected one armature, found {len(rigs)}")
rig = rigs[0]

missing_bones = [name for name in RULES[animal]["bones"] if name not in rig.data.bones]
missing_actions = [name for name in RULES[animal]["actions"] if name not in bpy.data.actions]
empty_actions = []
for name in RULES[animal]["actions"]:
    if name not in bpy.data.actions:
        continue
    action = bpy.data.actions[name]
    # Blender 5 stores animation curves in layered channel bags instead of the
    # old Action.fcurves collection. A non-zero authored frame range works for
    # both APIs and catches accidentally empty gameplay clips.
    start, end = action.frame_range
    if end <= start:
        empty_actions.append(name)

unbound_meshes = []
for mesh in (obj for obj in bpy.context.scene.objects if obj.type == "MESH"):
    has_rig = any(mod.type == "ARMATURE" and mod.object == rig for mod in mesh.modifiers)
    if not has_rig or len(mesh.vertex_groups) == 0:
        unbound_meshes.append(mesh.name)

errors = []
if missing_bones: errors.append("missing bones: " + ", ".join(missing_bones))
if missing_actions: errors.append("missing actions: " + ", ".join(missing_actions))
if empty_actions: errors.append("empty actions: " + ", ".join(empty_actions))
if unbound_meshes: errors.append("unbound meshes: " + ", ".join(unbound_meshes))
if errors:
    raise RuntimeError(animal + " validation failed — " + " | ".join(errors))

print(f"RIG_VALID {animal}: {len(rig.data.bones)} bones, {len(RULES[animal]['actions'])} gameplay actions, all meshes bound")
