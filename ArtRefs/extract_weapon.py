# Extrai somente o LOD4 da arma, preservando UVs e orientação para o Unity.
# Uso: blender --background --python extract_weapon.py -- <fonte.fbx> <destino.fbx>
import bpy
import os
import sys


args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
source, destination = args[0], args[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)

meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
weapon = next((obj for obj in meshes if "LOD4" in obj.name), None)
if weapon is None:
    raise RuntimeError("LOD4 da arma não encontrado em " + source)

for obj in list(bpy.data.objects):
    if obj is not weapon:
        bpy.data.objects.remove(obj, do_unlink=True)

weapon.name = "SeedLauncher"
weapon.data.name = "SeedLauncher_Mesh"
bpy.ops.object.select_all(action="DESELECT")
weapon.select_set(True)
bpy.context.view_layer.objects.active = weapon

os.makedirs(os.path.dirname(destination), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=destination,
    use_selection=True,
    apply_unit_scale=True,
    add_leaf_bones=False,
    bake_anim=False,
)
print("[Weapon] LOD4 exportado -> " + destination)
