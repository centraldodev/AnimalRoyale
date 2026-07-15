"""Creates the Ant FBX with a Generic six-leg rig and combat clips.

Unlike the mammals, the ant keeps hard segmented shells on purpose — that is
what insect exoskeletons look like. Realism comes from a glossy chitin finish,
a narrow petiole waist, banded abdomen, jointed antennae and curved mandibles.
"""
import bpy, math, os, sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from organic import claw, material as organic_material, rigid_bind, tapered_limb  # noqa: E402
OUT=os.path.abspath(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv else os.path.abspath('AntOutput')
def clear(): bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def mat(n,c,rough=0.42,coat=0.2):
 return organic_material(n,c,roughness=rough,coat=coat)
def rig():
 bpy.ops.object.armature_add(enter_editmode=True);r=bpy.context.object;r.name='Ant_Rig';r.data.name='Ant_RigData';bs={'Root':r.data.edit_bones[0]};bs['Root'].name='Root';bs['Root'].head=(0,0,0);bs['Root'].tail=(0,.5,0)
 def b(n,h,t,p='Root'):
  q=r.data.edit_bones.new(n);q.head=h;q.tail=t;q.parent=bs[p];bs[n]=q
 b('Thorax',(0,.45,0),(0,.65,.25));b('Head',(0,.62,.42),(0,.72,.72),'Thorax');b('Abdomen',(0,.48,-.23),(0,.55,-.75),'Thorax');b('Mandible_L',(-.14,.60,.73),(-.34,.54,.91),'Head');b('Mandible_R',(.14,.60,.73),(.34,.54,.91),'Head')
 for z,label in ((.30,'F'),(0,'M'),(-.28,'B')):
  b('Leg_'+label+'_L',(-.22,.43,z),(-.42,.27,z+.04),'Thorax');b('LowerLeg_'+label+'_L',(-.42,.27,z+.04),(-.64,.08,z+.10),'Leg_'+label+'_L')
  b('Leg_'+label+'_R',(.22,.43,z),(.42,.27,z+.04),'Thorax');b('LowerLeg_'+label+'_R',(.42,.27,z+.04),(.64,.08,z+.10),'Leg_'+label+'_R')
 bpy.ops.object.mode_set(mode='OBJECT')
 for x in r.pose.bones:x.rotation_mode='XYZ'
 return r
def bind(o,n,m,r,b):
 o.name=n;o.data.materials.append(m)
 for p in o.data.polygons:p.use_smooth=True
 g=o.vertex_groups.new(name=b);g.add(list(range(len(o.data.vertices))),1,'REPLACE');a=o.modifiers.new('AntRig','ARMATURE');a.object=r;return o
def sph(n,p,s,m,r,b):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=28,ring_count=18,location=p);o=bpy.context.object;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return bind(o,n,m,r,b)
def ring(n,p,rx,ry,m,r,b):
 bpy.ops.mesh.primitive_cylinder_add(vertices=28,radius=1,depth=1,location=p);o=bpy.context.object;o.scale=(rx,ry,.028);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return bind(o,n,m,r,b)
def limb(n,a,b,rad,m,r,bone):
 a,b=Vector(a),Vector(b);d=b-a;bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=rad,depth=d.length,location=(a+b)*.5);o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(d.normalized());bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return bind(o,n,m,r,bone)
def model(r,red,dark,eye):
 shine=mat('Ant_Shine',(1,1,.94),rough=.15,coat=0)
 iris=mat('Ant_Iris',(.42,.12,.025),rough=.18,coat=.12)
 # Three glossy shell segments joined by a narrow petiole waist.
 sph('Abdomen',(0,.48,-.42),(.44,.37,.56),red,r,'Abdomen')
 ring('AbdomenBand_0',(0,.48,-.54),.425,.355,dark,r,'Abdomen')
 ring('AbdomenBand_1',(0,.48,-.72),.36,.30,dark,r,'Abdomen')
 sph('Petiole',(0,.50,-.16),(.15,.14,.13),red,r,'Thorax')
 sph('Thorax',(0,.53,.02),(.32,.32,.35),red,r,'Thorax')
 sph('Neck',(0,.58,.30),(.16,.15,.14),red,r,'Head')
 # Heart-shaped head: wide cranium tapering to the jaws.
 sph('Head',(0,.64,.53),(.46,.40,.38),red,r,'Head')
 sph('Cranium',(0,.76,.43),(.39,.31,.30),red,r,'Head')
 sph('Face',(0,.58,.72),(.32,.24,.22),red,r,'Head')
 for side,label in ((-1,'L'),(1,'R')):
  # Large compound eyes retain a dark insect globe, but layered irises and
  # broad catchlights give the friendly expression from the reference.
  sph('Eye_'+label,(side*.245,.73,.755),(.145,.16,.09),eye,r,'Head')
  sph('Iris_'+label,(side*.245,.735,.825),(.088,.10,.035),iris,r,'Head')
  sph('Pupil_'+label,(side*.245,.738,.852),(.052,.064,.018),dark,r,'Head')
  sph('EyeSpark_'+label,(side*.218,.775,.872),(.026,.030,.010),shine,r,'Head')
  # Curved mandibles: a tapered arm plus an inward-hooked tip.
  tapered_limb('Mandible_'+label,(side*.15,.54,.76),(side*.34,.46,.94),.08,.045,dark,r,'Mandible_'+label)
  claw('MandibleTip_'+label,(side*.34,.455,.945),.15,.048,dark,r,'Mandible_'+label,pitch=1.0)
  for z,key in ((.30,'F'),(0,'M'),(-.28,'B')):
   hip=(side*.20,.46,z);knee=(side*.42,.27,z+.04);foot=(side*.64,.08,z+.10)
   # Coxa ball into a muscular femur, thin tibia and a tiny tarsus tip.
   sph('Coxa_'+key+'_'+label,hip,(.075,.075,.075),red,r,'Leg_'+key+'_'+label)
   tapered_limb('Femur_'+key+'_'+label,hip,knee,.055,.038,dark,r,'Leg_'+key+'_'+label)
   sph('Knee_'+key+'_'+label,knee,(.055,.052,.055),red,r,'LowerLeg_'+key+'_'+label)
   tapered_limb('Tibia_'+key+'_'+label,knee,foot,.038,.02,dark,r,'LowerLeg_'+key+'_'+label)
   claw('Tarsus_'+key+'_'+label,foot,.07,.018,dark,r,'LowerLeg_'+key+'_'+label,pitch=.9)
  # Elbowed antennae: scape up, flagellum sweeping forward.
  elbow=(side*.27,1.08,.62)
  tapered_limb('Scape_'+label,(side*.12,.91,.60),elbow,.026,.018,dark,r,'Head')
  tapered_limb('Flagellum_'+label,elbow,(side*.36,1.23,.96),.018,.010,dark,r,'Head')
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
 act(r,'Ant_Idle',30,[(1,{'Mandible_L':(0,0,7),'Mandible_R':(0,0,-7)},None),(15,{'Mandible_L':(0,0,-7),'Mandible_R':(0,0,7)},None),(30,{'Mandible_L':(0,0,7),'Mandible_R':(0,0,-7)},None)])
 a={'Leg_F_L':(26,0,0),'LowerLeg_F_L':(-22,0,0),'Leg_M_R':(26,0,0),'LowerLeg_M_R':(-22,0,0),'Leg_B_L':(26,0,0),'LowerLeg_B_L':(-22,0,0),'Leg_F_R':(-26,0,0),'LowerLeg_F_R':(22,0,0),'Leg_M_L':(-26,0,0),'LowerLeg_M_L':(22,0,0),'Leg_B_R':(-26,0,0),'LowerLeg_B_R':(22,0,0)};b={n:tuple(-q for q in v) for n,v in a.items()}
 act(r,'Ant_Walk',24,[(1,a,None),(12,b,None),(24,a,None)]);act(r,'Ant_Run',18,[(1,{n:tuple(q*1.45 for q in v) for n,v in a.items()},None),(9,{n:tuple(q*1.45 for q in v) for n,v in b.items()},None),(18,{n:tuple(q*1.45 for q in v) for n,v in a.items()},None)])
 act(r,'Ant_Bite',20,[(1,{},None),(8,{'Head':(-22,0,0),'Mandible_L':(0,0,34),'Mandible_R':(0,0,-34)},None),(20,{},None)])
 act(r,'Ant_Throw',24,[(1,{},None),(10,{'Leg_F_L':(-58,0,0),'Leg_F_R':(-58,0,0),'LowerLeg_F_L':(-48,0,0),'LowerLeg_F_R':(-48,0,0),'Thorax':(14,0,0)},None),(18,{'Leg_F_L':(48,0,0),'Leg_F_R':(48,0,0),'LowerLeg_F_L':(34,0,0),'LowerLeg_F_R':(34,0,0),'Thorax':(-8,0,0)},(0,0,.22)),(24,{},None)])
 act(r,'Ant_Burrow',24,[(1,{},None),(12,{'Thorax':(18,0,0)},(0,-.30,0)),(24,{},None)])
 act(r,'Ant_Shield',18,[(1,{},None),(9,{'Thorax':(-10,0,0),'Head':(12,0,0)},None),(18,{},None)]);r.animation_data.action=bpy.data.actions['Ant_Idle']
clear();R=rig();RED=mat('Ant_Red',(.34,.025,.010));DARK=mat('Ant_Dark',(.065,.008,.004));EYE=mat('Ant_Eye',(.025,.010,.006),rough=.34,coat=.25);model(R,RED,DARK,EYE);animations(R);os.makedirs(OUT,exist_ok=True);bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=R;bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Ant_Source.blend'));bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Ant.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,mesh_smooth_type='FACE',apply_scale_options='FBX_SCALE_UNITS')
