"""Creates the Eagle FBX with a Generic wing rig and its gameplay clips.

The body is one organic metaball mesh (see organic.py); the wings are built
from individually placed covert, secondary and primary feathers so they read
as real layered plumage while flapping on the same two-bone wing rig.
"""
import bpy, math, os, sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from organic import (  # noqa: E402
    OrganicBody, cartoon_eye, claw, detail_sphere,
    material as organic_material, oriented_detail_sphere, tapered_limb,
)
OUT = os.path.abspath(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else os.path.abspath('EagleOutput')

def material(name, color):
    return organic_material(name, color, roughness=0.6)
def clear(): bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def make_rig():
    bpy.ops.object.armature_add(enter_editmode=True); r=bpy.context.object; r.name='Eagle_Rig'; r.data.name='Eagle_RigData'; bs={'Root':r.data.edit_bones[0]}; bs['Root'].name='Root'; bs['Root'].head=(0,0,0); bs['Root'].tail=(0,.7,0)
    def b(n,h,t,p='Root'):
        q=r.data.edit_bones.new(n); q.head=h; q.tail=t; q.parent=bs[p]; bs[n]=q
    b('Body',(0,.55,0),(0,1.05,0)); b('Head',(0,1.0,.25),(0,1.28,.45),'Body')
    b('Wing_L',(-.28,.95,0),(-.82,1.01,0),'Body'); b('WingTip_L',(-.82,1.01,0),(-1.45,1.05,-.02),'Wing_L')
    b('Wing_R',(.28,.95,0),(.82,1.01,0),'Body'); b('WingTip_R',(.82,1.01,0),(1.45,1.05,-.02),'Wing_R')
    b('Tail',(0,.72,-.28),(0,.75,-.85),'Body')
    b('Leg_L',(-.14,.55,.08),(-.16,.31,.10),'Root'); b('LowerLeg_L',(-.16,.31,.10),(-.18,.10,.14),'Leg_L'); b('Talon_L',(-.18,.10,.14),(-.18,.06,.42),'LowerLeg_L')
    b('Leg_R',(.14,.55,.08),(.16,.31,.10),'Root'); b('LowerLeg_R',(.16,.31,.10),(.18,.10,.14),'Leg_R'); b('Talon_R',(.18,.10,.14),(.18,.06,.42),'LowerLeg_R')
    bpy.ops.object.mode_set(mode='OBJECT')
    for x in r.pose.bones:x.rotation_mode='XYZ'
    return r
def bind(o,n,m,r,b):
    o.name=n; o.data.materials.append(m)
    for p in o.data.polygons:p.use_smooth=True
    g=o.vertex_groups.new(name=b);g.add(list(range(len(o.data.vertices))),1,'REPLACE');a=o.modifiers.new('EagleRig','ARMATURE');a.object=r;return o
def sph(n,p,s,m,r,b):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=14,ring_count=8,location=p);o=bpy.context.object;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return bind(o,n,m,r,b)
def cube(n,p,s,m,r,b,rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(location=p,rotation=rot);o=bpy.context.object;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return bind(o,n,m,r,b)
def model(r,brown,cream,gold,black,white):
    dark = material('Eagle_DarkFeather', (.10, .04, .018))
    spark = material('Eagle_Spark', (1, 1, .96))

    body = OrganicBody('EagleBody', resolution=0.035)
    # Streamlined teardrop torso with a keeled chest.
    body.ball((0, .80, -.10), .27)
    body.ball((0, .74, .05), .28)
    body.ball((0, .64, .10), .21)
    body.ball((0, .72, -.28), .21)
    body.chain((0, .73, -.34), (0, .74, -.50), .16, .11, 4)
    # Shoulder roots so the feathered wings emerge from the plumage.
    body.ball((-.24, .93, -.02), .12)
    body.ball((.24, .93, -.02), .12)
    # Neck flowing into the head, brow and cheeks.
    body.chain((0, .92, .12), (0, 1.10, .28), .17, .14, 4)
    body.ball((0, 1.20, .34), .165)
    body.ball((0, 1.28, .38), .10)
    # Feathered thighs ("trousers").
    for s in (-1, 1):
        body.chain((s*.14, .60, .08), (s*.16, .34, .10), .11, .05, 4)

    body.finish('Eagle_Body', r, brown, regions=(
        (cream, lambda c: c.y > 1.04 and abs(c.x) < .20 and c.z > -0.02),  # white head
    ))

    # Short, stout hooked beak.
    tapered_limb('Beak', (0, 1.18, .46), (0, 1.16, .63), .065, .025, gold, r, 'Head')
    claw('BeakHook', (0, 1.14, .645), .06, .02, gold, r, 'Head', pitch=2.8)

    # Fierce eyes: layered eye plus an angled dark brow ridge.
    for s, label in ((-1, 'L'), (1, 'R')):
        cartoon_eye('Eye_'+label, (s*.105, 1.26, .43), (s*.35, .05, 1.0),
                    .06, r, 'Head', white, gold, black, spark)
        oriented_detail_sphere('Brow_'+label, (s*.105, 1.325, .44),
                               (.05, .015, .045), (0, 0, -s*.35), dark, r, 'Head')

    # Wings: covert base plus fanned secondary and primary feathers.
    for s, label in ((-1, 'L'), (1, 'R')):
        oriented_detail_sphere('WingBase_'+label, (s*.55, .98, -.02),
                               (.29, .035, .15), (0, 0, 0), brown, r, 'Wing_'+label)
        oriented_detail_sphere('WingTipBase_'+label, (s*1.10, 1.005, -.05),
                               (.33, .028, .12), (0, -s*.06, 0), brown, r, 'WingTip_'+label)
        for i in range(6):  # secondaries trail the inner wing
            x = .34 + i * .10
            oriented_detail_sphere('Secondary_%s_%d' % (label, i),
                                   (s*x, .965, -.17), (.05, .016, .13 + i*.008),
                                   (0, 0, 0), dark, r, 'Wing_'+label)
        for i in range(7):  # primaries sweep outward from the wing tip
            x = .80 + i * .105
            sweep = -s * math.radians(4 + i * 6)
            oriented_detail_sphere('Primary_%s_%d' % (label, i),
                                   (s*x, 1.0, -.13 - i*.022),
                                   (.05, .014, .16 + i*.02), (0, sweep, 0),
                                   dark, r, 'WingTip_'+label)

    # Tail: a fan of five cream feathers.
    for i in range(5):
        angle = math.radians((i - 2) * 14)
        offset_x = math.sin(angle) * .28
        oriented_detail_sphere('TailFeather_%d' % i,
                               (offset_x, .74, -.62 - math.cos(angle) * .06),
                               (.055, .02, .19), (0, angle, 0), cream, r, 'Tail')

    # Bare lower legs and three-toed talons with black claws.
    for s, label in ((-1, 'L'), (1, 'R')):
        tapered_limb('LowerLeg_'+label, (s*.16, .34, .10), (s*.18, .11, .14),
                     .04, .028, gold, r, 'LowerLeg_'+label)
        for toe, dx in ((-1, -.06), (0, 0), (1, .06)):
            tip = (s*.18 + dx, .05, .32 + abs(dx) * -.35)
            tapered_limb('Toe_%s_%d' % (label, toe), (s*.18, .09, .16), tip,
                         .028, .018, gold, r, 'Talon_'+label)
            claw('Claw_%s_%d' % (label, toe), tip, .06, .018, black, r, 'Talon_'+label, pitch=.9)
def key(r,f,rots=None,loc=None):
    for b in r.pose.bones:b.rotation_euler=(0,0,0);b.location=(0,0,0)
    for n,v in (rots or {}).items():r.pose.bones[n].rotation_euler=tuple(math.radians(q) for q in v)
    if loc:r.pose.bones['Root'].location=loc
    for b in r.pose.bones:b.keyframe_insert(data_path='rotation_euler',frame=f);b.keyframe_insert(data_path='location',frame=f) if b.name=='Root' else None
def act(r,n,end,frames):
    r.animation_data_create();r.animation_data.action=bpy.data.actions.new(n)
    for f,ro,lo in frames:key(r,f,ro,lo)
    r.animation_data.action.use_fake_user=True
def animations(r):
    act(r,'Eagle_Idle',30,[(1,{'Head':(2,0,0),'WingTip_L':(0,0,-3),'WingTip_R':(0,0,3)},None),(15,{'Head':(-2,0,0),'WingTip_L':(0,0,3),'WingTip_R':(0,0,-3)},None),(30,{'Head':(2,0,0),'WingTip_L':(0,0,-3),'WingTip_R':(0,0,3)},None)])
    up={'Wing_L':(0,0,-28),'Wing_R':(0,0,28)};down={'Wing_L':(0,0,34),'Wing_R':(0,0,-34)}
    fly_up={**up,'WingTip_L':(0,0,-18),'WingTip_R':(0,0,18),'Tail':(-5,0,0)}
    fly_down={**down,'WingTip_L':(0,0,28),'WingTip_R':(0,0,-28),'Tail':(7,0,0)}
    act(r,'Eagle_Fly',24,[(1,fly_up,None),(6,{'Wing_L':(0,0,-5),'Wing_R':(0,0,5),'WingTip_L':(0,0,-30),'WingTip_R':(0,0,30)},None),(12,fly_down,None),(18,{'Wing_L':(0,0,2),'Wing_R':(0,0,-2),'WingTip_L':(0,0,34),'WingTip_R':(0,0,-34)},None),(24,fly_up,None)])
    # Wings fold during the dive and both legs extend in front of the body. The
    # final kick makes the claw strike readable from the third-person camera.
    act(r,'Eagle_Dive',22,[
        (1,up,None),
        (7,{'Wing_L':(0,0,12),'Wing_R':(0,0,-12),'WingTip_L':(0,0,34),'WingTip_R':(0,0,-34),'Head':(-24,0,0),'Leg_L':(-38,0,0),'Leg_R':(-38,0,0),'LowerLeg_L':(32,0,0),'LowerLeg_R':(32,0,0)},(0,.22,.48)),
        (13,{'Wing_L':(0,0,24),'Wing_R':(0,0,-24),'WingTip_L':(0,0,46),'WingTip_R':(0,0,-46),'Head':(-18,0,0),'Leg_L':(-78,0,0),'Leg_R':(-78,0,0),'LowerLeg_L':(-24,0,0),'LowerLeg_R':(-24,0,0),'Talon_L':(-42,0,-16),'Talon_R':(-42,0,16)},(0,.38,.72)),
        (17,{'Wing_L':(0,0,-8),'Wing_R':(0,0,8),'WingTip_L':(0,0,-18),'WingTip_R':(0,0,18),'Leg_L':(24,0,0),'Leg_R':(24,0,0),'LowerLeg_L':(38,0,0),'LowerLeg_R':(38,0,0),'Talon_L':(28,0,12),'Talon_R':(28,0,-12)},(0,.18,.34)),
        (22,up,None)
    ])
    act(r,'Eagle_Gust',20,[(1,up,None),(10,down,None),(20,up,None)])
    act(r,'Eagle_Perch',20,[(1,{'Wing_L':(0,0,30),'Wing_R':(0,0,-30)},None),(12,{},None),(20,{},None)])
    r.animation_data.action=bpy.data.actions['Eagle_Idle']
clear();B=material('Eagle_Brown',(.20,.07,.025));C=material('Eagle_Cream',(.95,.83,.56));G=material('Eagle_Gold',(1,.48,.04));K=material('Eagle_Black',(.02,.01,.008));W=material('Eagle_White',(.96,.94,.78));R=make_rig();model(R,B,C,G,K,W);animations(R);os.makedirs(OUT,exist_ok=True);bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=R;bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Eagle_Source.blend'));bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Eagle.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,mesh_smooth_type='FACE',apply_scale_options='FBX_SCALE_UNITS')
