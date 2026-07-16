"""Extracts the five tree islands from the lightest ArtRefs LOD.

Usage:
  blender --background --python Tools/Blender/extract_artref_trees.py -- <source.fbx> <output-directory>

The source contains the complete five-tree atlas repeated as model_LOD0..4.
Only model_LOD4 is used, then each large connected island becomes one tree.
Two additional decimated meshes are generated per tree for Unity LODGroups.
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector


TARGET_SCALE = 10.0
MID_RATIO = 0.46
FAR_RATIO = 0.18


def arguments():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) < 2:
        raise SystemExit("Expected source FBX and output directory")
    return os.path.abspath(args[0]), os.path.abspath(args[1])


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(corner[axis] for corner in corners) for axis in range(3)))
    maximum = Vector(tuple(max(corner[axis] for corner in corners) for axis in range(3)))
    return minimum, maximum


def isolate_tree_components(source_obj):
    bpy.ops.object.select_all(action="DESELECT")
    source_obj.hide_set(False)
    source_obj.select_set(True)
    bpy.context.view_layer.objects.active = source_obj
    bpy.ops.mesh.separate(type="LOOSE")

    parts = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    trees = sorted(parts, key=lambda obj: len(obj.data.vertices), reverse=True)[:5]
    tree_set = set(trees)
    for obj in parts:
        if obj not in tree_set:
            bpy.data.objects.remove(obj, do_unlink=True)
    return trees


def normalize_tree(obj):
    minimum, maximum = world_bounds(obj)
    center = (minimum + maximum) * 0.5
    translation = Matrix.Translation((-center.x, -center.y, -minimum.z))
    obj.data.transform(translation)
    obj.location = Vector((0.0, 0.0, 0.0))
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (TARGET_SCALE, TARGET_SCALE, TARGET_SCALE)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)


def decimated_copy(source, name, ratio):
    result = source.copy()
    result.data = source.data.copy()
    result.name = name
    bpy.context.collection.objects.link(result)
    modifier = result.modifiers.new(name="GameLOD", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = result
    result.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    result.select_set(False)
    return result


def export_variant(tree, index, output_directory):
    base_name = f"ArtRefTree{index:02d}"
    tree.name = base_name + "_LOD0"
    mid = decimated_copy(tree, base_name + "_LOD1", MID_RATIO)
    far = decimated_copy(tree, base_name + "_LOD2", FAR_RATIO)
    lods = [tree, mid, far]

    bpy.ops.object.select_all(action="DESELECT")
    for lod in lods:
        lod.select_set(True)
    bpy.context.view_layer.objects.active = tree

    path = os.path.join(output_directory, base_name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )

    triangle_counts = []
    for lod in lods:
        lod.data.calc_loop_triangles()
        triangle_counts.append(len(lod.data.loop_triangles))
    print(f"EXPORTED {base_name}: triangles={triangle_counts} path={path}")

    for lod in lods:
        bpy.data.objects.remove(lod, do_unlink=True)


def main():
    source_path, output_directory = arguments()
    os.makedirs(output_directory, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source_path)

    source = bpy.data.objects.get("model_LOD4")
    if source is None or source.type != "MESH":
        raise RuntimeError("model_LOD4 mesh was not found")

    for obj in list(bpy.data.objects):
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)

    trees = isolate_tree_components(source)
    if len(trees) != 5:
        raise RuntimeError(f"Expected five large tree components, found {len(trees)}")

    # Stable left-to-right ordering based on the original atlas positions.
    trees.sort(key=lambda obj: world_bounds(obj)[0].x)
    for index, tree in enumerate(trees, start=1):
        normalize_tree(tree)
        export_variant(tree, index, output_directory)


if __name__ == "__main__":
    main()
