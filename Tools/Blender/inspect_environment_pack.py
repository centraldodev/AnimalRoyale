"""Reports the loose components stored in the lightest mesh of an ArtRefs pack.

Usage:
  blender --background --python Tools/Blender/inspect_environment_pack.py -- <source.fbx>
"""

import os
import sys

import bpy
from mathutils import Vector


def source_path():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if not args:
        raise SystemExit("Expected an FBX path")
    return os.path.abspath(args[0])


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(corner[axis] for corner in corners) for axis in range(3)))
    maximum = Vector(tuple(max(corner[axis] for corner in corners) for axis in range(3)))
    return minimum, maximum


def main():
    path = source_path()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)

    source = bpy.data.objects.get("model_LOD4")
    if source is None or source.type != "MESH":
        raise RuntimeError("model_LOD4 mesh was not found")

    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.mesh.separate(type="LOOSE")

    parts = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    parts.sort(key=lambda obj: len(obj.data.vertices), reverse=True)
    print(f"PACK {os.path.basename(os.path.dirname(path))} components={len(parts)}")
    for index, part in enumerate(parts):
        part.data.calc_loop_triangles()
        minimum, maximum = world_bounds(part)
        center = (minimum + maximum) * 0.5
        size = maximum - minimum
        print(
            f"  {index:02d} verts={len(part.data.vertices):6d} "
            f"tris={len(part.data.loop_triangles):6d} "
            f"center=({center.x:.3f},{center.y:.3f},{center.z:.3f}) "
            f"size=({size.x:.3f},{size.y:.3f},{size.z:.3f})"
        )


if __name__ == "__main__":
    main()
