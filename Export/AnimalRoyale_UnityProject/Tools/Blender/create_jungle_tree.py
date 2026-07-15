"""Creates a reusable stylized jungle tree for Unity (Blender Z-up).

The canopy is made of noise-displaced lumpy clumps instead of perfect spheres,
the trunk leans and tapers, and the buttress roots flare naturally — a much
more believable tree that keeps the saturated cartoon palette.
"""
import bpy, math, os, random, sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from organic import displace_noise, material  # noqa: E402

OUT = os.path.abspath(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else os.path.abspath('JungleTreeOutput')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)

BARK = material('Jungle_Bark', (.27, .075, .014), roughness=.85)
BARK_LIGHT = material('Jungle_BarkLight', (.46, .16, .032), roughness=.85)
LEAF = material('Jungle_Leaf', (.025, .35, .052), roughness=.75)
LEAF_LIGHT = material('Jungle_LeafLight', (.17, .60, .075), roughness=.75)
VINE = material('Jungle_Vine', (.025, .23, .03), roughness=.7)

RNG = random.Random(7)


def finish(o, n, m):
    o.name = n
    o.data.materials.append(m)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def limb(n, a, b, r1, r2, m):
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=r1, radius2=r2, depth=d.length, location=(a + b) * .5)
    o = bpy.context.object
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(o, n, m)


def clump(n, p, s, m, strength=.32, freq=1.1):
    """A lumpy foliage mass: displaced ico sphere, reads like massed leaves."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=p)
    o = bpy.context.object
    o.scale = s
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    displace_noise(o, strength=strength, frequency=freq, seed=RNG.uniform(0, 40))
    return finish(o, n, m)


# Leaning, tapering trunk built from three stacked segments.
trunk_path = [(0, 0, 0), (.10, .05, 1.9), (.28, .12, 3.6), (.42, .16, 5.2)]
radii = [.52, .42, .33, .26]
for i in range(3):
    limb('TreeTrunk_%d' % i, trunk_path[i], trunk_path[i + 1], radii[i], radii[i + 1], BARK)

# Flared buttress roots around the base.
for i in range(6):
    angle = i * math.pi * 2 / 6 + RNG.uniform(-.25, .25)
    reach = RNG.uniform(1.15, 1.75)
    limb('ButtressRoot_%d' % i, (math.cos(angle) * .22, math.sin(angle) * .22, .95),
         (math.cos(angle) * reach, math.sin(angle) * reach, .04), .22, .09, BARK_LIGHT)

# Branches reach from the upper trunk into the canopy.
top = Vector(trunk_path[-1])
for i, (dx, dy, dz) in enumerate(((1.7, .2, 1.0), (-1.75, .45, 1.2), (.25, -1.75, 1.4), (-.2, 1.5, .8), (1.1, -1.1, .9))):
    start = Vector(trunk_path[2]) + Vector((0, 0, RNG.uniform(-.3, .5)))
    limb('Branch_%d' % i, start, top + Vector((dx, dy, dz)) * .9, .19, .07, BARK_LIGHT)

# Canopy: a dome of lumpy clumps with a dark core so it never sees through.
clump('CanopyCore', (top.x, top.y, 6.1), (2.1, 1.9, 1.5), LEAF, strength=.22)
for i in range(8):
    angle = i * math.pi * 2 / 8 + RNG.uniform(-.2, .2)
    radius = RNG.uniform(1.35, 1.8)
    p = (top.x + math.cos(angle) * radius, top.y + math.sin(angle) * radius, RNG.uniform(5.4, 6.2))
    s = (RNG.uniform(1.15, 1.6), RNG.uniform(1.0, 1.45), RNG.uniform(.85, 1.15))
    clump('CanopyClump_%d' % i, p, s, LEAF_LIGHT if i % 2 else LEAF)
for i in range(3):
    angle = RNG.uniform(0, math.pi * 2)
    p = (top.x + math.cos(angle) * .9, top.y + math.sin(angle) * .9, 7.15 + RNG.uniform(-.15, .3))
    clump('CanopyTop_%d' % i, p, (RNG.uniform(1.0, 1.4), RNG.uniform(.9, 1.2), RNG.uniform(.7, .95)),
          LEAF if i % 2 else LEAF_LIGHT)
# Under-canopy tufts hanging below the crown edge.
for i in range(4):
    angle = i * math.pi / 2 + .5
    p = (top.x + math.cos(angle) * 1.9, top.y + math.sin(angle) * 1.9, 4.9)
    clump('UnderTuft_%d' % i, p, (.7, .6, .45), LEAF, strength=.26)

# Hanging vines.
limb('HangingVine_0', (top.x + 1.1, top.y + .2, 6.0), (top.x + 1.3, top.y + .3, 2.7), .05, .03, VINE)
limb('HangingVine_1', (top.x - .9, top.y + .8, 5.8), (top.x - .85, top.y + .9, 3.2), .04, .025, VINE)

os.makedirs(OUT, exist_ok=True)
SOURCE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Sources')
os.makedirs(SOURCE_DIR, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE_DIR, 'JungleTree_Source.blend'))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'JungleTree.fbx'), use_selection=True,
                         object_types={'MESH'}, add_leaf_bones=False, mesh_smooth_type='FACE',
                         apply_scale_options='FBX_SCALE_UNITS')
print('Created', os.path.join(OUT, 'JungleTree.fbx'))
