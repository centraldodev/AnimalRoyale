"""Renders the lightest mesh of an ArtRefs environment pack for inspection.

Usage:
  blender --background --python Tools/Blender/render_environment_pack.py -- <source.fbx> <diffuse.png> <output.png>
"""

import math
import os
import sys

import bpy
from mathutils import Vector


def main():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) < 3:
        raise SystemExit("Expected FBX, diffuse texture and output image")
    source_path, texture_path, output_path = map(os.path.abspath, args[:3])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source_path)
    source = bpy.data.objects.get("model_LOD4")
    if source is None:
        source = next(
            (obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name.endswith("_LOD0")),
            None,
        )
    if source is None:
        raise RuntimeError("A model_LOD4 or *_LOD0 mesh was not found")

    for obj in list(bpy.data.objects):
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)

    material = bpy.data.materials.new("EnvironmentPreview")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(texture_path)
    material.node_tree.links.new(nodes.get("Principled BSDF").inputs["Base Color"], texture.outputs["Color"])
    source.data.materials.clear()
    source.data.materials.append(material)

    corners = [source.matrix_world @ Vector(corner) for corner in source.bound_box]
    minimum = Vector(tuple(min(corner[axis] for corner in corners) for axis in range(3)))
    maximum = Vector(tuple(max(corner[axis] for corner in corners) for axis in range(3)))
    center = (minimum + maximum) * 0.5
    maximum_dimension = max(maximum - minimum)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = maximum_dimension * 1.18
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.location = center + Vector((maximum_dimension * 0.85, -maximum_dimension * 1.7, maximum_dimension * 0.52))
    direction = center - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    scene.render.filepath = output_path
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED {output_path}")


if __name__ == "__main__":
    main()
