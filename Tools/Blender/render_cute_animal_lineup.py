"""Render the four production characters together for art-direction review."""
import bpy
import math
from pathlib import Path
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
PREVIEW = ROOT / "Assets/AnimalBattleRoyale/Art/Concepts/CuteAnimalRedesignPreview.png"
LINEUP_BLEND = ROOT / "Assets/AnimalBattleRoyale/Art/Characters/CuteAnimalLineup.blend"
CHARACTER_ROOT = ROOT / "Assets/AnimalBattleRoyale/Art/Characters"


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def append_character(name, x, scale):
    source = CHARACTER_ROOT / name / "Models" / f"{name}_Source.blend"
    with bpy.data.libraries.load(str(source), link=False) as (available, loaded):
        loaded.objects = list(available.objects)
        loaded.actions = [action for action in available.actions if action == f"{name}_Idle"]

    group = bpy.data.objects.new(name + "_LineupRoot", None)
    bpy.context.scene.collection.objects.link(group)
    group.location = (x, 0, 0)
    group.rotation_euler.x = math.radians(90)
    group.scale = (scale, scale, scale)
    for obj in loaded.objects:
        if obj is None:
            continue
        bpy.context.scene.collection.objects.link(obj)
        if obj.parent is None:
            obj.parent = group
        if obj.type == "ARMATURE":
            action = bpy.data.actions.get(f"{name}_Idle")
            if action is not None:
                obj.animation_data_create()
                obj.animation_data.action = action
    return group


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(PREVIEW)
scene.view_settings.look = "AgX - Medium High Contrast"

world = bpy.data.worlds.new("LineupWorld")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.19, 0.46, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.42
scene.world = world

append_character("Eagle", -3.15, 1.10)
append_character("Ant", -1.20, 1.22)
append_character("Monkey", 0.95, 1.04)
append_character("Tiger", 3.25, 1.04)
scene.frame_set(1)

bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -0.04))
ground = bpy.context.object
ground.name = "SoftJungleGround"
ground_material = bpy.data.materials.new("LineupGround")
ground_material.diffuse_color = (0.055, 0.28, 0.045, 1)
ground_material.use_nodes = True
ground_shader = ground_material.node_tree.nodes.get("Principled BSDF")
ground_shader.inputs["Base Color"].default_value = (0.055, 0.28, 0.045, 1)
ground_shader.inputs["Roughness"].default_value = 0.92
ground.data.materials.append(ground_material)

bpy.ops.object.light_add(type="AREA", location=(-4.5, -5.0, 7.5))
key = bpy.context.object
key.name = "WarmKey"
key.data.energy = 1750
key.data.shape = "DISK"
key.data.size = 5.5
key.data.color = (1.0, 0.74, 0.48)
look_at(key, (0, 0, 1.0))

bpy.ops.object.light_add(type="AREA", location=(5.5, -2.0, 4.8))
fill = bpy.context.object
fill.name = "CoolFill"
fill.data.energy = 1150
fill.data.size = 5.0
fill.data.color = (0.32, 0.62, 1.0)
look_at(fill, (0, 0, 1.15))

bpy.ops.object.light_add(type="AREA", location=(0, 4.0, 5.5))
rim = bpy.context.object
rim.name = "Rim"
rim.data.energy = 1350
rim.data.size = 4.0
rim.data.color = (0.58, 1.0, 0.34)
look_at(rim, (0, 0, 1.0))

bpy.ops.object.camera_add(location=(0, -12.8, 3.5))
camera = bpy.context.object
camera.data.lens = 58
look_at(camera, (0, 0, 1.05))
scene.camera = camera

PREVIEW.parent.mkdir(parents=True, exist_ok=True)
LINEUP_BLEND.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(LINEUP_BLEND))
bpy.ops.render.render(write_still=True)
print("LINEUP_RENDERED", PREVIEW)
