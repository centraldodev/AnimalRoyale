"""Extracts five game-ready rock variants from the lightest ArtRefs atlas LOD.

Usage:
  blender --background --python Tools/Blender/extract_artref_rocks.py -- <source.fbx> <output-directory>

The source repeats the complete rock atlas as model_LOD0..4. Only model_LOD4
is separated into its five largest connected islands. Each rock receives two
additional decimated LODs and is normalized to a consistent game scale.
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector


TARGET_MAX_DIMENSION = 2.2
MID_RATIO = 0.46
FAR_RATIO = 0.18


def arguments():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) < 2:
        raise SystemExit("Expected source FBX and output directory")
    return os.path.abspath(args[0]), os.path.abspath(args[1])


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


def isolate_rocks(source):
    select_only([source])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.mesh.separate(type="LOOSE")
    parts = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    rocks = sorted(parts, key=lambda obj: len(obj.data.vertices), reverse=True)[:5]
    rock_set = set(rocks)
    for part in parts:
        if part not in rock_set:
            bpy.data.objects.remove(part, do_unlink=True)
    if len(rocks) != 5:
        raise RuntimeError(f"Expected five rock components, found {len(rocks)}")
    return rocks


def original_volume(obj):
    minimum, maximum = world_bounds(obj)
    size = maximum - minimum
    return size.x * size.y * size.z


def normalize_rock(obj):
    minimum, maximum = world_bounds(obj)
    size = maximum - minimum
    center = (minimum + maximum) * 0.5
    maximum_dimension = max(size.x, size.y, size.z)
    scale = TARGET_MAX_DIMENSION / max(0.001, maximum_dimension)
    transform = Matrix.Scale(scale, 4) @ Matrix.Translation((-center.x, -center.y, -minimum.z))
    obj.data.transform(transform)
    obj.location = Vector((0.0, 0.0, 0.0))
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)


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


def export_variant(rock, index, output_directory):
    base_name = f"ArtRefRock{index:02d}"
    rock.name = base_name + "_LOD0"
    middle = decimated_copy(rock, base_name + "_LOD1", MID_RATIO)
    far = decimated_copy(rock, base_name + "_LOD2", FAR_RATIO)
    lods = [rock, middle, far]
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

    rocks = isolate_rocks(source)
    rocks.sort(key=original_volume)
    for index, rock in enumerate(rocks, start=1):
        normalize_rock(rock)
        export_variant(rock, index, output_directory)


if __name__ == "__main__":
    main()
