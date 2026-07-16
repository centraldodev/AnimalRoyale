"""Creates a lightweight procedural rig for the four generated animal meshes.

The Rodin FBXs are continuous, unrigged meshes.  This script keeps that surface
continuous, adds independently controllable head, limb (or wing) and optional
tail bones, calculates smooth heat weights and exports a Unity-ready FBX.

Usage:
  blender --background --python ArtRefs/rig_animals.py -- \
    Tiger source.fbx destination.fbx [/tmp/preview.png]
"""

import math
import os
import sys

import bpy
from mathutils import Vector


# Positions are fractions of half-width (x) and total height (z).  They were
# measured from the front silhouettes so joints sit inside the generated mesh.
RIGS = {
    "Tiger": {
        "body": (0.20, 0.61),
        "head": (0.53, 0.89),
        "arm": (0.45, 0.54, 0.70, 0.29),
        "leg": (0.26, 0.30, 0.43, 0.06),
        "tail": (0.28, 0.27, 0.82, 0.24),
    },
    "Ant": {
        "body": (0.19, 0.61),
        "head": (0.56, 0.88),
        "arm": (0.35, 0.54, 0.54, 0.29),
        "leg": (0.25, 0.31, 0.41, 0.06),
        "antenna": (0.20, 0.81, 0.48, 0.98),
    },
    "Eagle": {
        "body": (0.19, 0.60),
        "head": (0.51, 0.88),
        "arm": (0.34, 0.55, 0.79, 0.31),
        "leg": (0.20, 0.28, 0.38, 0.06),
    },
    "Monkey": {
        "body": (0.19, 0.62),
        "head": (0.53, 0.90),
        "arm": (0.42, 0.55, 0.64, 0.27),
        "leg": (0.24, 0.31, 0.40, 0.06),
        "tail": (0.25, 0.28, 0.83, 0.31),
    },
}


def arguments():
    values = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(values) < 3 or values[0] not in RIGS:
        raise SystemExit("Expected: <Tiger|Ant|Eagle|Monkey> <source.fbx> <output.fbx> [preview.png]")
    return values[0], os.path.abspath(values[1]), os.path.abspath(values[2]), (
        os.path.abspath(values[3]) if len(values) > 3 else None
    )


def import_single_mesh(path, animal):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"No mesh in {path}")

    # The gameplay FBXs currently contain one figure. Join defensively if an
    # exporter left tiny disconnected details as separate mesh objects.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = max(meshes, key=lambda item: len(item.data.vertices))
    if len(meshes) > 1:
        bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    mesh.name = f"{animal}_Mesh"
    mesh.data.name = f"{animal}_Skin"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Deterministic origin: horizontally centered and standing on Z=0.
    points = [vertex.co for vertex in mesh.data.vertices]
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    center = (minimum + maximum) * 0.5
    shift = Vector((-center.x, -center.y, -minimum.z))
    for vertex in mesh.data.vertices:
        vertex.co += shift
    mesh.data.update()
    return mesh, maximum.x - minimum.x, maximum.z - minimum.z


def make_armature(animal, width, height):
    config = RIGS[animal]
    half_width = width * 0.5
    armature_data = bpy.data.armatures.new(f"{animal}_Rig")
    armature = bpy.data.objects.new(f"{animal}_Rig", armature_data)
    bpy.context.collection.objects.link(armature)
    armature.show_in_front = True

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(name, head, tail, parent=None, deform=True):
        item = armature_data.edit_bones.new(name)
        item.head = Vector(head)
        item.tail = Vector(tail)
        # All authored joints lie in the X/Z silhouette plane. Aligning local Z
        # to model depth makes local-X a predictable forward/back swing axis and
        # local-Z a predictable front-view flap axis on both mirrored sides.
        item.align_roll(Vector((0, 1, 0)))
        item.parent = parent
        item.use_connect = False
        item.use_deform = deform
        return item

    rig_root = bone("RigRoot", (0, 0, 0), (0, 0, height * 0.10), deform=False)
    body_low, body_high = config["body"]
    body = bone("Body", (0, 0, height * body_low), (0, 0, height * body_high), rig_root)
    head_low, head_high = config["head"]
    bone("Head", (0, 0, height * head_low), (0, 0, height * head_high), body)

    shoulder_x, shoulder_z, hand_x, hand_z = config["arm"]
    limb_prefix = "Wing" if animal == "Eagle" else "Arm"
    for suffix, side in (("L", 1), ("R", -1)):
        bone(
            f"{limb_prefix}_{suffix}",
            (side * half_width * shoulder_x, 0, height * shoulder_z),
            (side * half_width * hand_x, 0, height * hand_z),
            body,
        )

    hip_x, hip_z, foot_x, foot_z = config["leg"]
    for suffix, side in (("L", 1), ("R", -1)):
        bone(
            f"Leg_{suffix}",
            (side * half_width * hip_x, 0, height * hip_z),
            (side * half_width * foot_x, 0, height * foot_z),
            body,
        )

    if "tail" in config:
        root_x, root_z, tip_x, tip_z = config["tail"]
        bone(
            "Tail",
            (half_width * root_x, width * 0.08, height * root_z),
            (half_width * tip_x, width * 0.04, height * tip_z),
            body,
        )

    if "antenna" in config:
        root_x, root_z, tip_x, tip_z = config["antenna"]
        for suffix, side in (("L", 1), ("R", -1)):
            bone(
                f"Antenna_{suffix}",
                (side * half_width * root_x, 0, height * root_z),
                (side * half_width * tip_x, 0, height * tip_z),
                body,
            )

    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def bind_with_automatic_weights(mesh, armature):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    missing = []
    for bone in armature.data.bones:
        if bone.use_deform and mesh.vertex_groups.get(bone.name) is None:
            missing.append(bone.name)
    if missing:
        raise RuntimeError("Automatic weights did not create groups: " + ", ".join(missing))

    modifier = next((item for item in mesh.modifiers if item.type == "ARMATURE"), None)
    if modifier is None:
        raise RuntimeError("Armature modifier was not created")
    modifier.name = "Animal Rig"

    # Rodin occasionally leaves a few isolated decorative vertices that the
    # heat solver cannot reach. Keep those stable on the torso.
    deform_indices = {
        group.index for group in mesh.vertex_groups
        if armature.data.bones.get(group.name) is not None
        and armature.data.bones[group.name].use_deform
    }
    unweighted = [
        vertex.index for vertex in mesh.data.vertices
        if not any(group.group in deform_indices and group.weight > 0.0001 for group in vertex.groups)
    ]
    if unweighted:
        mesh.vertex_groups["Body"].add(unweighted, 1.0, "REPLACE")
        print(f"[Rig] assigned {len(unweighted)} isolated vertices to Body")


def print_weight_report(mesh, armature):
    deform_names = {bone.name for bone in armature.data.bones if bone.use_deform}
    totals = {name: 0 for name in deform_names}
    dominant = {name: 0 for name in deform_names}
    unweighted = 0
    for vertex in mesh.data.vertices:
        influences = [
            group for group in vertex.groups
            if group.weight > 0.001 and mesh.vertex_groups[group.group].name in deform_names
        ]
        if not influences:
            unweighted += 1
        for influence in influences:
            totals[mesh.vertex_groups[influence.group].name] += 1
        if influences:
            strongest = max(influences, key=lambda item: item.weight)
            dominant[mesh.vertex_groups[strongest.group].name] += 1
    print(f"[Rig] vertices={len(mesh.data.vertices)} unweighted={unweighted}")
    for name in sorted(totals):
        print(
            f"[Rig] {name:12s} influenced_vertices={totals[name]:6d} "
            f"dominant_vertices={dominant[name]:6d}"
        )
    if unweighted:
        raise RuntimeError(f"{unweighted} vertices have no deform weight")


def render_pose_preview(animal, mesh, armature, width, height, output_path):
    # A deliberately asymmetric test pose makes failed or crossed limb weights
    # obvious before the asset reaches Unity.
    bpy.context.view_layer.objects.active = armature
    armature.data.pose_position = "POSE"
    for name, degrees in {
        "Head": (8, 8, -5),
        "Arm_L": (-28, 0, -7),
        "Arm_R": (28, 0, 7),
        "Wing_L": (0, 0, -42),
        "Wing_R": (0, 0, 42),
        "Leg_L": (25, 0, 0),
        "Leg_R": (-25, 0, 0),
        "Tail": (0, 18, 0),
        "Antenna_L": (0, 0, -8),
        "Antenna_R": (0, 0, 8),
    }.items():
        pose_bone = armature.pose.bones.get(name)
        if pose_bone is None:
            continue
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = tuple(math.radians(value) for value in degrees)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 600
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.62, 0.72, 0.90)

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = height * 1.14
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.location = Vector((0, -height * 2.2, height * 0.50))
    camera.rotation_euler = (math.radians(90), 0, 0)
    scene.render.filepath = output_path
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print(f"[Rig] preview={output_path}")

    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion.identity()
    bpy.context.view_layer.update()


def export_fbx(mesh, armature, output_path):
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_anim=False,
        mesh_smooth_type="FACE",
        axis_forward="-Z",
        axis_up="Y",
    )
    print(f"[Rig] exported={output_path}")


def main():
    animal, source, output, preview = arguments()
    if not os.path.exists(source):
        raise SystemExit(f"Missing source: {source}")
    mesh, width, height = import_single_mesh(source, animal)
    armature = make_armature(animal, width, height)
    bind_with_automatic_weights(mesh, armature)
    print_weight_report(mesh, armature)
    if preview:
        render_pose_preview(animal, mesh, armature, width, height, preview)
    export_fbx(mesh, armature, output)


if __name__ == "__main__":
    main()
