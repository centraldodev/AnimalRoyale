"""Render a contact sheet used to visually verify the ranged-combat FBX assets."""
import bpy
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(sys.argv[sys.argv.index('--') + 1])
OUTPUT = os.path.abspath(sys.argv[sys.argv.index('--') + 2])
NAMES = ['BananaProjectile', 'RockProjectile', 'EagleDropping', 'BananaBunch', 'StonePile', 'EagleNest']

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

positions = [(-4.0, 1.9), (0.0, 1.9), (4.0, 1.9), (-4.0, -2.0), (0.0, -2.0), (4.0, -2.0)]
for name, (target_x, target_y) in zip(NAMES, positions):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, name, name + '.fbx'))
    imported = [obj for obj in bpy.context.scene.objects if obj not in before and obj.type == 'MESH']
    corners = []
    for obj in imported:
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    center_x = (min(point.x for point in corners) + max(point.x for point in corners)) * 0.5
    center_y = (min(point.y for point in corners) + max(point.y for point in corners)) * 0.5
    min_z = min(point.z for point in corners)
    offset = Vector((target_x - center_x, target_y - center_y, -min_z + 0.08))
    roots = [obj for obj in imported if obj.parent not in imported]
    for obj in roots:
        obj.location += offset

bpy.ops.mesh.primitive_plane_add(size=2, location=(0, 0, 0))
ground = bpy.context.object
ground.scale = (7.2, 5.2, 1)
ground_mat = bpy.data.materials.new('PreviewGround')
ground_mat.diffuse_color = (0.025, 0.045, 0.04, 1)
ground.data.materials.append(ground_mat)

world = bpy.context.scene.world
world.color = (0.02, 0.035, 0.03)

def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

bpy.ops.object.camera_add(location=(9.2, -15.5, 10.2))
camera = bpy.context.object
point_at(camera, (0, 0, 0.7))
camera.data.lens = 52
bpy.context.scene.camera = camera

for location, energy, size, color in [((-6, -6, 10), 1150, 6, (1.0, 0.78, 0.54)),
                                      ((7, 1, 7), 850, 5, (0.45, 0.72, 1.0))]:
    bpy.ops.object.light_add(type='AREA', location=location)
    light = bpy.context.object
    light.data.energy = energy
    light.data.shape = 'DISK'
    light.data.size = size
    light.data.color = color
    point_at(light, (0, 0, 0))

scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1200
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = OUTPUT
scene.render.film_transparent = False
scene.view_settings.look = 'AgX - Medium High Contrast'
bpy.ops.render.render(write_still=True)
print('Preview rendered to', OUTPUT)
