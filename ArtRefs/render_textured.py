# Renderiza um FBX com uma textura diffuse aplicada (Workbench TEXTURE), vista frontal.
# Uso: blender --background --python render_textured.py -- <fbx> <diffuse_png> <out_png>
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
fbx, diffuse, out_png = argv[0], argv[1], argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
obj = meshes[0]
for m in meshes[1:]:
    pass

# material com textura
mat = bpy.data.materials.new("Tex")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
img = mat.node_tree.nodes.new("ShaderNodeTexImage")
img.image = bpy.data.images.load(os.path.abspath(diffuse))
mat.node_tree.links.new(bsdf.inputs["Base Color"], img.outputs["Color"])
for o in meshes:
    o.data.materials.clear()
    o.data.materials.append(mat)

# bounds
allbb = []
for o in meshes:
    allbb += [o.matrix_world @ Vector(c) for c in o.bound_box]
mn = Vector((min(v.x for v in allbb), min(v.y for v in allbb), min(v.z for v in allbb)))
mx = Vector((max(v.x for v in allbb), max(v.y for v in allbb), max(v.z for v in allbb)))
center = (mn + mx) / 2
maxd = max(mx - mn)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = sc.render.resolution_y = 480
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'

cam_data = bpy.data.cameras.new("Cam"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = maxd * 1.2
cam = bpy.data.objects.new("Cam", cam_data); sc.collection.objects.link(cam); sc.camera = cam
cam.location = center + Vector((0, -1, 0)) * (maxd * 2)
cam.rotation_euler = (math.radians(90), 0, 0)
sc.render.filepath = os.path.abspath(out_png)
bpy.ops.render.render(write_still=True)
print("[Tex] wrote", out_png)
