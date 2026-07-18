"""Convert a Tripo GLB tiger into Unity-friendly FBX and texture assets.

Usage:
  Blender --background --factory-startup --python Tools/Blender/prepare_tripo_tiger.py -- \
    tiger3D_Triplo.glb \
    Assets/AnimalBattleRoyale/Art/Characters/Tiger/Models/Tiger_Tripo.fbx \
    Assets/AnimalBattleRoyale/Art/Characters/Tiger/Textures/Tiger_Tripo_BaseColor.png \
    [optional-animation-source.glb-or-fbx]
"""

import sys
import re
from pathlib import Path

import bpy


def arguments():
    argv = sys.argv
    args = argv[argv.index("--") + 1 :] if "--" in argv else []
    if len(args) not in {3, 4}:
        raise SystemExit(
            "Expected: -- <source.glb-or-fbx> <output.fbx> <basecolor.png> "
            "[animation-source.glb-or-fbx]"
        )
    source, fbx_output, texture_output = args[:3]
    animation_source = Path(args[3]).resolve() if len(args) == 4 else None
    return (
        Path(source).resolve(),
        Path(fbx_output).resolve(),
        Path(texture_output).resolve(),
        animation_source,
    )


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def find_base_color_image():
    for material in bpy.data.materials:
        if not material.use_nodes or material.node_tree is None:
            continue
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image is not None:
                return node.image
    return next((image for image in bpy.data.images if image.type == "IMAGE"), None)


def import_model(path):
    suffix = path.suffix.lower()
    if suffix in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=str(path), import_pack_images=True)
    elif suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    else:
        raise ValueError(f"Unsupported source format: {suffix}")


def main():
    source, fbx_output, texture_output, animation_source = arguments()
    if not source.exists():
        raise FileNotFoundError(source)
    if animation_source is not None and not animation_source.exists():
        raise FileNotFoundError(animation_source)

    fbx_output.parent.mkdir(parents=True, exist_ok=True)
    texture_output.parent.mkdir(parents=True, exist_ok=True)
    clear_scene()
    import_model(source)

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(armatures) != 1 or not meshes:
        raise RuntimeError(f"Expected one armature and at least one mesh; got {len(armatures)} and {len(meshes)}")

    armature = armatures[0]
    armature.name = "Tiger_Tripo_Armature"
    armature.data.name = "Tiger_Tripo_Armature"
    for index, mesh in enumerate(meshes, start=1):
        mesh.name = "Tiger_Tripo_Mesh" if len(meshes) == 1 else f"Tiger_Tripo_Mesh_{index:02d}"
        mesh.data.name = mesh.name

    base_color = find_base_color_image()
    if base_color is None:
        raise RuntimeError("No base-color image found in the source model")
    base_color.filepath_raw = str(texture_output)
    base_color.file_format = "PNG"
    base_color.save()

    # A separately generated Tripo model may have the same UniRig hierarchy but
    # no clips. Importing the prior animated rig lets Blender bake its pose
    # deltas against the new visual armature during FBX export.
    visual_objects = set(bpy.context.scene.objects)
    if animation_source is not None:
        import_model(animation_source)
        animation_objects = set(bpy.context.scene.objects) - visual_objects
        for obj in animation_objects:
            bpy.data.objects.remove(obj, do_unlink=True)

    # Tripo currently exports generic NlaTrack names. Stable names make Unity
    # sub-assets deterministic while retaining every supplied animation. Rename
    # in two passes to avoid Blender resolving temporary name collisions.
    actions = list(bpy.data.actions)
    ordered_actions = []
    for fallback_index, action in enumerate(sorted(actions, key=lambda item: item.name), start=1):
        match = re.search(r"Clip_(\d+)_", action.name)
        source_index = int(match.group(1)) if match else fallback_index
        ordered_actions.append((source_index, action))
        action.name = f"__TigerTripoTemp_{fallback_index:02d}"

    ordered_actions.sort(key=lambda item: item[0])
    for index, (_, action) in enumerate(ordered_actions, start=1):
        frame_count = round(action.frame_range[1] - action.frame_range[0] + 1)
        action.name = f"Tiger_Tripo_Clip_{index:02d}_{frame_count}f"
        action.use_fake_user = True

    if ordered_actions:
        armature.animation_data_create()
        first_action = ordered_actions[0][1]
        armature.animation_data.action = first_action
        if first_action.slots:
            armature.animation_data.action_slot = first_action.slots[0]

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = armature

    bpy.ops.export_scene.fbx(
        filepath=str(fbx_output),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",
        embed_textures=False,
    )

    print("TRIPO_TIGER_EXPORT")
    print(f"FBX: {fbx_output}")
    print(f"Texture: {texture_output}")
    print("Actions:", [action.name for action in bpy.data.actions])


if __name__ == "__main__":
    main()
