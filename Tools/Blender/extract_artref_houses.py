"""Extracts five game-ready house variants from an ArtRefs atlas.

Usage:
  blender --background --python Tools/Blender/extract_artref_houses.py -- <source.fbx> <output-directory>

The source stores five multi-part houses in model_LOD4. The parts are assigned
to the nearest main building, joined, normalized to game scale and exported
with two additional decimated LODs. ArtRefHouse05 is the raised house with the
long front staircase.
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector


TARGET_MAX_DIMENSION = 8.5
MID_RATIO = 0.48
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


def object_center(obj):
    minimum, maximum = world_bounds(obj)
    return (minimum + maximum) * 0.5


def separate_house_groups(source):
    select_only([source])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.mesh.separate(type="LOOSE")
    parts = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]

    for part in list(parts):
        if len(part.data.polygons) > 0:
            continue
        bpy.data.objects.remove(part, do_unlink=True)
        parts.remove(part)

    anchors = sorted(parts, key=lambda obj: len(obj.data.vertices), reverse=True)[:5]
    if len(anchors) != 5:
        raise RuntimeError(f"Expected five house anchors, found {len(anchors)}")

    centers = {anchor: object_center(anchor) for anchor in anchors}
    groups = {anchor: [anchor] for anchor in anchors}
    for part in parts:
        if part in groups:
            continue
        center = object_center(part)
        nearest = min(
            anchors,
            key=lambda anchor: (centers[anchor].x - center.x) ** 2
            + (centers[anchor].z - center.z) ** 2,
        )
        groups[nearest].append(part)

    # Atlas order: three houses on the upper row, then two on the lower row.
    # This stable order makes the lower-right stair house ArtRefHouse05.
    ordered = sorted(
        anchors,
        key=lambda anchor: (0 if centers[anchor].z > 0.6 else 1, centers[anchor].x),
    )
    return [(anchor, groups[anchor]) for anchor in ordered]


def join_objects(objects, name):
    select_only(objects)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.view_layer.objects.active
    result.name = name
    return result


def normalize_house(house):
    select_only([house])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    minimum, maximum = world_bounds(house)
    size = maximum - minimum
    scale = TARGET_MAX_DIMENSION / max(0.001, max(size.x, size.y, size.z))
    center = (minimum + maximum) * 0.5
    transform = Matrix.Scale(scale, 4) @ Matrix.Translation((-center.x, -center.y, -minimum.z))
    house.data.transform(transform)
    house.location = Vector((0.0, 0.0, 0.0))
    house.rotation_euler = (0.0, 0.0, 0.0)
    house.scale = (1.0, 1.0, 1.0)


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


def export_house(house, index, output_directory):
    base_name = f"ArtRefHouse{index:02d}"
    house.name = base_name + "_LOD0"
    middle = decimated_copy(house, base_name + "_LOD1", MID_RATIO)
    far = decimated_copy(house, base_name + "_LOD2", FAR_RATIO)
    lods = [house, middle, far]
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

    groups = separate_house_groups(source)
    for index, (_, parts) in enumerate(groups, start=1):
        house = join_objects(parts, f"ArtRefHouse{index:02d}_LOD0")
        normalize_house(house)
        export_house(house, index, output_directory)


if __name__ == "__main__":
    main()
