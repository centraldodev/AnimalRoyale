import bpy
import math
import os
import random
from pathlib import Path
from mathutils import Vector, Quaternion


SEED = 7731
random.seed(SEED)

SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[2]
ART_ROOT = PROJECT_ROOT / "Assets/AnimalBattleRoyale/Art/Environment/HighDetailJungle"
RESOURCE_ROOT = PROJECT_ROOT / "Assets/AnimalBattleRoyale/Resources/EnvironmentModels"
PREVIEW_PATH = PROJECT_ROOT / "Assets/AnimalBattleRoyale/Art/Concepts/HighDetailJungleKitPreview.png"
BLEND_PATH = ART_ROOT / "Models/HighDetailJungleKit.blend"


def ensure_dirs():
    (ART_ROOT / "Models").mkdir(parents=True, exist_ok=True)
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    for name in ("JungleTreeHD", "BroadleafClusterHD", "MossyRockHD", "MountainSpireHD", "FlowerClusterHD"):
        (RESOURCE_ROOT / name).mkdir(parents=True, exist_ok=True)


def reset_file():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.name = "AssetSources"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    return scene


def material(name, color, roughness=0.72, metallic=0.0, noise_scale=4.0, noise_strength=0.12, emission=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission is not None:
        shader.inputs["Emission Color"].default_value = (*emission, 1.0)
        shader.inputs["Emission Strength"].default_value = 1.8

    texcoord = nodes.new("ShaderNodeTexCoord")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = noise_scale
    noise.inputs["Detail"].default_value = 3.0
    noise.inputs["Roughness"].default_value = 0.7
    ramp = nodes.new("ShaderNodeValToRGB")
    dark = tuple(max(0.0, c * (1.0 - noise_strength)) for c in color)
    light = tuple(min(1.0, c * (1.0 + noise_strength)) for c in color)
    ramp.color_ramp.elements[0].color = (*dark, 1.0)
    ramp.color_ramp.elements[1].color = (*light, 1.0)
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = noise_strength * 0.55
    bump.inputs["Distance"].default_value = 0.12
    links.new(texcoord.outputs["Generated"], noise.inputs["Vector"])
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return mat


def build_materials():
    return {
        "bark": material("HD_Bark", (0.23, 0.075, 0.018), 0.9, noise_scale=5.5, noise_strength=0.32),
        "bark_light": material("HD_BarkLight", (0.43, 0.17, 0.045), 0.86, noise_scale=7.0, noise_strength=0.28),
        "bark_dark": material("HD_BarkDark", (0.105, 0.027, 0.009), 0.94, noise_scale=6.0, noise_strength=0.22),
        "leaf": material("HD_Leaf", (0.025, 0.29, 0.035), 0.76, noise_scale=3.0, noise_strength=0.2),
        "leaf_mid": material("HD_LeafMid", (0.075, 0.49, 0.055), 0.72, noise_scale=3.5, noise_strength=0.18),
        "leaf_light": material("HD_LeafSunlit", (0.28, 0.67, 0.09), 0.7, noise_scale=4.0, noise_strength=0.16),
        "vine": material("HD_Vine", (0.035, 0.22, 0.025), 0.92, noise_scale=7.0, noise_strength=0.2),
        "moss": material("HD_Moss", (0.12, 0.42, 0.035), 0.97, noise_scale=8.0, noise_strength=0.24),
        "rock": material("HD_Rock", (0.26, 0.3, 0.28), 0.94, noise_scale=3.2, noise_strength=0.33),
        "rock_light": material("HD_RockLight", (0.46, 0.48, 0.41), 0.9, noise_scale=4.0, noise_strength=0.26),
        "earth": material("HD_Earth", (0.4, 0.205, 0.055), 0.96, noise_scale=8.0, noise_strength=0.25),
        "flower_red": material("HD_FlowerRed", (0.88, 0.045, 0.025), 0.66, noise_scale=2.0, noise_strength=0.1),
        "flower_gold": material("HD_FlowerGold", (1.0, 0.55, 0.035), 0.64, noise_scale=2.0, noise_strength=0.1),
        "flower_purple": material("HD_FlowerPurple", (0.47, 0.06, 0.78), 0.66, noise_scale=2.0, noise_strength=0.1),
        "flower_pink": material("HD_FlowerPink", (0.95, 0.12, 0.43), 0.66, noise_scale=2.0, noise_strength=0.1),
        "crystal_blue": material("HD_CrystalBlue", (0.02, 0.38, 0.95), 0.16, metallic=0.12, noise_scale=2.0, noise_strength=0.08, emission=(0.0, 0.18, 0.8)),
        "crystal_purple": material("HD_CrystalPurple", (0.38, 0.025, 0.92), 0.18, metallic=0.1, noise_scale=2.0, noise_strength=0.08, emission=(0.22, 0.0, 0.72)),
    }


def new_collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def move_to_collection(obj, coll):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)


def add_empty(name, coll):
    obj = bpy.data.objects.new(name, None)
    coll.objects.link(obj)
    return obj


def assign_material(obj, mat):
    if obj.type == "MESH":
        obj.data.materials.append(mat)


def cone_between(name, start, end, radius_start, radius_end, mat, coll, vertices=12, parent=None):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    length = direction.length
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius_start,
        radius2=radius_end,
        depth=length,
        end_fill_type="NGON",
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    assign_material(obj, mat)
    move_to_collection(obj, coll)
    obj.parent = parent
    bevel = obj.modifiers.new("Soft bark edges", "BEVEL")
    bevel.width = min(radius_start, radius_end) * 0.08
    bevel.segments = 2
    return obj


def create_leaf(name, position, direction, scale, mat, coll, parent=None, roll=0.0):
    verts = [
        (-0.58, 0.0, -0.28), (-0.42, 0.035, 0.15), (0.0, 0.08, 0.72),
        (0.42, 0.035, 0.15), (0.58, 0.0, -0.28), (0.0, 0.045, -0.7),
        (0.0, -0.025, 0.0),
    ]
    faces = [
        (6, 0, 1), (6, 1, 2), (6, 2, 3), (6, 3, 4), (6, 4, 5), (6, 5, 0),
        (1, 0, 6), (2, 1, 6), (3, 2, 6), (4, 3, 6), (5, 4, 6), (0, 5, 6),
    ]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    obj.location = position
    obj.scale = (scale * 0.62, scale * 0.36, scale)
    direction = Vector(direction).normalized()
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction)
    obj.rotation_quaternion = Quaternion(direction, roll) @ obj.rotation_quaternion
    assign_material(obj, mat)
    obj.parent = parent
    return obj


def create_vine(name, points, radius, mat, coll, parent=None):
    curve_data = bpy.data.curves.new(name + "Curve", "CURVE")
    curve_data.dimensions = "3D"
    curve_data.resolution_u = 2
    curve_data.bevel_depth = radius
    curve_data.bevel_resolution = 2
    curve_data.resolution_u = 2
    spline = curve_data.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve_data)
    coll.objects.link(obj)
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


def join_objects(objects, name):
    objects = [obj for obj in objects if obj is not None and obj.name in bpy.data.objects]
    if not objects:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def leaf_cloud(prefix, center, radii, count, mats, coll, parent, seed_offset=0):
    rng = random.Random(SEED + seed_offset)
    leaves = []
    center = Vector(center)
    for i in range(count):
        theta = rng.uniform(0.0, math.tau)
        phi = rng.uniform(-0.38, 0.78)
        radial = Vector((math.cos(theta) * math.cos(phi), math.sin(theta) * math.cos(phi), math.sin(phi)))
        distance = rng.uniform(0.38, 1.0)
        position = center + Vector((radial.x * radii[0], radial.y * radii[1], radial.z * radii[2])) * distance
        direction = (radial + Vector((0, 0, rng.uniform(0.18, 0.55)))).normalized()
        mat = mats[min(len(mats) - 1, int(rng.random() * len(mats)))]
        leaf = create_leaf(
            f"{prefix}_Leaf_{i:03d}", position, direction,
            rng.uniform(0.34, 0.7), mat, coll, parent, rng.uniform(-math.pi, math.pi)
        )
        leaves.append(leaf)
    return leaves


def create_canopy_clump(name, position, scale, mat, coll, parent, seed_offset=0):
    rng = random.Random(SEED + seed_offset)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1.0, location=position)
    clump = bpy.context.object
    clump.name = name
    move_to_collection(clump, coll)
    clump.parent = parent
    clump.scale = scale
    for vertex in clump.data.vertices:
        co = vertex.co
        ripple = 1.0 + math.sin(co.x * 8.1 + co.z * 5.7) * 0.045 + math.sin(co.y * 9.3) * 0.035
        ripple += rng.uniform(-0.018, 0.018)
        co *= ripple
    assign_material(clump, mat)
    for polygon in clump.data.polygons:
        polygon.use_smooth = True
    bevel = clump.modifiers.new("SoftCanopySurface", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 2
    return clump


def create_tree_asset(mats):
    coll = new_collection("JungleTreeHD_Asset")
    root = add_empty("JungleTreeHD", coll)
    wood = []
    leaves = []
    canopy_clumps = []

    trunk_points = [Vector((0, 0, 0)), Vector((0.08, -0.02, 2.2)), Vector((-0.12, 0.08, 4.25)), Vector((0.15, -0.05, 6.25))]
    radii = [(0.72, 0.58), (0.58, 0.44), (0.44, 0.28)]
    for i in range(3):
        wood.append(cone_between(f"Trunk_{i}", trunk_points[i], trunk_points[i + 1], radii[i][0], radii[i][1], mats["bark"], coll, 14, root))

    for i in range(9):
        angle = i * math.tau / 9 + random.uniform(-0.18, 0.18)
        length = random.uniform(2.0, 3.25)
        end = Vector((math.cos(angle) * length, math.sin(angle) * length, random.uniform(0.05, 0.32)))
        wood.append(cone_between(f"ButtressRoot_{i}", (0, 0, 0.3), end, 0.34, 0.06, mats["bark_dark"], coll, 10, root))

    branch_specs = [
        ((0.0, 0.0, 4.6), (-2.8, 0.5, 6.2), (-4.3, 1.0, 6.75)),
        ((0.0, 0.0, 4.9), (2.5, -0.8, 6.45), (4.1, -1.45, 6.9)),
        ((0.05, 0.0, 5.35), (0.8, 2.5, 7.0), (1.1, 4.0, 7.2)),
        ((0.05, 0.0, 5.65), (-0.6, -2.35, 7.05), (-1.1, -3.85, 7.4)),
        ((0.1, 0.0, 6.0), (-1.5, 1.2, 7.5), (-2.6, 2.0, 8.0)),
        ((0.1, 0.0, 6.15), (1.65, 1.2, 7.45), (2.8, 2.0, 7.8)),
    ]
    canopy_centers = []
    for i, (start, middle, end) in enumerate(branch_specs):
        wood.append(cone_between(f"Branch_{i}_A", start, middle, 0.3, 0.18, mats["bark"], coll, 12, root))
        wood.append(cone_between(f"Branch_{i}_B", middle, end, 0.19, 0.07, mats["bark_light"], coll, 10, root))
        canopy_centers.append(Vector(end))

    for i, center in enumerate(canopy_centers):
        canopy_clumps.append(create_canopy_clump(
            f"CanopyVolume_{i}", center + Vector((0, 0, 0.05)),
            (2.0, 1.7, 1.22),
            [mats["leaf"], mats["leaf_mid"], mats["leaf_light"]][i % 3], coll, root, 700 + i))
        leaves.extend(leaf_cloud(f"Canopy_{i}", center, (2.15, 1.85, 1.4), 86,
                                 [mats["leaf"], mats["leaf_mid"], mats["leaf_light"]], coll, root, 100 + i))
    top_centers = [(-1.3, 0.1, 8.45), (1.15, 0.2, 8.55), (0.0, -1.1, 8.35), (0.1, 1.15, 8.5), (0, 0, 9.15)]
    for i, center in enumerate(top_centers):
        canopy_clumps.append(create_canopy_clump(
            f"TopCanopyVolume_{i}", center, (1.65, 1.5, 1.1),
            [mats["leaf_mid"], mats["leaf"], mats["leaf_light"]][i % 3], coll, root, 730 + i))
    leaves.extend(leaf_cloud("CanopyTop", (0.0, 0.0, 8.65), (3.1, 2.75, 1.85), 190,
                             [mats["leaf"], mats["leaf_mid"], mats["leaf_light"]], coll, root, 200))

    for i in range(7):
        angle = i * math.tau / 7 + 0.35
        radius = random.uniform(1.7, 3.5)
        x = math.cos(angle) * radius
        y = math.sin(angle) * radius
        start_z = random.uniform(6.4, 8.1)
        length = random.uniform(2.0, 4.4)
        points = [
            (x, y, start_z),
            (x + random.uniform(-0.35, 0.35), y + random.uniform(-0.35, 0.35), start_z - length * 0.45),
            (x + random.uniform(-0.45, 0.45), y + random.uniform(-0.45, 0.45), start_z - length),
        ]
        create_vine(f"HangingVine_{i}", points, random.uniform(0.035, 0.065), mats["vine"], coll, root)

    join_objects(wood, "DetailedTrunkAndRoots")
    join_objects(leaves, "LayeredIndividualLeaves")
    join_objects(canopy_clumps, "OrganicCanopyVolumes")
    return coll, root


def create_broadleaf_asset(mats):
    coll = new_collection("BroadleafClusterHD_Asset")
    root = add_empty("BroadleafClusterHD", coll)
    leaves = []
    create_canopy_clump("BroadleafCore", (0, 0, 0.42), (0.82, 0.78, 0.38), mats["leaf"], coll, root, 810)
    for ring, (count, radius, height) in enumerate(((12, 0.5, 0.2), (16, 0.9, 0.32), (18, 1.3, 0.42))):
        for i in range(count):
            angle = i * math.tau / count + ring * 0.37 + random.uniform(-0.08, 0.08)
            direction = Vector((math.cos(angle), math.sin(angle), random.uniform(0.25, 0.62))).normalized()
            position = direction * radius
            position.z = height + random.uniform(-0.08, 0.12)
            leaves.append(create_leaf(
                f"BroadLeaf_{ring}_{i}", position, direction,
                random.uniform(0.55, 0.95),
                random.choice([mats["leaf"], mats["leaf_mid"], mats["leaf_light"]]),
                coll, root, random.uniform(-0.4, 0.4)
            ))
    join_objects(leaves, "BroadleafRosette")
    return coll, root


def create_rock_asset(mats):
    coll = new_collection("MossyRockHD_Asset")
    root = add_empty("MossyRockHD", coll)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4, radius=1.0, location=(0, 0, 0.85))
    rock = bpy.context.object
    rock.name = "WeatheredRock"
    move_to_collection(rock, coll)
    rock.parent = root
    rock.scale = (1.35, 1.05, 0.9)
    for vertex in rock.data.vertices:
        co = vertex.co
        variation = 1.0 + 0.13 * math.sin(co.x * 7.1 + co.y * 4.3) + 0.08 * math.sin(co.z * 9.7)
        co *= variation
    rock.data.materials.append(mats["rock"])
    rock.data.materials.append(mats["rock_light"])
    for polygon in rock.data.polygons:
        polygon.material_index = 1 if polygon.center.z > 0.55 and polygon.normal.z > 0.25 else 0
        polygon.use_smooth = True
    bevel = rock.modifiers.new("RoundedWeathering", "BEVEL")
    bevel.width = 0.045
    bevel.segments = 3

    moss_objects = []
    for i in range(7):
        angle = i * math.tau / 7 + random.uniform(-0.25, 0.25)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0,
                                             location=(math.cos(angle) * random.uniform(0.35, 0.9),
                                                       math.sin(angle) * random.uniform(0.25, 0.72),
                                                       random.uniform(1.28, 1.66)))
        moss = bpy.context.object
        moss.name = f"MossPatch_{i}"
        moss.scale = (random.uniform(0.32, 0.65), random.uniform(0.25, 0.52), random.uniform(0.035, 0.09))
        assign_material(moss, mats["moss"])
        move_to_collection(moss, coll)
        moss.parent = root
        moss_objects.append(moss)
    join_objects(moss_objects, "LayeredMossPatches")
    return coll, root


def irregular_spire(name, location, radius, height, mats, coll, parent, sides=11, levels=6):
    vertices = []
    faces = []
    rng = random.Random(hash(name) & 0xFFFF)
    for level in range(levels):
        t = level / (levels - 1)
        current_radius = radius * ((1.0 - t) ** 0.72) * (1.0 + rng.uniform(-0.08, 0.08))
        z = height * t
        center_shift = Vector((math.sin(t * 5.2) * radius * 0.08, math.cos(t * 4.1) * radius * 0.06, 0))
        for side in range(sides):
            angle = side * math.tau / sides
            wobble = 1.0 + 0.12 * math.sin(side * 2.17 + level * 1.31) + rng.uniform(-0.05, 0.05)
            vertices.append(tuple(Vector(location) + center_shift + Vector((math.cos(angle) * current_radius * wobble,
                                                                            math.sin(angle) * current_radius * wobble, z))))
    for level in range(levels - 1):
        for side in range(sides):
            nxt = (side + 1) % sides
            a = level * sides + side
            b = level * sides + nxt
            c = (level + 1) * sides + nxt
            d = (level + 1) * sides + side
            faces.append((a, b, c, d))
    faces.append(tuple(range(sides - 1, -1, -1)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(mats["rock"])
    obj.data.materials.append(mats["rock_light"])
    for polygon in obj.data.polygons:
        center_z = polygon.center.z - location[2]
        if center_z > height * 0.58 and polygon.index % 4 == 0:
            polygon.material_index = 1
        else:
            polygon.material_index = 0
        polygon.use_smooth = True
    bevel = obj.modifiers.new("WeatheredEdges", "BEVEL")
    bevel.width = 0.08
    bevel.segments = 2
    return obj


def create_mountain_asset(mats):
    coll = new_collection("MountainSpireHD_Asset")
    root = add_empty("MountainSpireHD", coll)
    irregular_spire("MountainCore", (0, 0, 0), 3.5, 11.5, mats, coll, root, 24, 14)
    irregular_spire("SidePeakLeft", (-3.0, 0.6, 0), 2.3, 7.4, mats, coll, root, 20, 12)
    irregular_spire("SidePeakRight", (2.7, -0.7, 0), 2.0, 6.5, mats, coll, root, 20, 12)
    irregular_spire("FootBoulderA", (-1.8, -2.0, 0), 1.8, 3.0, mats, coll, root, 18, 9)
    irregular_spire("FootBoulderB", (2.0, 1.8, 0), 1.65, 2.7, mats, coll, root, 18, 9)

    moss_terraces = []
    terrace_specs = [
        ((-1.35, -0.4, 3.05), (1.75, 1.15, 0.16)),
        ((1.0, 0.25, 5.15), (1.5, 1.0, 0.13)),
        ((-0.35, 0.05, 7.5), (1.15, 0.82, 0.11)),
        ((-3.0, 0.55, 3.65), (1.0, 0.72, 0.1)),
        ((2.7, -0.65, 2.85), (0.95, 0.65, 0.1)),
    ]
    for i, (position, scale) in enumerate(terrace_specs):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=position)
        terrace = bpy.context.object
        terrace.name = f"NaturalMossTerrace_{i}"
        terrace.scale = scale
        assign_material(terrace, mats["moss"])
        move_to_collection(terrace, coll)
        terrace.parent = root
        for polygon in terrace.data.polygons:
            polygon.use_smooth = True
        moss_terraces.append(terrace)
    join_objects(moss_terraces, "NaturalMossTerraces")

    ledge_leaves = []
    for i, center in enumerate(((-1.8, 0.2, 4.6), (1.2, -0.3, 6.7), (0.3, 0.4, 8.8), (2.3, 0.1, 3.4))):
        ledge_leaves.extend(leaf_cloud(f"MountainPlant_{i}", center, (0.9, 0.75, 0.45), 13,
                                       [mats["leaf"], mats["leaf_mid"], mats["leaf_light"]], coll, root, 430 + i))
    join_objects(ledge_leaves, "MountainLedgeVegetation")
    return coll, root


def create_flower_asset(mats):
    coll = new_collection("FlowerClusterHD_Asset")
    root = add_empty("FlowerClusterHD", coll)
    parts = []
    palette = [mats["flower_red"], mats["flower_gold"], mats["flower_purple"], mats["flower_pink"]]
    for i in range(9):
        angle = i * 2.4
        radius = 0.12 + 0.08 * i
        x = math.cos(angle) * radius
        y = math.sin(angle) * radius
        height = random.uniform(0.28, 0.75)
        parts.append(cone_between(f"FlowerStem_{i}", (x, y, 0), (x * 1.08, y * 1.08, height), 0.025, 0.015,
                                  mats["vine"], coll, 8, root))
        petal_mat = palette[i % len(palette)]
        for p in range(7):
            petal_angle = p * math.tau / 7
            bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6,
                                                 location=(x + math.cos(petal_angle) * 0.13,
                                                           y + math.sin(petal_angle) * 0.13,
                                                           height))
            petal = bpy.context.object
            petal.name = f"Petal_{i}_{p}"
            petal.scale = (0.12, 0.055, 0.045)
            petal.rotation_euler = (0, 0, petal_angle)
            assign_material(petal, petal_mat)
            move_to_collection(petal, coll)
            petal.parent = root
            parts.append(petal)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=0.09, location=(x, y, height + 0.015))
        center_obj = bpy.context.object
        center_obj.name = f"FlowerCenter_{i}"
        assign_material(center_obj, mats["flower_gold"])
        move_to_collection(center_obj, coll)
        center_obj.parent = root
        parts.append(center_obj)
    join_objects(parts, "DetailedFlowerPatch")
    return coll, root


def select_collection_objects(coll):
    bpy.ops.object.select_all(action="DESELECT")
    selected = []
    for obj in coll.all_objects:
        obj.select_set(True)
        selected.append(obj)
    if selected:
        bpy.context.view_layer.objects.active = selected[0]


def export_asset(coll, asset_name):
    output = RESOURCE_ROOT / asset_name / f"{asset_name}.fbx"
    select_collection_objects(coll)
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"MESH", "EMPTY", "OTHER"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )
    print(f"EXPORTED {asset_name}: {output}")


def add_collection_instance(scene, coll, name, location, scale=1.0, rotation_z=0.0):
    obj = bpy.data.objects.new(name, None)
    obj.instance_type = "COLLECTION"
    obj.instance_collection = coll
    obj.location = location
    obj.scale = (scale, scale, scale)
    obj.rotation_euler.z = rotation_z
    scene.collection.objects.link(obj)
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_preview(asset_collections, mats):
    scene = bpy.data.scenes.new("HighDetailJunglePreview")
    bpy.context.window.scene = scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.use_nodes = True
    compositor = scene.node_tree
    compositor.nodes.clear()
    render_layers = compositor.nodes.new("CompositorNodeRLayers")
    glow = compositor.nodes.new("CompositorNodeGlare")
    glow.glare_type = "FOG_GLOW"
    glow.quality = "HIGH"
    glow.threshold = 1.05
    glow.size = 6
    composite = compositor.nodes.new("CompositorNodeComposite")
    compositor.links.new(render_layers.outputs["Image"], glow.inputs["Image"])
    compositor.links.new(glow.outputs["Image"], composite.inputs["Image"])

    world = bpy.data.worlds.new("PreviewJungleWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.025, 0.32, 0.92, 1.0)
    bg.inputs["Strength"].default_value = 0.72

    bpy.ops.mesh.primitive_plane_add(size=45, location=(0, 4, -0.04))
    ground = bpy.context.object
    ground.name = "PreviewGrassGround"
    ground.data.materials.append(mats["moss"])

    path_vertices = []
    path_faces = []
    path_segments = 20
    for i in range(path_segments + 1):
        t = i / path_segments
        y = -7.0 + t * 31.0
        center_x = math.sin(t * math.pi * 2.15 - 0.55) * (1.1 - t * 0.55)
        width = 3.6 - t * 2.05
        path_vertices.extend(((center_x - width, y, 0.018), (center_x + width, y, 0.018)))
    for i in range(path_segments):
        start = i * 2
        path_faces.append((start, start + 1, start + 3, start + 2))
    path_mesh = bpy.data.meshes.new("CurvingJunglePathMesh")
    path_mesh.from_pydata(path_vertices, [], path_faces)
    path_mesh.update()
    path = bpy.data.objects.new("CurvingJunglePath", path_mesh)
    scene.collection.objects.link(path)
    path.data.materials.append(mats["earth"])

    tree_coll, broad_coll, rock_coll, mountain_coll, flower_coll = asset_collections
    add_collection_instance(scene, mountain_coll, "Mountain_Back", (0, 15.5, 0), 1.28, 0.15)
    add_collection_instance(scene, mountain_coll, "Mountain_Left", (-10.5, 20, -0.2), 0.76, -0.25)
    add_collection_instance(scene, mountain_coll, "Mountain_Right", (11, 21, -0.3), 0.78, 0.35)
    add_collection_instance(scene, tree_coll, "HeroTree_Left", (-7.0, 1.8, 0), 1.34, 0.2)
    add_collection_instance(scene, tree_coll, "HeroTree_Right", (7.2, 3.8, 0), 1.28, -0.45)
    add_collection_instance(scene, tree_coll, "Tree_Mid", (-8.8, 12.8, 0), 0.82, 0.6)
    add_collection_instance(scene, tree_coll, "Tree_MidRight", (8.6, 14.2, 0), 0.76, -0.2)
    for i in range(14):
        side = -1 if i % 2 == 0 else 1
        y = random.uniform(8.0, 24.0)
        x = side * random.uniform(5.0, 13.5)
        add_collection_instance(scene, tree_coll, f"LayeredTree_{i}", (x, y, 0), random.uniform(0.4, 0.68), random.uniform(0, math.tau))
    for i in range(34):
        side = -1 if i % 2 == 0 else 1
        y = random.uniform(-1.5, 14.0)
        x = side * random.uniform(2.8, 9.0)
        add_collection_instance(scene, broad_coll, f"Broadleaf_{i}", (x, y, 0), random.uniform(0.55, 1.3), random.uniform(0, math.tau))
    for i in range(22):
        side = -1 if i % 2 == 0 else 1
        add_collection_instance(scene, rock_coll, f"Rock_{i}", (side * random.uniform(2.4, 8.8), random.uniform(-2, 14), 0),
                                random.uniform(0.45, 1.2), random.uniform(0, math.tau))
    for i in range(36):
        side = -1 if i % 2 == 0 else 1
        add_collection_instance(scene, flower_coll, f"Flowers_{i}", (side * random.uniform(2.2, 10.0), random.uniform(-2.5, 13.5), 0.02),
                                random.uniform(0.42, 0.9), random.uniform(0, math.tau))

    water_mat = material("PreviewWaterfall", (0.08, 0.52, 1.0), 0.16, noise_scale=10.0, noise_strength=0.22,
                         emission=(0.02, 0.18, 0.72))
    bpy.ops.mesh.primitive_cube_add(location=(0.05, 11.9, 5.55), scale=(0.72, 0.08, 3.45))
    waterfall = bpy.context.object
    waterfall.name = "DistantWaterfall"
    waterfall.data.materials.append(water_mat)
    bevel = waterfall.modifiers.new("WaterfallSoftEdges", "BEVEL")
    bevel.width = 0.16
    bevel.segments = 5
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=20, location=(0.05, 10.9, 1.62))
    waterfall_pool = bpy.context.object
    waterfall_pool.name = "WaterfallPool"
    waterfall_pool.scale = (2.5, 1.15, 0.12)
    waterfall_pool.data.materials.append(water_mat)

    for i in range(420):
        side = -1 if i % 2 == 0 else 1
        y = random.uniform(-3.0, 15.0)
        x = side * random.uniform(2.15, 10.5)
        height = random.uniform(0.12, 0.36)
        bpy.ops.mesh.primitive_cone_add(vertices=5, radius1=random.uniform(0.035, 0.075), radius2=0,
                                       depth=height, location=(x, y, height * 0.5))
        blade = bpy.context.object
        blade.name = f"PreviewGrassBlade_{i}"
        blade.data.materials.append(mats["leaf_light"] if i % 5 == 0 else mats["leaf_mid"])

    for i in range(14):
        side = -1 if i % 2 == 0 else 1
        height = random.uniform(0.45, 1.5)
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=random.uniform(0.16, 0.34), radius2=0,
                                       depth=height, location=(side * random.uniform(2.7, 9.0), random.uniform(-1.0, 13.0), height * 0.5))
        crystal = bpy.context.object
        crystal.name = f"PreviewCrystal_{i}"
        crystal.data.materials.append(mats["crystal_blue"] if i % 3 else mats["crystal_purple"])

    bpy.ops.object.light_add(type="SUN", location=(0, 0, 18))
    sun = bpy.context.object
    sun.name = "WarmJungleSun"
    sun.data.energy = 3.65
    sun.data.color = (1.0, 0.72, 0.42)
    sun.rotation_euler = (math.radians(35), math.radians(-18), math.radians(-28))
    sun.data.angle = math.radians(12)

    bpy.ops.object.light_add(type="AREA", location=(-8, -3, 12))
    fill = bpy.context.object
    fill.data.energy = 1750
    fill.data.shape = "DISK"
    fill.data.size = 9
    fill.data.color = (0.32, 0.62, 1.0)
    look_at(fill, (0, 6, 2))

    for i in range(7):
        x = -12 + i * 4.0
        z = random.uniform(11.5, 15.5)
        for puff in range(4):
            bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12,
                                                 location=(x + puff * 0.75, 24 + i * 0.3, z + math.sin(puff) * 0.35))
            cloud = bpy.context.object
            cloud.name = f"Cloud_{i}_{puff}"
            cloud.scale = (1.35, 0.65, 0.7)
            cloud.data.materials.append(material("PreviewCloud", (0.94, 0.97, 1.0), 0.9, noise_strength=0.03))

    bpy.ops.object.camera_add(location=(0.0, -20.5, 6.25))
    camera = bpy.context.object
    camera.data.lens = 45
    camera.data.sensor_width = 36
    look_at(camera, (0, 8.0, 3.3))
    scene.camera = camera

    scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED PREVIEW: {PREVIEW_PATH}")


def main():
    ensure_dirs()
    reset_file()
    mats = build_materials()
    tree = create_tree_asset(mats)
    broadleaf = create_broadleaf_asset(mats)
    rock = create_rock_asset(mats)
    mountain = create_mountain_asset(mats)
    flower = create_flower_asset(mats)
    assets = [tree, broadleaf, rock, mountain, flower]
    names = ["JungleTreeHD", "BroadleafClusterHD", "MossyRockHD", "MountainSpireHD", "FlowerClusterHD"]
    for (coll, _), name in zip(assets, names):
        export_asset(coll, name)
    create_preview([item[0] for item in assets], mats)


if __name__ == "__main__":
    main()
