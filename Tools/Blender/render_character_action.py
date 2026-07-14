"""Renders a quick front/three-quarter validation image from an opened character blend."""
import bpy
import math
import os
import sys
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1:]
if len(args) != 3:
    raise RuntimeError("Usage: <blend> --python render_character_action.py -- <action> <frame> <output.png>")
action_name, frame_text, output_path = args
frame = int(frame_text)

rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
if action_name not in bpy.data.actions:
    raise RuntimeError("Missing action " + action_name)
rig.animation_data_create()
rig.animation_data.action = bpy.data.actions[action_name]
bpy.context.scene.frame_set(frame)

# Source characters intentionally use Unity-style Y-up coordinates. Rotate all
# root objects as one group so the preview stands upright in Blender's Z-up view.
preview_root = bpy.data.objects.new("PreviewAxisCorrection", None)
bpy.context.scene.collection.objects.link(preview_root)
preview_root.rotation_euler.x = math.radians(90)
for obj in list(bpy.context.scene.objects):
    if obj != preview_root and obj.parent is None and obj.type in {"ARMATURE", "MESH"}:
        obj.parent = preview_root

world = bpy.context.scene.world
world.color = (0.035, 0.055, 0.08)
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.035, 0.055, 0.08, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.45

def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

bpy.ops.object.light_add(type="AREA", location=(3.5, -4.5, 6.0))
key = bpy.context.object
key.data.energy = 1050
key.data.shape = "DISK"
key.data.size = 5.0
look_at(key, (0, 0, 1.0))
bpy.ops.object.light_add(type="AREA", location=(-3.0, -1.5, 3.2))
fill = bpy.context.object
fill.data.energy = 700
fill.data.size = 4.0
look_at(fill, (0, 0, 1.0))

bpy.ops.object.camera_add(location=(3.8, -7.2, 3.0))
camera = bpy.context.object
camera.data.lens = 58
look_at(camera, (0, 0, 1.0))
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.render.filepath = os.path.abspath(output_path)
scene.render.image_settings.color_mode = "RGBA"
bpy.ops.render.render(write_still=True)
print("PREVIEW_RENDERED", action_name, frame, scene.render.filepath)
