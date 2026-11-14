# Builds the LEFT hand from the final RIGHT hand FBX: mesh, custom normals, armature,
# skin weights and every Action are mirrored together across the X axis (Blender space),
# then exported as a new FBX with positive scale everywhere. The Right FBX is only read.
#
#   blender -b --factory-startup --python mirror_hand.py -- <right.fbx> <left.fbx>
import bpy, sys, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, DST = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh_ob = next(o for o in bpy.data.objects if o.type == 'MESH')
actions = list(bpy.data.actions)
rest_action = arm.animation_data.action
print("source armature", arm.name, "mesh", mesh_ob.name, "actions", [a.name for a in actions])

MIRROR = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))


def assign(action):
    arm.animation_data.action = action
    try:
        arm.animation_data.action_slot = action.slots[0]
    except Exception:
        pass
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()


def snapshot():
    """World-space bone heads/tails and deformed vertex positions of the current pose."""
    deps = bpy.context.evaluated_depsgraph_get()
    bones = {pb.name: ((arm.matrix_world @ pb.head).copy(), (arm.matrix_world @ pb.tail).copy()) for pb in arm.pose.bones}
    ev = mesh_ob.evaluated_get(deps)
    verts = [(mesh_ob.matrix_world @ v.co).copy() for v in ev.data.vertices]
    return bones, verts


# ---- 1. reference: every pose of the RIGHT hand, mirrored
reference = {}
for a in actions:
    assign(a)
    bones, verts = snapshot()
    reference[a.name] = ({k: (MIRROR @ h, MIRROR @ t) for k, (h, t) in bones.items()}, [MIRROR @ v for v in verts])
assign(rest_action)

# ---- 2. mesh: positions, winding and custom normals
me = mesh_ob.data
loop_normals = {}
for poly in me.polygons:
    for li in poly.loop_indices:
        n = me.corner_normals[li].vector
        loop_normals[(poly.index, me.loops[li].vertex_index)] = Vector((-n.x, n.y, n.z))
for v in me.vertices:
    v.co.x = -v.co.x
me.flip_normals()  # mirroring turns faces inside out; restore outward winding
custom = [None] * len(me.loops)
for poly in me.polygons:
    for li in poly.loop_indices:
        custom[li] = loop_normals[(poly.index, me.loops[li].vertex_index)]
me.normals_split_custom_set(custom)
me.update()

# ---- 3. armature: heads, tails, rolls (all read first: connected bones share points)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
data = {b.name: (b.head.copy(), b.tail.copy(), b.roll) for b in eb}
for b in eb:
    h, t, r = data[b.name]
    b.head = Vector((-h.x, h.y, h.z))
    b.tail = Vector((-t.x, t.y, t.z))
    b.roll = -r
bpy.ops.object.mode_set(mode='OBJECT')

# ---- 4. actions: bone rotations (w, x, -y, -z), bone locations (-x, y, z)
def channelbags(action):
    for layer in action.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                yield cb

for a in actions:
    for cb in channelbags(a):
        for fc in cb.fcurves:
            if not fc.data_path.startswith('pose.bones'):
                continue
            prop = fc.data_path.rsplit('.', 1)[-1]
            flip = (prop == 'rotation_quaternion' and fc.array_index in (2, 3)) or \
                   (prop == 'location' and fc.array_index == 0) or \
                   (prop == 'rotation_euler' and fc.array_index in (1, 2))
            if not flip:
                continue
            for k in fc.keyframe_points:
                k.co[1] = -k.co[1]
                k.handle_left[1] = -k.handle_left[1]
                k.handle_right[1] = -k.handle_right[1]
            fc.update()

# ---- 5. names
arm.name = arm.name.replace('_R', '_L')
arm.data.name = arm.data.name.replace('_R', '_L')
mesh_ob.name = mesh_ob.name.replace('_R', '_L')
me.name = me.name.replace('_R', '_L')
for a in actions:
    if '|' in a.name:
        a.name = a.name.split('|', 1)[1]
print("left armature", arm.name, "mesh", mesh_ob.name, "actions", [a.name for a in actions])

# ---- 6. verify every pose against the mirrored right hand
worst_bone = worst_vert = 0.0
for a in actions:
    assign(a)
    bones, verts = snapshot()
    ref_b, ref_v = reference[[k for k in reference if k.endswith(a.name)][0]]
    eb_ = max(max((bones[k][0] - ref_b[k][0]).length, (bones[k][1] - ref_b[k][1]).length) for k in bones)
    ev_ = max((v - r).length for v, r in zip(verts, ref_v))
    worst_bone, worst_vert = max(worst_bone, eb_), max(worst_vert, ev_)
    print("VERIFY %-24s bone err %.2e m  vertex err %.2e m" % (a.name, eb_, ev_))
print("VERIFY worst bone %.2e worst vertex %.2e" % (worst_bone, worst_vert))
assert worst_bone < 1e-5 and worst_vert < 1e-5, "mirror verification failed"

# outward normals: mean of (normal . (face centre - mesh centre)) must stay positive
centre = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
outward = sum(p.normal.dot(p.center - centre) for p in me.polygons) / len(me.polygons)
print("VERIFY mean outward normal dot %.5f (must be > 0)" % outward)
assert outward > 0

for o in bpy.data.objects:
    assert all(s > 0 for s in o.scale), o.name + " has negative scale"

# ---- 7. export, laid out like the right FBX: one take per Action, named exactly like the
# Action, in the right file's take order (rest first). Unity poses an imported model from the
# FIRST take, so this order is what makes the left model's default pose the rest pose. The
# rest pose is also frozen into the pose bones for the file's static node transforms.
with open(SRC, 'rb') as f:
    src_bytes = f.read()

def take_order(a):
    i = src_bytes.find(a.name.encode() + b"\x00\x01AnimStack")
    return i if i >= 0 else (0 if a == rest_action else len(src_bytes))

ordered = sorted(actions, key=take_order)
action_names = [a.name for a in actions]
rest_name = rest_action.name
print("take order", [a.name for a in ordered])

assign(rest_action)
rest_pose = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy()) for pb in arm.pose.bones}
arm.animation_data.action = None
for a in ordered:
    track = arm.animation_data.nla_tracks.new()
    track.name = a.name
    strip = track.strips.new(a.name, int(a.frame_range[0]), a)
    strip.name = a.name
for pb in arm.pose.bones:
    pb.location, pb.rotation_quaternion, pb.scale = rest_pose[pb.name]
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(
    filepath=DST,
    use_selection=False,
    object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_NONE',
    axis_forward='-Z', axis_up='Y',
    mesh_smooth_type='OFF',
    use_mesh_modifiers=False,
    add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X',
    armature_nodetype='NULL',
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    path_mode='AUTO',
)
print("EXPORTED", DST)

# ---- 8. round trip: re-import the written file and check it again
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=DST)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh_ob = next(o for o in bpy.data.objects if o.type == 'MESH')
print("ROUNDTRIP objects", [(o.name, o.type, tuple(round(s, 4) for s in o.scale)) for o in bpy.data.objects])
print("ROUNDTRIP actions", [a.name for a in bpy.data.actions])
print("ROUNDTRIP verts", len(mesh_ob.data.vertices), "polys", len(mesh_ob.data.polygons), "groups", [g.name for g in mesh_ob.vertex_groups])
worst = 0.0
for a in bpy.data.actions:
    assign(a)
    bones, verts = snapshot()
    key = [k for k in reference if k.endswith(a.name.split('|')[-1])][0]
    ref_b, ref_v = reference[key]
    eb_ = max(max((bones[k][0] - ref_b[k][0]).length, (bones[k][1] - ref_b[k][1]).length) for k in bones)
    ev_ = max((v - r).length for v, r in zip(verts, ref_v))
    worst = max(worst, eb_, ev_)
    print("ROUNDTRIP %-34s bone err %.2e m  vertex err %.2e m" % (a.name, eb_, ev_))
print("ROUNDTRIP worst %.2e" % worst)
assert worst < 1e-5, "round trip mismatch"
assert len(bpy.data.actions) == len(actions), "actions lost in export"

# ---- 9. Unity shows a model in its FIRST take: it must be the rest Action, like the right FBX
with open(DST, 'rb') as f:
    out_bytes = f.read()
first = min((out_bytes.find(n.encode() + b"\x00\x01AnimStack"), n) for n in action_names
            if out_bytes.find(n.encode() + b"\x00\x01AnimStack") >= 0)
print("FIRST TAKE", first[1])
assert first[1] == rest_name, "first take is not the rest pose"
