# Inspeciona a grade de numeros: separa partes soltas e reporta centroides.
# Uso: blender --background --python inspect_grid.py -- <fbx> <lod>
import bpy, sys
from mathutils import Vector
argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
fbx = argv[0]; lod = argv[1] if len(argv) > 1 else "model_LOD3"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
base = None
for o in [o for o in bpy.data.objects if o.type == "MESH"]:
    if lod in o.name and base is None: base = o
    else: bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT'); base.select_set(True)
bpy.context.view_layer.objects.active = base
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.mesh.separate(type='LOOSE')
parts = [o for o in bpy.context.selected_objects if o.type == "MESH"]
def c(o):
    bb=[o.matrix_world @ Vector(v) for v in o.bound_box]
    return (sum(v.x for v in bb)/8, sum(v.y for v in bb)/8, sum(v.z for v in bb)/8)
cs=[c(o) for o in parts]
print(f"[Grid] partes soltas: {len(parts)}")
xs=[p[0] for p in cs]; ys=[p[1] for p in cs]; zs=[p[2] for p in cs]
print(f"[Grid] X {min(xs):.2f}..{max(xs):.2f}  Y {min(ys):.2f}..{max(ys):.2f}  Z {min(zs):.2f}..{max(zs):.2f}")
for i,p in enumerate(sorted(cs, key=lambda p:(-p[2],p[0]))):
    print(f"[Grid] part x={p[0]:.2f} y={p[1]:.2f} z={p[2]:.2f}")
