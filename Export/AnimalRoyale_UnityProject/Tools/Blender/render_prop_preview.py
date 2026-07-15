"""Renders a validation image of one or more environment FBX files.

Usage:
  Blender --background --python render_prop_preview.py -- <output.png> <a.fbx> [b.fbx ...]

Props are laid out along +X in import order (Z-up assets render as-is).
"""
import bpy
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
output_path, fbx_paths = args[0], args[1:]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

cursor_x = 0.0
for fbx_path in fbx_paths:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(fbx_path))
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    min_x = min((obj.location.x + obj.bound_box[0][0] * obj.scale.x) for obj in imported)
    max_x = max((obj.location.x + obj.bound_box[6][0] * obj.scale.x) for obj in imported)
    shift = cursor_x - min_x
    for obj in imported:
        if obj.parent is None:
            obj.location.x += shift
    cursor_x += (max_x - min_x) + 1.5

world = bpy.context.scene.world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.035, 0.055, 0.08, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


center = Vector((cursor_x * 0.5, 0, 2.2))
bpy.ops.object.light_add(type="AREA", location=(center.x + 6, -9, 11))
key = bpy.context.object
key.data.energy = 5200
key.data.size = 9.0
look_at(key, center)
bpy.ops.object.light_add(type="AREA", location=(center.x - 7, -4, 5))
fill = bpy.context.object
fill.data.energy = 2400
fill.data.size = 7.0
look_at(fill, center)

distance = max(cursor_x * 1.05, 9.0)
bpy.ops.object.camera_add(location=(center.x + distance * 0.35, -distance, center.z + distance * 0.32))
camera = bpy.context.object
camera.data.lens = 46
look_at(camera, center)
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 960
scene.render.resolution_y = 540
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = os.path.abspath(output_path)
bpy.ops.render.render(write_still=True)
print("PROPS_RENDERED", scene.render.filepath)
