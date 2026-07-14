"""Builds the refined playable tiger while preserving the gameplay rig.

The first model was intentionally broad and cartoon-like. This version keeps
the same bone and action names, but uses low feline proportions, tapered limbs,
small predatory eyes, a split muzzle and irregular surface-painted stripes.
"""
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import create_tiger as legacy  # noqa: E402
from organic import (  # noqa: E402
    OrganicBody, claw, detail_sphere, material, oriented_detail_sphere,
    rigid_bind, tapered_limb,
)


ORANGE = material("TigerRefined_Orange", (0.72, 0.20, 0.025), roughness=0.82)
CREAM = material("TigerRefined_Cream", (0.88, 0.62, 0.31), roughness=0.86)
BLACK = material("TigerRefined_Black", (0.018, 0.009, 0.005), roughness=0.78)
AMBER = material("TigerRefined_Amber", (0.78, 0.35, 0.025), roughness=0.32)
SPARK = material("TigerRefined_EyeGlint", (1.0, 0.98, 0.86), roughness=0.12)
NOSE = material("TigerRefined_Nose", (0.24, 0.035, 0.025), roughness=0.48)
INNER_EAR = material("TigerRefined_InnerEar", (0.52, 0.24, 0.17), roughness=0.88)


def create_rig():
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    rig = bpy.context.object
    rig.name = "Tiger_Rig"
    rig.data.name = "Tiger_RigData"
    root = rig.data.edit_bones[0]
    root.name = "Root"
    root.head = (0, 0, 0)
    root.tail = (0, 0.65, 0)
    bones = {"Root": root}

    def bone(name, head, tail, parent="Root"):
        item = rig.data.edit_bones.new(name)
        item.head = head
        item.tail = tail
        item.parent = bones[parent]
        bones[name] = item

    bone("Spine", (0, 0.65, -0.35), (0, 0.79, 0.25))
    bone("Chest", (0, 0.78, 0.20), (0, 0.91, 0.52), "Spine")
    bone("Neck", (0, 0.89, 0.48), (0, 1.00, 0.70), "Chest")
    bone("Head", (0, 0.99, 0.68), (0, 1.02, 1.02), "Neck")
    bone("Jaw", (0, 0.94, 0.88), (0, 0.89, 1.18), "Head")
    bone("Ear_L", (-0.19, 1.15, 0.73), (-0.23, 1.34, 0.72), "Head")
    bone("Ear_R", (0.19, 1.15, 0.73), (0.23, 1.34, 0.72), "Head")
    leg_bones = [
        ("FL", (-0.30, 0.72, 0.39), (-0.34, 0.39, 0.45), (-0.35, 0.13, 0.48), "Chest"),
        ("FR", (0.30, 0.72, 0.39), (0.34, 0.39, 0.45), (0.35, 0.13, 0.48), "Chest"),
        ("BL", (-0.29, 0.69, -0.49), (-0.34, 0.39, -0.55), (-0.35, 0.13, -0.45), "Spine"),
        ("BR", (0.29, 0.69, -0.49), (0.34, 0.39, -0.55), (0.35, 0.13, -0.45), "Spine"),
    ]
    for label, hip, knee, ankle, parent in leg_bones:
        bone("Leg_" + label, hip, knee, parent)
        bone("LowerLeg_" + label, knee, ankle, "Leg_" + label)
        bone("Paw_" + label, ankle, (ankle[0], 0.10, ankle[2] + 0.28), "LowerLeg_" + label)
        bone("Claw_" + label, (ankle[0], 0.10, ankle[2] + 0.18),
             (ankle[0], 0.07, ankle[2] + 0.39), "Paw_" + label)
    bone("Tail_1", (0, 0.78, -0.78), (0.10, 0.87, -1.20), "Spine")
    bone("Tail_2", (0.10, 0.87, -1.20), (0.29, 0.96, -1.52), "Tail_1")
    bone("Tail_3", (0.29, 0.96, -1.52), (0.43, 0.94, -1.78), "Tail_2")
    bpy.ops.object.mode_set(mode="OBJECT")
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    return rig


def pointed_ear(side, label, rig):
    bpy.ops.mesh.primitive_cone_add(
        vertices=3, radius1=0.13, radius2=0.025, depth=0.24,
        location=(side * 0.19, 1.20, 0.73),
        rotation=(math.radians(-90), 0, side * math.radians(8)))
    ear = bpy.context.object
    ear.scale = (1.0, 0.58, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    rigid_bind(ear, "Ear_" + label, ORANGE, rig, "Ear_" + label)
    oriented_detail_sphere(
        "EarInner_" + label, (side * 0.19, 1.205, 0.755),
        (0.064, 0.100, 0.024), (0, 0, 0), INNER_EAR, rig, "Ear_" + label)


def build_body(rig):
    body = OrganicBody("TigerRefinedBody", resolution=0.032)
    body.ball((0, 0.72, -0.48), 0.39)
    body.ball((-0.20, 0.68, -0.50), 0.27)
    body.ball((0.20, 0.68, -0.50), 0.27)
    body.ball((0, 0.70, -0.15), 0.37)
    body.ball((0, 0.74, 0.16), 0.39)
    body.ball((-0.20, 0.74, 0.34), 0.28)
    body.ball((0.20, 0.74, 0.34), 0.28)
    body.ball((0, 0.79, 0.38), 0.34)
    body.chain((0, 0.82, 0.46), (0, 0.98, 0.69), 0.27, 0.24, 5)
    body.ball((0, 1.02, 0.78), 0.30)
    body.ball((-0.17, 0.99, 0.84), 0.18)
    body.ball((0.17, 0.99, 0.84), 0.18)
    body.ball((0, 0.96, 0.98), 0.19)
    body.chain((0, 0.94, 0.98), (0, 0.91, 1.16), 0.17, 0.11, 4)
    body.ball((0, 0.88, 1.05), 0.13)

    stripe_bands = (-0.58, -0.35, -0.11, 0.13, 0.34)

    def body_stripe(c):
        if not (-0.73 < c.z < 0.49) or c.y < 0.66:
            return False
        flank = abs(c.x) > 0.16 and c.y > 0.69
        dorsal = c.y > 0.91 and abs(c.x) < 0.25
        if not (flank or dorsal):
            return False
        phase = c.z + 0.24 * (c.y - 0.72) + (0.045 if c.x > 0 else -0.045)
        return any(abs(phase - band) < (0.018 if dorsal else 0.027)
                   for band in stripe_bands)

    def face_stripe(c):
        forehead = (c.y > 1.03 and 0.73 < c.z < 0.95 and
                    (abs(c.x - 0.09) < 0.032 or abs(c.x + 0.09) < 0.032))
        return forehead

    return body.finish("Tiger_Body", rig, ORANGE, regions=(
        (BLACK, body_stripe),
        (BLACK, face_stripe),
        (CREAM, lambda c: c.y < 0.67 and -0.48 < c.z < 0.28 and abs(c.x) < 0.25),
        (CREAM, lambda c: c.z > 0.99 and c.y < 0.98),
    ))


def build_limbs(rig):
    legs = [
        ("FL", (-0.30, 0.72, 0.39), (-0.34, 0.39, 0.45), (-0.35, 0.13, 0.48)),
        ("FR", (0.30, 0.72, 0.39), (0.34, 0.39, 0.45), (0.35, 0.13, 0.48)),
        ("BL", (-0.29, 0.69, -0.49), (-0.34, 0.39, -0.55), (-0.35, 0.13, -0.45)),
        ("BR", (0.29, 0.69, -0.49), (0.34, 0.39, -0.55), (0.35, 0.13, -0.45)),
    ]
    for label, hip, knee, ankle in legs:
        tapered_limb("UpperLeg_" + label, hip, knee, 0.115, 0.078,
                     ORANGE, rig, "Leg_" + label, vertices=18)
        tapered_limb("LowerLeg_" + label, knee, ankle, 0.082, 0.055,
                     ORANGE, rig, "LowerLeg_" + label, vertices=18)
        oriented_detail_sphere("Joint_" + label, knee, (0.084, 0.078, 0.074),
                               (0, 0, 0), ORANGE, rig, "LowerLeg_" + label)
        paw = (ankle[0], 0.105, ankle[2] + 0.115)
        oriented_detail_sphere("Paw_" + label, paw, (0.112, 0.062, 0.17),
                               (0, 0, 0), ORANGE, rig, "Paw_" + label)
        for toe_index in (-1, 0, 1):
            toe = (ankle[0] + toe_index * 0.065, 0.085, ankle[2] + 0.245)
            oriented_detail_sphere("Toe_%s_%d" % (label, toe_index), toe,
                                   (0.052, 0.038, 0.075), (0, 0, 0), CREAM,
                                   rig, "Paw_" + label)
            claw("Claw_%s_%d" % (label, toe_index),
                 (toe[0], 0.073, toe[2] + 0.055), 0.055, 0.014,
                 BLACK, rig, "Claw_" + label)


def build_tail(rig):
    segments = [
        ("TailBase", (0, 0.78, -0.78), (0.10, 0.87, -1.20), 0.085, 0.070, "Tail_1", ORANGE),
        ("TailMid", (0.10, 0.87, -1.20), (0.29, 0.96, -1.52), 0.070, 0.055, "Tail_2", ORANGE),
        ("TailTip", (0.29, 0.96, -1.52), (0.43, 0.94, -1.78), 0.055, 0.035, "Tail_3", BLACK),
    ]
    for name, start, end, start_radius, end_radius, bone_name, mat in segments:
        tapered_limb(name, start, end, start_radius, end_radius,
                     mat, rig, bone_name, vertices=18)
    tapered_limb("TailRing_1", (0.13, 0.885, -1.25), (0.18, 0.91, -1.34),
                 0.072, 0.064, BLACK, rig, "Tail_2", vertices=18)
    tapered_limb("TailRing_2", (0.25, 0.945, -1.46), (0.31, 0.96, -1.56),
                 0.060, 0.052, BLACK, rig, "Tail_3", vertices=18)


def build_face(rig):
    for side, label in ((-1, "L"), (1, "R")):
        pointed_ear(side, label, rig)
        oriented_detail_sphere("EyeSocket_" + label, (side * 0.115, 1.048, 1.015),
                               (0.047, 0.030, 0.020), (0, 0, 0), BLACK, rig, "Head")
        oriented_detail_sphere("Eye_" + label, (side * 0.115, 1.047, 1.030),
                               (0.032, 0.022, 0.011), (0, 0, 0), AMBER, rig, "Head")
        oriented_detail_sphere("Pupil_" + label, (side * 0.115, 1.047, 1.039),
                               (0.007, 0.017, 0.005), (0, 0, 0), BLACK, rig, "Head")
        oriented_detail_sphere("EyeSpark_" + label, (side * 0.109, 1.054, 1.044),
                               (0.004, 0.004, 0.002), (0, 0, 0), SPARK, rig, "Head")
        tapered_limb("Brow_" + label, (side * 0.060, 1.085, 1.012),
                     (side * 0.165, 1.075, 0.998), 0.016, 0.008,
                     BLACK, rig, "Head", vertices=10)

    oriented_detail_sphere("Muzzle_L", (-0.085, 0.925, 1.115),
                           (0.12, 0.085, 0.11), (0, 0, 0), CREAM, rig, "Jaw")
    oriented_detail_sphere("Muzzle_R", (0.085, 0.925, 1.115),
                           (0.12, 0.085, 0.11), (0, 0, 0), CREAM, rig, "Jaw")
    oriented_detail_sphere("Chin", (0, 0.865, 1.12),
                           (0.105, 0.055, 0.105), (0, 0, 0), CREAM, rig, "Jaw")
    bpy.ops.mesh.primitive_cone_add(vertices=3, radius1=0.070, radius2=0.030,
                                    depth=0.045, location=(0, 0.975, 1.225),
                                    rotation=(0, 0, math.radians(180)))
    nose = bpy.context.object
    nose.scale = (1.0, 0.72, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    rigid_bind(nose, "Nose", NOSE, rig, "Head")
    tapered_limb("MouthLine", (-0.075, 0.905, 1.185), (0.075, 0.905, 1.185),
                 0.008, 0.008, BLACK, rig, "Jaw", vertices=8)
    for side in (-1, 1):
        for row, (dy, dz) in enumerate(((0.025, 0.0), (-0.015, 0.01), (-0.05, -0.005))):
            tapered_limb("Whisker_%d_%d" % (side, row),
                         (side * 0.11, 0.93 + dy, 1.17 + dz),
                         (side * 0.34, 0.91 + dy * 1.8, 1.25 + dz),
                         0.004, 0.001, BLACK, rig, "Head", vertices=6)


def build_tiger(rig):
    build_body(rig)
    build_limbs(rig)
    build_tail(rig)
    build_face(rig)


if __name__ == "__main__":
    legacy.clean_scene()
    tiger_rig = create_rig()
    build_tiger(tiger_rig)
    legacy.create_animations(tiger_rig)
    legacy.export(tiger_rig)
