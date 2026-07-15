"""Shared helpers that turn the primitive-based characters into organic ones.

The characters keep their saturated cartoon palette, but the main body is now a
single continuous mesh (metaball surface converted to a mesh) skinned with
automatic weights. That removes the "toy made of parts" look: shoulders flow
into legs, the neck blends into the chest and limbs taper like real anatomy.

All coordinates follow the project convention: Y up, +Z forward (the Unity
importer applies the axis correction when instancing the prefab).
"""

import bpy
from mathutils import Vector


TARGET_BODY_TRIANGLES = 28000


def material(name, color, roughness=0.55, metallic=0.0, coat=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        principled = next(
            (node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
            None)
    if principled is None:
        principled = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        output = next(
            (node for node in mat.node_tree.nodes if node.type == "OUTPUT_MATERIAL"),
            None)
        if output is None:
            output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
        mat.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    if coat > 0.0 and "Coat Weight" in principled.inputs:
        principled.inputs["Coat Weight"].default_value = coat
    # Give skin, fur, chitin and feathers a real micro-surface in the Blender
    # source. Glossy eyes and claws stay optically clean.
    if roughness >= 0.4:
        texcoord = mat.node_tree.nodes.new("ShaderNodeTexCoord")
        noise = mat.node_tree.nodes.new("ShaderNodeTexNoise")
        if name.startswith("Ant_"):
            noise_scale, bump_strength, bump_distance = 5.5, 0.045, 0.012
        elif name.startswith("Eagle_"):
            noise_scale, bump_strength, bump_distance = 24.0, 0.07, 0.009
        else:
            noise_scale, bump_strength, bump_distance = 34.0, 0.065, 0.008
        noise.inputs["Scale"].default_value = noise_scale
        noise.inputs["Detail"].default_value = 3.0
        noise.inputs["Roughness"].default_value = 0.62
        ramp = mat.node_tree.nodes.new("ShaderNodeValToRGB")
        dark = tuple(max(0.0, component * 0.72) for component in color)
        light = tuple(min(1.0, component * 1.20 + 0.015) for component in color)
        ramp.color_ramp.elements[0].color = (*dark, 1.0)
        ramp.color_ramp.elements[1].color = (*light, 1.0)
        bump = mat.node_tree.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = bump_strength
        bump.inputs["Distance"].default_value = bump_distance
        mat.node_tree.links.new(texcoord.outputs["Generated"], noise.inputs["Vector"])
        mat.node_tree.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        mat.node_tree.links.new(ramp.outputs["Color"], principled.inputs["Base Color"])
        mat.node_tree.links.new(noise.outputs["Fac"], bump.inputs["Height"])
        mat.node_tree.links.new(bump.outputs["Normal"], principled.inputs["Normal"])
    return mat


class OrganicBody:
    """Accumulates metaball masses and produces one smooth skinned mesh."""

    def __init__(self, name, resolution=0.055):
        self.data = bpy.data.metaballs.new(name + "_Meta")
        self.data.resolution = resolution
        self.data.render_resolution = resolution
        # With the default threshold (0.6) the surface sits at ~57% of each
        # element's radius and bodies come out emaciated. A low threshold puts
        # the skin close to the authored radius and blends chains smoothly.
        self.data.threshold = 0.08
        self.object = bpy.data.objects.new(name, self.data)
        bpy.context.scene.collection.objects.link(self.object)

    def ball(self, position, radius, stiffness=2.0):
        element = self.data.elements.new()
        element.co = Vector(position)
        element.radius = radius
        element.stiffness = stiffness
        return element

    def ellipsoid(self, position, radius, size, stiffness=2.0):
        element = self.data.elements.new()
        element.type = "ELLIPSOID"
        element.co = Vector(position)
        element.radius = radius
        element.size_x, element.size_y, element.size_z = size
        element.stiffness = stiffness
        return element

    def chain(self, start, end, start_radius, end_radius, count=5):
        start, end = Vector(start), Vector(end)
        for index in range(count):
            t = index / max(count - 1, 1)
            self.ball(start.lerp(end, t), start_radius + (end_radius - start_radius) * t)

    def curve(self, knot1, handle1, handle2, knot2, start_radius, end_radius, samples=16):
        """Balls along a cubic Bézier — tails and necks curve instead of kinking."""
        from mathutils import geometry
        points = geometry.interpolate_bezier(
            Vector(knot1), Vector(handle1), Vector(handle2), Vector(knot2), samples)
        for index, point in enumerate(points):
            t = index / max(samples - 1, 1)
            self.ball(point, start_radius + (end_radius - start_radius) * t)

    def finish(self, name, rig, base_material, regions=()):
        """Convert to mesh, paint material regions and bind with smooth weights.

        regions: sequence of (material, predicate) applied on top of the base
        material; the predicate receives the polygon center as a Vector.
        """
        bpy.ops.object.select_all(action="DESELECT")
        self.object.select_set(True)
        bpy.context.view_layer.objects.active = self.object
        bpy.ops.object.convert(target="MESH")
        mesh_object = bpy.context.object
        mesh_object.name = name
        mesh_object.data.name = name + "_Mesh"

        decimate_to_target(mesh_object)

        mesh_object.data.materials.append(base_material)
        for region_material, _ in regions:
            mesh_object.data.materials.append(region_material)
        for polygon in mesh_object.data.polygons:
            polygon.use_smooth = True
            center = Vector(polygon.center)
            for slot_index, (_, predicate) in enumerate(regions, start=1):
                if predicate(center):
                    polygon.material_index = slot_index
                    break

        auto_weight(mesh_object, rig)
        return mesh_object


def decimate_to_target(mesh_object, target=TARGET_BODY_TRIANGLES):
    triangle_count = sum(len(p.vertices) - 2 for p in mesh_object.data.polygons)
    if triangle_count <= target:
        return
    modifier = mesh_object.modifiers.new("Budget", "DECIMATE")
    modifier.ratio = target / triangle_count
    bpy.context.view_layer.objects.active = mesh_object
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in mesh_object.data.polygons:
        polygon.use_smooth = True


def auto_weight(mesh_object, rig):
    """Bone-heat weights: limbs bend smoothly instead of moving as solid parts."""
    bpy.ops.object.select_all(action="DESELECT")
    mesh_object.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    except RuntimeError:
        bpy.ops.object.parent_set(type="ARMATURE_ENVELOPE")
    return mesh_object


def rigid_bind(obj, name, mat, rig, bone_name):
    """Classic single-bone binding, still right for eyes, claws and feathers."""
    obj.name = name
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    group = obj.vertex_groups.new(name=bone_name)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("Rig", "ARMATURE")
    modifier.object = rig
    return obj


def detail_sphere(name, position, scale, mat, rig, bone_name, segments=20, rings=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=position)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return rigid_bind(obj, name, mat, rig, bone_name)


def oriented_detail_sphere(name, position, scale, rotation, mat, rig, bone_name):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=14, location=position, rotation=rotation)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return rigid_bind(obj, name, mat, rig, bone_name)


def tapered_limb(name, start, end, start_radius, end_radius, mat, rig, bone_name, vertices=14):
    """A cone frustum between two points: limbs and digits read as muscle, not pipe."""
    start, end = Vector(start), Vector(end)
    direction = end - start
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=start_radius,
        radius2=end_radius,
        depth=direction.length,
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return rigid_bind(obj, name, mat, rig, bone_name)


def claw(name, position, length, radius, mat, rig, bone_name, pitch=0.55):
    """Curved-looking claw: a sharp cone pitched forward-down."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=10, radius1=radius, radius2=0.002, depth=length,
        location=position, rotation=(pitch, 0.0, 0.0),
    )
    obj = bpy.context.object
    return rigid_bind(obj, name, mat, rig, bone_name)


def surface_patch(name, body_object, center, size, rotation, mat, rig,
                  offset=0.012, thickness=0.018, cuts=8):
    """A marking that hugs the body surface (tiger stripe, face patch, band).

    A subdivided grid is shrinkwrapped onto the finished body mesh and given a
    tiny shell, so the marking follows the anatomy like painted fur instead of
    a box floating over it.
    """
    bpy.ops.mesh.primitive_grid_add(
        x_subdivisions=cuts, y_subdivisions=cuts, size=1.0,
        location=center, rotation=rotation,
    )
    obj = bpy.context.object
    obj.scale = (size[0], size[1], 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    wrap = obj.modifiers.new("Wrap", "SHRINKWRAP")
    wrap.target = body_object
    # Project straight through the body along the patch normal: every vertex
    # lands on the facing surface instead of snapping to whatever is nearest
    # (which turned stripes into stretched flaps between body and legs).
    wrap.wrap_method = "PROJECT"
    wrap.use_project_z = True
    wrap.use_negative_direction = True
    wrap.use_positive_direction = True
    wrap.offset = offset
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=wrap.name)

    shell = obj.modifiers.new("Shell", "SOLIDIFY")
    shell.thickness = thickness
    shell.offset = 0.0
    bpy.ops.object.modifier_apply(modifier=shell.name)

    obj.name = name
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    auto_weight(obj, rig)
    return obj


def displace_noise(obj, strength=0.25, frequency=1.0, seed=0.0, flat=False):
    """Pushes vertices along their normals with coherent noise.

    Smooth shading gives lumpy organic foliage; flat shading gives faceted
    natural rock. Either way the perfect-primitive look disappears.
    """
    from mathutils import noise
    offset = Vector((seed * 12.9898 % 7.0, seed * 78.233 % 5.0, seed * 3.71 % 9.0))
    normals = [vertex.normal.copy() for vertex in obj.data.vertices]
    for vertex, normal in zip(obj.data.vertices, normals):
        sample = noise.noise(vertex.co * frequency + offset)
        vertex.co += normal * (sample * strength)
    for polygon in obj.data.polygons:
        polygon.use_smooth = not flat
    return obj


def cartoon_eye(prefix, position, forward, size, rig, bone_name,
                white, iris, pupil, highlight=None):
    """Layered eye with a specular highlight dot — the classic 'alive' look."""
    forward = Vector(forward).normalized()
    detail_sphere(prefix + "_White", position, (size, size * 1.08, size * 0.55),
                  white, rig, bone_name)
    detail_sphere(prefix + "_Iris", Vector(position) + forward * size * 0.42,
                  (size * 0.62, size * 0.68, size * 0.22), iris, rig, bone_name)
    detail_sphere(prefix + "_Pupil", Vector(position) + forward * size * 0.55,
                  (size * 0.30, size * 0.40, size * 0.12), pupil, rig, bone_name)
    if highlight is not None:
        up_offset = Vector((size * 0.16, size * 0.18, 0.0))
        detail_sphere(prefix + "_Spark", Vector(position) + forward * size * 0.66 + up_offset,
                      (size * 0.10, size * 0.10, size * 0.05), highlight, rig, bone_name,
                      segments=10, rings=8)
