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

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.context.view_layer.update()

# Frame every species by its evaluated bounds rather than using one distant
# camera that made the monkey and ant unreadably small.
corners = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue
    corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
minimum = Vector((min(point.x for point in corners), min(point.y for point in corners), min(point.z for point in corners)))
maximum = Vector((max(point.x for point in corners), max(point.y for point in corners), max(point.z for point in corners)))
center = (minimum + maximum) * 0.5
extent = max(maximum.x - minimum.x, maximum.y - minimum.y, maximum.z - minimum.z)
direction = Vector((0.9, -2.45, 0.72)).normalized()
bpy.ops.object.camera_add(location=center + direction * extent * 2.35)
camera = bpy.context.object
camera.data.lens = 62
look_at(camera, center + Vector((0, 0, extent * 0.03)))
bpy.context.scene.camera = camera

bpy.ops.mesh.primitive_plane_add(size=extent * 7.0, location=(center.x, center.y, minimum.z - .025))
ground = bpy.context.object
ground_material = bpy.data.materials.new("PreviewGround")
ground_material.diffuse_color = (0.035, 0.11, 0.045, 1)
ground.data.materials.append(ground_material)

scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.render.filepath = os.path.abspath(output_path)
scene.render.image_settings.color_mode = "RGBA"
bpy.ops.render.render(write_still=True)
print("PREVIEW_RENDERED", action_name, frame, scene.render.filepath)
