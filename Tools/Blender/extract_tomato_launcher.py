"""Creates a game-ready fruit launcher with three LOD meshes.

Usage:
  blender --background --python Tools/Blender/extract_tomato_launcher.py -- <source.fbx> <output-directory> [asset-name]
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector


TARGET_LENGTH = 1.05
NEAR_RATIO = 0.24
MID_RATIO = 0.09
FAR_RATIO = 0.035


def arguments():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) < 2:
        raise SystemExit("Expected source FBX and output directory")
    asset_name = args[2] if len(args) > 2 else "TomatoLauncher"
    return os.path.abspath(args[0]), os.path.abspath(args[1]), asset_name


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(corner[axis] for corner in corners) for axis in range(3)))
    maximum = Vector(tuple(max(corner[axis] for corner in corners) for axis in range(3)))
    return minimum, maximum


def normalize(source):
    select_only([source])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    minimum, maximum = world_bounds(source)
    center = (minimum + maximum) * 0.5
    source_length = max(0.001, maximum.y - minimum.y)
    scale = TARGET_LENGTH / source_length
    source.data.transform(Matrix.Scale(scale, 4) @ Matrix.Translation(-center))
    source.location = Vector((0.0, 0.0, 0.0))
    source.rotation_euler = (0.0, 0.0, 0.0)
    source.scale = (1.0, 1.0, 1.0)


def decimated_copy(source, name, ratio):
    result = source.copy()
    result.data = source.data.copy()
    result.name = name
    bpy.context.collection.objects.link(result)
    modifier = result.modifiers.new(name="GameLOD", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    select_only([result])
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return result


def main():
    source_path, output_directory, base_name = arguments()
    os.makedirs(output_directory, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source_path)

    source = bpy.data.objects.get("model_LOD4")
    if source is None or source.type != "MESH":
        raise RuntimeError("model_LOD4 mesh was not found")
    for obj in list(bpy.data.objects):
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)

    normalize(source)
    near = decimated_copy(source, base_name + "_LOD0", NEAR_RATIO)
    middle = decimated_copy(source, base_name + "_LOD1", MID_RATIO)
    far = decimated_copy(source, base_name + "_LOD2", FAR_RATIO)
    lods = [near, middle, far]
    select_only(lods)

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


if __name__ == "__main__":
    main()
