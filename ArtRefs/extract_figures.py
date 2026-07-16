# Separa um FBX 3-em-1 do Rodin em 3 figuras (por posicao X), exporta cada uma
# como FBX unico e renderiza a vista frontal de cada para identificacao.
# Uso: blender --background --python extract_figures.py -- <fbx_in> <out_dir> <source_lod>
#   source_lod: nome do LOD a usar como base (ex.: model_LOD2). Default model_LOD2.
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
fbx_in = argv[0]
out_dir = argv[1]
source_lod = argv[2] if len(argv) > 2 else "model_LOD2"
os.makedirs(out_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_in)

# manter apenas o LOD escolhido
base = None
for o in [o for o in bpy.data.objects if o.type == "MESH"]:
    if source_lod in o.name and base is None:
        base = o
    else:
        bpy.data.objects.remove(o, do_unlink=True)
if base is None:
    print("[Extract] ERRO: LOD nao encontrado:", source_lod); sys.exit(1)

# aplicar transform e separar por partes soltas
bpy.ops.object.select_all(action='DESELECT')
base.select_set(True)
bpy.context.view_layer.objects.active = base
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.mesh.separate(type='LOOSE')
parts = [o for o in bpy.context.selected_objects if o.type == "MESH"]
print(f"[Extract] partes soltas: {len(parts)}")

# centroides X de cada parte
def centroid_x(o):
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return sum(v.x for v in bb) / 8.0

xs = [centroid_x(o) for o in parts]
xmin, xmax = min(xs), max(xs)
span = (xmax - xmin) or 1.0
# 3 bandas por X: 0=esquerda,1=meio,2=direita
def band(x):
    t = (x - xmin) / span
    return 0 if t < 1/3 else (1 if t < 2/3 else 2)

clusters = {0: [], 1: [], 2: []}
for o, x in zip(parts, xs):
    clusters[band(x)].append(o)

# juntar cada cluster e exportar
def join_and_export(objs, idx):
    if not objs:
        print(f"[Extract] cluster {idx} vazio"); return
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    fig = bpy.context.active_object
    fig.name = f"figure_{idx}"
    # recentrar no origem, apoiado no chao
    bb = [fig.matrix_world @ Vector(c) for c in fig.bound_box]
    mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
    mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    center = (mn + mx) / 2
    fig.location -= Vector((center.x, center.y, mn.z))
    bpy.ops.object.select_all(action='DESELECT')
    fig.select_set(True)
    bpy.context.view_layer.objects.active = fig
    out_fbx = os.path.join(out_dir, f"figure_{idx}.fbx")
    bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, apply_unit_scale=True)
    print(f"[Extract] exportado {out_fbx}  (tris aprox {len(fig.data.polygons)})")
    return fig

# render simples de cada figura (front) para identificar
def render_front(fig, idx):
    for o in bpy.data.objects:
        o.hide_render = (o != fig)
    bb = [fig.matrix_world @ Vector(c) for c in fig.bound_box]
    mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
    mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    center = (mn + mx) / 2
    maxd = max(mx - mn)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.render.resolution_x = sc.render.resolution_y = 400
    cam_data = bpy.data.cameras.new(f"c{idx}"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = maxd * 1.2
    cam = bpy.data.objects.new(f"c{idx}", cam_data); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = center + Vector((0, -1, 0)) * (maxd * 2)
    cam.rotation_euler = (math.radians(90), 0, 0)
    sc.render.filepath = os.path.join(out_dir, f"figure_{idx}_front.png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)

figs = []
for i in range(3):
    f = join_and_export(clusters[i], i)
    if f: figs.append((i, f))
for i, f in figs:
    render_front(f, i)
    print(f"[Extract] render figure_{i}_front.png")
