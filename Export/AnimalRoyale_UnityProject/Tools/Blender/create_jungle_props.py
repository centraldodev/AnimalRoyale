"""Exports reusable Blender environment props (Blender Z-up; Unity converts).

Props: JungleBush, JungleMountain, JungleFruit, JungleRock, JungleFlower.
Bushes and mountains use noise-displaced surfaces so nothing reads as a
perfect primitive; rocks are faceted boulders with a moss cap; the flower is
a real bloom with petals whose material Unity tints per patch.
"""
import bpy, math, os, random, sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from organic import displace_noise, material  # noqa: E402

OUT = os.path.abspath(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else os.path.abspath('JunglePropsOutput')
RNG = random.Random(23)


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


LEAF = material('Leaf', (.03, .36, .05), roughness=.75)
LIGHT = material('LeafLight', (.19, .60, .08), roughness=.75)
ROCK = material('Rock', (.27, .32, .31), roughness=.9)
ROCK2 = material('RockLight', (.43, .48, .45), roughness=.9)
MOSS = material('Moss', (.10, .42, .09), roughness=.85)
RED = material('FruitRed', (.94, .035, .015), roughness=.45)
GOLD = material('FruitGold', (1, .52, .025), roughness=.45)
STEM = material('Stem', (.10, .34, .025), roughness=.7)
PETAL = material('FlowerPetal', (1, .30, .55), roughness=.55)
PISTIL = material('FlowerCenter', (1, .78, .10), roughness=.5)


def finish(o, n, m, smooth=True):
    o.name = n
    o.data.materials.append(m)
    for p in o.data.polygons:
        p.use_smooth = smooth
    return o


def lump(n, p, s, m, strength=.3, freq=1.1, subdivisions=3):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=p)
    o = bpy.context.object
    o.scale = s
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    displace_noise(o, strength=strength, frequency=freq, seed=RNG.uniform(0, 40))
    return finish(o, n, m)


def crag(n, p, s, m, strength=.3, freq=1.0, seed=None, taper=0.0, subdivisions=2):
    """Faceted rock: displaced ico sphere with flat shading.

    taper narrows the shape toward its top, turning a boulder into a peak.
    """
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=p)
    o = bpy.context.object
    o.scale = s
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if taper > 0.0:
        z_values = [v.co.z for v in o.data.vertices]
        z_min, z_max = min(z_values), max(z_values)
        for v in o.data.vertices:
            t = (v.co.z - z_min) / (z_max - z_min)
            f = 1.0 - taper * t
            v.co.x *= f
            v.co.y *= f
    displace_noise(o, strength=strength, frequency=freq,
                   seed=RNG.uniform(0, 40) if seed is None else seed, flat=True)
    return finish(o, n, m, smooth=False)


def ellipse(n, p, s, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=p, rotation=rot)
    o = bpy.context.object
    o.scale = s
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, n, m)


def export(name):
    target = os.path.join(OUT, name)
    os.makedirs(target, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(target, name + '.fbx'), use_selection=True,
                             object_types={'MESH'}, mesh_smooth_type='FACE',
                             apply_scale_options='FBX_SCALE_UNITS')
    print('Created', os.path.join(target, name + '.fbx'))
    clear()


# --- JungleBush: lumpy foliage with leaf blades and a few blooms -----------
clear()
for p, s, m in [((0, 0, .75), (1.4, 1.15, .85), LEAF),
                ((.85, .2, .8), (1.0, .85, .7), LIGHT),
                ((-.85, .35, .78), (1.05, .9, .72), LEAF),
                ((.05, -.8, 1.05), (1.0, .85, .8), LIGHT),
                ((0, .1, 1.35), (.9, .75, .6), LEAF)]:
    lump('BushClump', p, s, m, strength=.3, freq=1.3)
for i in range(8):
    angle = i * math.pi * 2 / 8 + RNG.uniform(-.2, .2)
    direction = Vector((math.cos(angle), math.sin(angle), RNG.uniform(.15, .5))).normalized()
    tip = direction * RNG.uniform(1.5, 1.9) + Vector((0, 0, .7))
    ellipse('LeafBlade_%d' % i, tip, (.5, .16, .05),
            LIGHT if i % 2 else LEAF,
            rot=(0, RNG.uniform(-.5, -.2), angle))
for i in range(3):
    angle = RNG.uniform(0, math.pi * 2)
    ellipse('BushBloom_%d' % i,
            (math.cos(angle) * RNG.uniform(.5, 1.0), math.sin(angle) * RNG.uniform(.5, 1.0), RNG.uniform(1.3, 1.7)),
            (.11, .11, .06), PETAL)
export('JungleBush')

# --- JungleMountain: craggy tapered peak with vegetated lower slopes -------
main = crag('MountainCore', (0, 0, 2.7), (2.6, 2.3, 3.4), ROCK, strength=.45, freq=.75,
            taper=.72, subdivisions=3)
main.data.materials.append(ROCK2)
for polygon in main.data.polygons:
    if polygon.center.z > 4.3:
        polygon.material_index = 1
crag('SidePeak_0', (1.9, .5, 1.5), (1.4, 1.2, 2.1), ROCK, strength=.4, freq=.9, taper=.6)
crag('SidePeak_1', (-1.7, -.7, 1.2), (1.3, 1.15, 1.7), ROCK2, strength=.4, freq=.9, taper=.55)
crag('FootBoulder_0', (2.3, -1.2, .5), (.9, .8, .7), ROCK, strength=.3)
crag('FootBoulder_1', (-2.2, 1.4, .45), (.85, .75, .65), ROCK2, strength=.3)
for i in range(5):
    angle = i * math.pi * 2 / 5 + RNG.uniform(-.3, .3)
    radius = RNG.uniform(1.7, 2.4)
    lump('SlopeGreen_%d' % i,
         (math.cos(angle) * radius, math.sin(angle) * radius, RNG.uniform(.7, 1.6)),
         (RNG.uniform(.7, 1.1), RNG.uniform(.6, .95), RNG.uniform(.4, .6)),
         LEAF if i % 2 else MOSS, strength=.24, freq=1.4)
export('JungleMountain')

# --- JungleFruit: unchanged mango ------------------------------------------
ellipse('Mango', (0, 0, .35), (.34, .30, .48), GOLD)
ellipse('MangoBlush', (.14, .1, .38), (.20, .18, .27), RED)
bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=.035, depth=.28, location=(0, 0, .86))
finish(bpy.context.object, 'MangoStem', STEM)
export('JungleFruit')

# --- JungleRock: faceted mossy boulder --------------------------------------
crag('Boulder', (0, 0, .55), (1.25, 1.0, .78), ROCK, strength=.3, freq=1.1, seed=4.0)
crag('BoulderSide', (1.05, .35, .28), (.55, .45, .34), ROCK2, strength=.26, freq=1.3, seed=9.0)
lump('MossCap', (-.15, -.1, 1.05), (.75, .6, .28), MOSS, strength=.2, freq=1.6)
export('JungleRock')

# --- JungleFlower: stem, leaves, petal ring and center ----------------------
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=.024, radius2=.015, depth=.46, location=(0, 0, .23))
finish(bpy.context.object, 'FlowerStem', STEM)
for side in (-1, 1):
    ellipse('FlowerLeaf_%d' % side, (side * .10, .02 * side, .12), (.13, .05, .022),
            STEM, rot=(0, side * -.5, side * .6))
for i in range(6):
    angle = i * math.pi * 2 / 6
    ellipse('Petal_%d' % i,
            (math.cos(angle) * .105, math.sin(angle) * .105, .475),
            (.10, .055, .02), PETAL, rot=(0, -.42, angle))
ellipse('FlowerCenter', (0, 0, .48), (.055, .055, .04), PISTIL)
export('JungleFlower')
