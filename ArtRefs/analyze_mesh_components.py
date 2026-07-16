"""Reports connected mesh islands and their bounds for an imported animal FBX.

Usage:
  Blender --background --python ArtRefs/analyze_mesh_components.py -- model.fbx
"""

import os
import sys

import bpy


def input_path():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return os.path.abspath(args[0]) if args else ""


def connected_components(mesh):
    neighbors = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].append(b)
        neighbors[b].append(a)

    remaining = set(range(len(mesh.vertices)))
    components = []
    while remaining:
        seed = remaining.pop()
        stack = [seed]
        component = [seed]
        while stack:
            current = stack.pop()
            for neighbor in neighbors[current]:
                if neighbor not in remaining:
                    continue
                remaining.remove(neighbor)
                stack.append(neighbor)
                component.append(neighbor)
        components.append(component)
    return sorted(components, key=len, reverse=True)


def main():
    path = input_path()
    if not path or not os.path.exists(path):
        raise SystemExit(f"Missing FBX: {path}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    print(f"MODEL {os.path.basename(path)}")
    for obj in [item for item in bpy.context.scene.objects if item.type == "MESH"]:
        components = connected_components(obj.data)
        print(f"MESH {obj.name} vertices={len(obj.data.vertices)} components={len(components)}")
        for index, component in enumerate(components[:40]):
            points = [obj.data.vertices[vertex].co for vertex in component]
            minimum = [min(point[axis] for point in points) for axis in range(3)]
            maximum = [max(point[axis] for point in points) for axis in range(3)]
            center = [(minimum[axis] + maximum[axis]) * 0.5 for axis in range(3)]
            size = [maximum[axis] - minimum[axis] for axis in range(3)]
            print(
                f"  {index:02d} verts={len(component):6d} "
                f"center=({center[0]:.3f},{center[1]:.3f},{center[2]:.3f}) "
                f"size=({size[0]:.3f},{size[1]:.3f},{size[2]:.3f})"
            )


if __name__ == "__main__":
    main()
