# Reporta triangulos por LOD de um FBX do Rodin
# Uso: blender --background --python lod_report.py -- <arquivo.fbx>
import bpy, sys, os

def get_arg():
    argv = sys.argv
    if "--" in argv:
        e = argv[argv.index("--")+1:]
        return e[0] if e else None
    return None

path = get_arg()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
print("\n===== TRIANGULOS POR LOD =====")
for o in sorted([o for o in bpy.data.objects if o.type=="MESH"], key=lambda x:x.name):
    me = o.data
    me.calc_loop_triangles()
    print(f"{o.name:12s}: {len(me.loop_triangles):>10,} tris  |  {len(me.vertices):>10,} verts")
print("==============================\n")
