"""Extracts game-ready bushes, bamboo and stone ruins from ArtRefs FBX packs.

Usage:
  blender --background --python Tools/Blender/extract_artref_environment.py -- bushes <source.fbx> <output-directory>
  blender --background --python Tools/Blender/extract_artref_environment.py -- bamboo <source.fbx> <output-directory>
  blender --background --python Tools/Blender/extract_artref_environment.py -- stone <source.fbx> <output-directory>
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector


def arguments():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) < 3 or args[0] not in {"bushes", "bamboo", "stone"}:
        raise SystemExit("Expected mode (bushes, bamboo or stone), source FBX and output directory")
    return args[0], os.path.abspath(args[1]), os.path.abspath(args[2])


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def apply_transforms(objects):
    select_only(objects)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(corner[axis] for corner in corners) for axis in range(3)))
    maximum = Vector(tuple(max(corner[axis] for corner in corners) for axis in range(3)))
    return minimum, maximum


def combined_bounds(objects):
    bounds = [world_bounds(obj) for obj in objects]
    minimum = Vector(tuple(min(item[0][axis] for item in bounds) for axis in range(3)))
    maximum = Vector(tuple(max(item[1][axis] for item in bounds) for axis in range(3)))
    return minimum, maximum


def normalize_objects(objects, scale):
    apply_transforms(objects)
    minimum, maximum = combined_bounds(objects)
    center = (minimum + maximum) * 0.5
    transform = Matrix.Scale(scale, 4) @ Matrix.Translation((-center.x, -center.y, -minimum.z))
    for obj in objects:
        obj.data.transform(transform)
        obj.location = Vector((0.0, 0.0, 0.0))


def join_objects(objects, name):
    select_only(objects)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.view_layer.objects.active
    result.name = name
    return result


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


def export_lods(lods, base_name, output_directory):
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


def extract_bushes(output_directory):
    source = bpy.data.objects.get("model_LOD4")
    if source is None:
        raise RuntimeError("model_LOD4 mesh was not found")
    select_only([source])
    bpy.ops.mesh.separate(type="LOOSE")
    parts = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    parts.sort(key=lambda obj: len(obj.data.vertices), reverse=True)
    main_parts = parts[:8]
    groups = {main: [main] for main in main_parts}
    main_centers = {}
    for main in main_parts:
        minimum, maximum = world_bounds(main)
        main_centers[main] = (minimum + maximum) * 0.5
    for part in parts[8:]:
        minimum, maximum = world_bounds(part)
        center = (minimum + maximum) * 0.5
        nearest = min(main_parts, key=lambda main: (main_centers[main] - center).length_squared)
        groups[nearest].append(part)

    ordered = sorted(main_parts, key=lambda main: (-main_centers[main].z, main_centers[main].x))
    for index, main in enumerate(ordered, start=1):
        base_name = f"ArtRefBush{index:02d}"
        bush = join_objects(groups[main], base_name + "_LOD0")
        normalize_objects([bush], 4.0)
        middle = decimated_copy(bush, base_name + "_LOD1", 0.42)
        far = decimated_copy(bush, base_name + "_LOD2", 0.15)
        export_lods([bush, middle, far], base_name, output_directory)


def extract_bamboo(output_directory):
    source = bpy.data.objects.get("model_LOD4")
    if source is None:
        raise RuntimeError("model_LOD4 mesh was not found")
    normalize_objects([source], 2.8)
    base_name = "ArtRefBamboo"
    near = decimated_copy(source, base_name + "_LOD0", 0.62)
    middle = decimated_copy(source, base_name + "_LOD1", 0.28)
    far = decimated_copy(source, base_name + "_LOD2", 0.10)
    export_lods([near, middle, far], base_name, output_directory)


def extract_stone(output_directory):
    source_names = ("model_LOD2", "model_LOD3", "model_LOD4")
    lods = [bpy.data.objects.get(name) for name in source_names]
    if any(lod is None for lod in lods):
        raise RuntimeError("The native LOD2, LOD3 and LOD4 meshes were not found")
    normalize_objects(lods, 4.0)
    base_name = "ArtRefStoneRuin"
    for index, lod in enumerate(lods):
        lod.name = f"{base_name}_LOD{index}"
    export_lods(lods, base_name, output_directory)


def main():
    mode, source_path, output_directory = arguments()
    os.makedirs(output_directory, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source_path)

    if mode == "bushes":
        extract_bushes(output_directory)
    elif mode == "bamboo":
        extract_bamboo(output_directory)
    else:
        extract_stone(output_directory)


if __name__ == "__main__":
    main()
