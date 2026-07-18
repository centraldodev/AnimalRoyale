"""Create clean Blender master files from the rigged FBXs currently used by Unity.

Usage:
  Blender --background --factory-startup \
    --python Tools/Blender/create_animal_master_blends.py

Each output keeps the imported armature, skin weights and packed base-color image,
but intentionally contains no Actions, NLA tracks or keyed animation data.
"""

from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[2]
CHARACTER_ROOT = PROJECT_ROOT / "Assets/AnimalBattleRoyale/Art/Characters"
OUTPUT_ROOT = PROJECT_ROOT / "Tools/Blender/Sources/Animals"
ANIMALS = ("Tiger", "Monkey", "Eagle", "Ant")


def clear_file():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for collection in list(bpy.data.collections):
        if collection.users == 0:
            bpy.data.collections.remove(collection)

    for datablocks in (
        bpy.data.meshes,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.actions,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def remove_all_animation():
    for obj in bpy.data.objects:
        if obj.animation_data is not None:
            obj.animation_data_clear()
        if obj.type == "ARMATURE":
            obj.data.pose_position = "POSE"
            for pose_bone in obj.pose.bones:
                pose_bone.location = (0.0, 0.0, 0.0)
                pose_bone.rotation_mode = "QUATERNION"
                pose_bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
                pose_bone.scale = (1.0, 1.0, 1.0)

    for shape_key in bpy.data.shape_keys:
        if shape_key.animation_data is not None:
            shape_key.animation_data_clear()

    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)


def install_packed_material(animal, texture_path, meshes):
    image = bpy.data.images.load(str(texture_path), check_existing=False)
    image.name = f"{animal}_BaseColor"
    image.pack()

    material = bpy.data.materials.new(f"{animal}_Master_Material")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "Packed Base Color"
    texture.label = "Packed Base Color"
    texture.image = image
    texture.interpolation = "Linear"
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    principled.inputs["Metallic"].default_value = 0.0
    principled.inputs["Roughness"].default_value = 0.72

    for mesh in meshes:
        mesh.data.materials.clear()
        mesh.data.materials.append(material)

    # FBX files can retain orphaned image paths beside the model. They are not
    # part of the master material and would make Blender's Pack All fail even
    # though the correct texture above is already embedded.
    for old_material in list(bpy.data.materials):
        if old_material != material and old_material.users == 0:
            bpy.data.materials.remove(old_material)
    for old_image in list(bpy.data.images):
        if old_image != image and old_image.users == 0:
            bpy.data.images.remove(old_image)


def validate_rig(animal, armatures, meshes):
    bone_count = sum(len(armature.data.bones) for armature in armatures)
    skinned_meshes = []
    for mesh in meshes:
        armature_modifiers = [modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"]
        if armature_modifiers and len(mesh.vertex_groups) > 0:
            skinned_meshes.append(mesh)

    errors = []
    if not armatures:
        errors.append("no armature")
    if bone_count == 0:
        errors.append("no bones")
    if not meshes:
        errors.append("no meshes")
    if len(skinned_meshes) != len(meshes):
        errors.append(f"only {len(skinned_meshes)}/{len(meshes)} meshes are skinned")
    if bpy.data.actions:
        errors.append(f"{len(bpy.data.actions)} old actions remain")
    if errors:
        raise RuntimeError(f"{animal}: " + "; ".join(errors))

    return bone_count, len(skinned_meshes)


def create_master(animal):
    model_path = CHARACTER_ROOT / animal / "Models" / f"{animal}3D_Rigged.fbx"
    texture_path = CHARACTER_ROOT / animal / "Textures" / f"{animal}3D_BaseColor.png"
    output_path = OUTPUT_ROOT / f"{animal}_Rig_Master.blend"
    if not model_path.is_file():
        raise FileNotFoundError(model_path)
    if not texture_path.is_file():
        raise FileNotFoundError(texture_path)

    clear_file()
    bpy.ops.import_scene.fbx(filepath=str(model_path), use_anim=True)

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    remove_all_animation()
    install_packed_material(animal, texture_path, meshes)

    for armature in armatures:
        armature.show_in_front = True
        armature["ABR_Rig_Master"] = True
        armature["ABR_Source_FBX"] = model_path.relative_to(PROJECT_ROOT).as_posix()

    scene = bpy.context.scene
    scene.name = f"{animal}_Rig_Master"
    scene["ABR_Character"] = animal
    scene["ABR_Animation_State"] = "Clean rig - no authored actions"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.unit_settings.system = "METRIC"
    scene.frame_start = 1
    scene.frame_end = 250

    bone_count, skinned_count = validate_rig(animal, armatures, meshes)
    bpy.ops.wm.save_as_mainfile(filepath=str(output_path), compress=True)

    print(
        f"MASTER_BLEND_VALID {animal}: {len(armatures)} armature(s), "
        f"{bone_count} bones, {skinned_count} skinned mesh(es), "
        f"0 actions, packed texture -> {output_path}"
    )


def main():
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    for animal in ANIMALS:
        create_master(animal)


if __name__ == "__main__":
    main()
