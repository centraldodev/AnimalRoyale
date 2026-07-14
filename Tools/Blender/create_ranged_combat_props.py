"""Create the ranged-combat projectiles and ammunition pickups for Unity."""
import bpy
import math
import os
import random
import sys
from mathutils import Vector


RESOURCE_OUT = os.path.abspath(sys.argv[sys.argv.index('--') + 1])
SOURCE_OUT = os.path.abspath(sys.argv[sys.argv.index('--') + 2])
RNG = random.Random(42)


def material(name, color, roughness=0.7):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = next((node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is not None:
        bsdf.inputs['Base Color'].default_value = (*color, 1.0)
        bsdf.inputs['Roughness'].default_value = roughness
    return mat


YELLOW = material('BananaYellow', (1.0, 0.62, 0.025), 0.48)
YELLOW_LIGHT = material('BananaHighlight', (1.0, 0.86, 0.16), 0.42)
STEM = material('BananaStem', (0.18, 0.11, 0.025), 0.82)
ROCK = material('RiverStone', (0.26, 0.31, 0.30), 0.92)
ROCK_LIGHT = material('RiverStoneLight', (0.46, 0.51, 0.48), 0.88)
MOSS = material('StoneMoss', (0.12, 0.38, 0.08), 0.9)
BROWN = material('EagleDropping', (0.25, 0.12, 0.045), 0.9)
BROWN_LIGHT = material('EagleDroppingLight', (0.48, 0.28, 0.11), 0.84)
NEST = material('NestFiber', (0.42, 0.25, 0.08), 0.94)
LEAF = material('NestLeaf', (0.1, 0.42, 0.08), 0.86)


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def assign(obj, mat, smooth=True):
    obj.data.materials.append(mat)
    if hasattr(obj.data, 'polygons'):
        for polygon in obj.data.polygons:
            polygon.use_smooth = smooth
    return obj


def sphere(name, location, scale, mat, subdivisions=2, smooth=True):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return assign(obj, mat, smooth)


def cylinder(name, location, radius, depth, mat, rotation=(0, 0, 0), vertices=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, mat, False)


def banana(name, location=(0, 0, 0), scale=1.0, rotation=(0, 0, 0)):
    curve_data = bpy.data.curves.new(name + 'Curve', 'CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = 2
    curve_data.bevel_depth = 0.105 * scale
    curve_data.bevel_resolution = 2
    spline = curve_data.splines.new('BEZIER')
    spline.bezier_points.add(3)
    points = [(-0.58, 0, 0.02), (-0.26, 0, 0.27), (0.22, 0, 0.29), (0.58, 0, 0.04)]
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = Vector(coordinate) * scale
        point.handle_left_type = 'AUTO'
        point.handle_right_type = 'AUTO'
    obj = bpy.data.objects.new(name, curve_data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    curve_data.materials.append(YELLOW)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj = bpy.context.object
    assign(obj, YELLOW_LIGHT)
    for polygon in obj.data.polygons:
        if polygon.center.z > 0.18 * scale:
            polygon.material_index = 1
    end_a = Vector((-0.62 * scale, 0, 0.02 * scale))
    end_b = Vector((0.62 * scale, 0, 0.04 * scale))
    for index, end in enumerate((end_a, end_b)):
        cap = sphere(name + '_Tip_' + str(index), end, Vector((0.075, 0.075, 0.085)) * scale, STEM, subdivisions=1, smooth=False)
        cap.parent = obj
        cap.location = end
    return obj


def rock(name, location=(0, 0, 0), scale=1.0, light=False):
    obj = sphere(name, location, (0.46 * scale, 0.36 * scale, 0.32 * scale), ROCK_LIGHT if light else ROCK, subdivisions=2, smooth=False)
    obj.rotation_euler = (RNG.uniform(-0.3, 0.3), RNG.uniform(-0.4, 0.4), RNG.uniform(-0.3, 0.3))
    for vertex in obj.data.vertices:
        factor = 1.0 + RNG.uniform(-0.12, 0.12)
        vertex.co *= factor
    return obj


def dropping(name, location=(0, 0, 0), scale=1.0):
    sphere(name + '_Base', location, (0.34 * scale, 0.30 * scale, 0.24 * scale), BROWN, subdivisions=2)
    sphere(name + '_Middle', Vector(location) + Vector((0.05, 0, 0.24)) * scale,
           (0.25 * scale, 0.23 * scale, 0.20 * scale), BROWN_LIGHT, subdivisions=2)
    sphere(name + '_Top', Vector(location) + Vector((-0.03, 0, 0.42)) * scale,
           (0.15 * scale, 0.14 * scale, 0.17 * scale), BROWN, subdivisions=2)


def export_asset(name):
    source_dir = os.path.join(SOURCE_OUT, name)
    resource_dir = os.path.join(RESOURCE_OUT, name)
    os.makedirs(source_dir, exist_ok=True)
    os.makedirs(resource_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_dir, name + '_Source.blend'))
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(resource_dir, name + '.fbx'),
        use_selection=True,
        object_types={'MESH'},
        mesh_smooth_type='FACE',
        apply_scale_options='FBX_SCALE_UNITS')
    clear()


clear()
banana('BananaProjectile')
export_asset('BananaProjectile')

rock('RockProjectile')
sphere('RockMoss', (-0.08, 0.02, 0.29), (0.20, 0.13, 0.055), MOSS, subdivisions=2)
export_asset('RockProjectile')

dropping('EagleDropping')
export_asset('EagleDropping')

for i in range(6):
    angle = i * math.pi * 2 / 6
    root = banana('BunchBanana_' + str(i), (math.cos(angle) * 0.24, math.sin(angle) * 0.24, 0.24), 0.72,
                  (RNG.uniform(-0.2, 0.2), RNG.uniform(-0.2, 0.2), angle))
    root.rotation_euler.z = angle
cylinder('BunchStem', (0, 0, 0.62), 0.09, 0.52, STEM, vertices=9)
export_asset('BananaBunch')

for i, data in enumerate([
        ((-0.32, -0.12, 0.21), 0.9, False), ((0.26, -0.18, 0.18), 0.78, True),
        ((0.02, 0.22, 0.27), 1.05, False), ((0.42, 0.23, 0.13), 0.62, True),
        ((-0.42, 0.25, 0.12), 0.58, False)]):
    rock('PileStone_' + str(i), data[0], data[1], data[2])
export_asset('StonePile')

for i in range(14):
    angle = i * math.pi * 2 / 14
    radius = 0.62 + (i % 2) * 0.08
    cylinder('NestTwig_' + str(i), (math.cos(angle) * radius, math.sin(angle) * radius, 0.14),
             0.045, 1.2, NEST, rotation=(0, math.pi * 0.5, angle + math.pi * 0.5), vertices=7)
for i in range(5):
    angle = i * math.pi * 2 / 5
    leaf = sphere('NestLeaf_' + str(i), (math.cos(angle) * 0.58, math.sin(angle) * 0.58, 0.08),
                  (0.35, 0.11, 0.035), LEAF, subdivisions=2)
    leaf.rotation_euler.z = angle
for i in range(3):
    dropping('NestSupply_' + str(i), ((i - 1) * 0.28, 0.04 * (i % 2), 0.28), 0.62)
export_asset('EagleNest')

print('Ranged combat assets created in', RESOURCE_OUT)
