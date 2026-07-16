# Blender headless model inspector
# Uso: blender --background --python inspect_model.py -- <caminho_do_modelo>
# Importa GLB/GLTF/FBX/OBJ e reporta estatisticas uteis para avaliar qualidade.

import bpy
import sys
import os

def get_arg():
    argv = sys.argv
    if "--" in argv:
        extra = argv[argv.index("--") + 1:]
        return extra[0] if extra else None
    return None

def clean_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def import_model(path):
    ext = os.path.splitext(path)[1].lower()
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise RuntimeError(f"Extensao nao suportada: {ext}")

def report():
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    total_tris = 0
    total_verts = 0
    mats = set()
    for m in meshes:
        me = m.data
        me.calc_loop_triangles()
        total_tris += len(me.loop_triangles)
        total_verts += len(me.vertices)
        for s in m.material_slots:
            if s.material:
                mats.add(s.material.name)

    imgs = [i for i in bpy.data.images if i.name not in ("Render Result", "Viewer Node")]

    # bounding box global
    import mathutils
    mn = [1e9, 1e9, 1e9]
    mx = [-1e9, -1e9, -1e9]
    for m in meshes:
        for corner in m.bound_box:
            world = m.matrix_world @ mathutils.Vector(corner)
            for i in range(3):
                mn[i] = min(mn[i], world[i])
                mx[i] = max(mx[i], world[i])
    dims = [round(mx[i] - mn[i], 3) for i in range(3)] if meshes else [0, 0, 0]

    print("\n================ RELATORIO DO MODELO ================")
    print(f"Objetos mesh:      {len(meshes)}  -> {[o.name for o in meshes]}")
    print(f"Esqueletos (rig):  {len(arms)}  -> {[o.name for o in arms]}")
    print(f"Vertices totais:   {total_verts:,}")
    print(f"Triangulos totais: {total_tris:,}")
    print(f"Materiais ({len(mats)}):   {sorted(mats)}")
    print(f"Texturas ({len(imgs)}):    {[(i.name, tuple(i.size)) for i in imgs]}")
    print(f"Dimensoes (X,Y,Z): {dims}  (unidades Blender)")
    print("=====================================================\n")

    # alertas uteis
    if total_tris > 100000:
        print("[AVISO] Malha pesada (>100k tris) - vai precisar de decimate/retopo p/ jogo.")
    if len(meshes) > 1:
        print(f"[INFO] {len(meshes)} meshes separados - talvez precise juntar/limpar.")
    if not imgs:
        print("[AVISO] Nenhuma textura encontrada - modelo pode estar sem PBR.")

def main():
    path = get_arg()
    if not path or not os.path.exists(path):
        print(f"[ERRO] Arquivo nao encontrado: {path}")
        return
    print(f"Inspecionando: {path}")
    clean_scene()
    import_model(path)
    report()

if __name__ == "__main__":
    main()
