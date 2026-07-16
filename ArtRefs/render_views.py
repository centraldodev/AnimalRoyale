# Renderiza um FBX em 3 vistas ortograficas (Workbench) para inspecionar orientacao.
# Uso: blender --background --python render_views.py -- <fbx> <out_dir>
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
fbx, out_dir = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)

# usar apenas o mesh mais leve (LOD4) para velocidade
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
keep = None
for o in meshes:
    if "LOD4" in o.name: keep = o
for o in meshes:
    o.hide_render = (keep is not None and o is not keep)

# bounding box global do objeto visivel
target = keep or meshes[0]
bb = [target.matrix_world @ Vector(c) for c in target.bound_box]
mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
center = (mn + mx) / 2
size = mx - mn
print(f"[Render] size={tuple(round(s,3) for s in size)} center={tuple(round(c,3) for c in center)}")
maxdim = max(size)

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.film_transparent = False
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'SINGLE'
scene.display.shading.single_color = (0.9, 0.55, 0.2)

cam_data = bpy.data.cameras.new("Cam")
cam_data.type = 'ORTHO'
cam_data.ortho_scale = maxdim * 1.2
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

def render(view, loc_offset, rot_euler):
    cam.location = center + Vector(loc_offset) * (maxdim * 2)
    cam.rotation_euler = rot_euler
    scene.render.filepath = os.path.join(out_dir, f"blender_{view}.png")
    bpy.ops.render.render(write_still=True)
    print(f"[Render] wrote blender_{view}.png")

# Blender: Z=up, Y=depth(-Y frente), X=lado
render("front", (0, -1, 0), (math.radians(90), 0, 0))          # olhando +Y
render("side",  (1, 0, 0),  (math.radians(90), 0, math.radians(90)))  # olhando -X
render("top",   (0, 0, 1),  (0, 0, 0))                          # olhando -Z
