# Extrai os 10 numeros da grade de contagem, centralizados, como FBXs individuais.
# Uso: blender --background --python extract_countdown.py -- <fbx> <out_dir>
import bpy, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
fbx, out_dir = argv[0], argv[1]
os.makedirs(out_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
base = None
for o in [o for o in bpy.data.objects if o.type == "MESH"]:
    if "model_LOD4" in o.name and base is None: base = o
    else: bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.object.select_all(action='DESELECT'); base.select_set(True)
bpy.context.view_layer.objects.active = base
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.mesh.separate(type='LOOSE')
parts = [o for o in bpy.context.selected_objects if o.type == "MESH"]

def centroid(o):
    bb = [o.matrix_world @ Vector(v) for v in o.bound_box]
    return Vector((sum(v.x for v in bb)/8, sum(v.y for v in bb)/8, sum(v.z for v in bb)/8))

data = [(o, centroid(o)) for o in parts]
zs = sorted(c.z for _, c in data)
zmid = (min(zs) + max(zs)) / 2
top = sorted([d for d in data if d[1].z >= zmid], key=lambda d: d[1].x)
bottom = sorted([d for d in data if d[1].z < zmid], key=lambda d: d[1].x)
mapping = {}
for i, n in enumerate([10, 9, 8, 7, 6]):
    if i < len(top): mapping[n] = top[i][0]
for i, n in enumerate([5, 4, 3, 2, 1]):
    if i < len(bottom): mapping[n] = bottom[i][0]

print(f"[Cd] top={len(top)} bottom={len(bottom)} mapped={len(mapping)}")
for n in range(1, 11):
    obj = mapping.get(n)
    if obj is None:
        print(f"[Cd] MISSING {n}"); continue
    # centralizar no origem
    bb = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    center = sum(bb, Vector()) / 8
    obj.location -= center
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, f"Count_{n}.fbx"),
                             use_selection=True, apply_unit_scale=True)
    print(f"[Cd] exported Count_{n}.fbx")
