"""
Water source for the Power-to-Methanol plant: demineralised-water storage tank, feed pump
and a reverse-osmosis / polishing skid that supplies the electrolyser.

Run in Blender (Scripting tab > Open > Run Script) or headless:
    blender -b --python water_source.py

Conventions matching the plant's other FBX models:
  * Dimensions below are real metres. MODEL_SCALE = 1/3 because every plant model is
    placed at scale 3 in Unity; set it to 1.0 if you place this one at scale 1.
  * Origin = centre of the concrete pad, pad underside at Z = 0.
  * Raw water enters at -X, treated water leaves at +X (towards the electrolyser).
  * Pipes are named Water_pipe_1..7 in flow order so the Unity flow system can animate them;
    the plant-side connection continues the numbering from 8. The flow system reads each
    segment's direction from its longest axis, so route segments must be longer than wide.
  * Empties WaterSource_Inlet_Point / WaterSource_Outlet_Point mark the two connection faces.

Re-running the script replaces the previous build (only objects in the "WaterSource"
collection are removed).
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

MODEL_SCALE = 1.0 / 3.0
PIPE_R = 0.35                      # plant pipes read ~0.7-0.9 m across; matches that style
EXPORT_FBX = True
EXPORT_PATH = r"C:\Users\Neeta\Downloads\de-vis-proj-group8-dev\Assets\water source.fbx"

S = MODEL_SCALE
COLLECTION = "WaterSource"


# ---- scene ---------------------------------------------------------------------------------

def get_collection():
    coll = bpy.data.collections.get(COLLECTION)
    if coll is None:
        coll = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(coll)
    for ob in list(coll.objects):
        data = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
    return coll


COLL = get_collection()


def material(name, rgb, metallic=0.0, roughness=0.55):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (*rgb, 1.0)
    return mat


M_CONCRETE = material("WS_Concrete", (0.55, 0.55, 0.53), roughness=0.9)
M_TANK = material("WS_TankWhite", (0.90, 0.91, 0.90), roughness=0.45)
M_WATER_BLUE = material("WS_WaterBlue", (0.12, 0.45, 0.80), roughness=0.4)
M_FRP_BLUE = material("WS_VesselBlue", (0.20, 0.42, 0.72), roughness=0.35)
M_STEEL = material("WS_GalvanisedSteel", (0.62, 0.64, 0.67), metallic=0.8, roughness=0.4)
M_YELLOW = material("WS_SafetyYellow", (0.95, 0.74, 0.10), roughness=0.5)
M_PIPE = material("WS_PipeGrey", (0.50, 0.52, 0.55), metallic=0.5, roughness=0.45)
M_MOTOR = material("WS_MotorBlue", (0.08, 0.26, 0.55), metallic=0.3, roughness=0.45)
M_PANEL = material("WS_PanelGrey", (0.80, 0.80, 0.78), roughness=0.5)
M_DARK = material("WS_Dark", (0.05, 0.06, 0.07), roughness=0.3)
M_RED = material("WS_LampRed", (0.85, 0.10, 0.08), roughness=0.3)
M_GREEN = material("WS_LampGreen", (0.10, 0.75, 0.25), roughness=0.3)
M_WHITE = material("WS_TextWhite", (0.97, 0.97, 0.97), roughness=0.5)


# ---- primitive builders (all inputs in real metres) -----------------------------------------

def _finish(name, bm, mat, smooth):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = smooth(poly)
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    COLL.objects.link(ob)
    return ob


def _place(loc, rot=None):
    m = Matrix.Translation(Vector(loc) * S)
    if rot is not None:
        m = m @ rot
    return m


def box(name, size, loc, mat, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(_place(loc, rot) @ Matrix.Diagonal((size[0] * S, size[1] * S, size[2] * S, 1.0)))
    return _finish(name, bm, mat, lambda p: False)


def _axis_rot(axis):
    """Rotation taking local +Z onto the given axis vector."""
    return Vector((0, 0, 1)).rotation_difference(Vector(axis).normalized()).to_matrix().to_4x4()


def cylinder(name, radius, length, loc, mat, axis=(0, 0, 1), segments=24, radius_top=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=radius * S, radius2=(radius if radius_top is None else radius_top) * S,
                          depth=length * S)
    bm.transform(_place(loc, _axis_rot(axis)))
    # Smooth the curved wall, keep the end caps flat.
    return _finish(name, bm, mat, lambda p: len(p.vertices) == 4)


def pipe(name, p0, p1, radius, mat, segments=20):
    a, b = Vector(p0), Vector(p1)
    return cylinder(name, radius, (b - a).length, (a + b) / 2, mat, axis=b - a, segments=segments)


def sphere(name, radius, loc, mat, scale_z=1.0, segments=20):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=segments // 2, radius=radius * S)
    bm.transform(_place(loc) @ Matrix.Diagonal((1.0, 1.0, scale_z, 1.0)))
    return _finish(name, bm, mat, lambda p: True)


def torus(name, major, minor, loc, mat, axis=(0, 0, 1), seg_major=40, seg_minor=8, arc=2 * math.pi):
    bm = bmesh.new()
    closed = abs(arc - 2 * math.pi) < 1e-6
    rings = seg_major if closed else seg_major + 1
    grid = []
    for i in range(rings):
        u = arc * i / seg_major
        cu, su = math.cos(u), math.sin(u)
        ring = []
        for j in range(seg_minor):
            v = 2 * math.pi * j / seg_minor
            r = major + minor * math.cos(v)
            ring.append(bm.verts.new((r * cu * S, r * su * S, minor * math.sin(v) * S)))
        grid.append(ring)
    for i in range(seg_major):
        a, b = grid[i], grid[(i + 1) % rings]
        for j in range(seg_minor):
            k = (j + 1) % seg_minor
            bm.faces.new((a[j], b[j], b[k], a[k]))
    bm.transform(_place(loc, _axis_rot(axis)))
    return _finish(name, bm, mat, lambda p: True)


def flange(name, center, axis, radius, mat=M_PIPE):
    return cylinder(name, radius, 0.1, center, mat, axis=axis, segments=24)


def elbow(name, loc, radius=PIPE_R, mat=M_PIPE):
    return sphere(name, radius * 1.02, loc, mat, segments=16)


def valve(name, loc, stem_axis=(0, 0, 1)):
    body = box(name + "_Body", (0.75, 0.75, 0.75), loc, M_MOTOR)
    stem_top = Vector(loc) + Vector(stem_axis) * 0.8
    pipe(name + "_Stem", loc, stem_top, 0.05, M_STEEL, segments=8)
    torus(name + "_Handwheel", 0.32, 0.04, stem_top, M_RED, axis=stem_axis, seg_major=24, seg_minor=6)
    return body


def marker(name, loc, direction):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "SINGLE_ARROW"
    ob.empty_display_size = 1.0 * S
    ob.location = Vector(loc) * S
    ob.rotation_euler = Vector((0, 0, 1)).rotation_difference(Vector(direction)).to_euler()
    COLL.objects.link(ob)
    return ob


# ---- layout ---------------------------------------------------------------------------------

PAD_TOP = 0.3
TX, TY = -4.2, 0.3                 # tank centre
R = 2.4                            # tank shell radius
SHELL_BOTTOM, SHELL_TOP = 0.5, 6.5
LINE_Y = TY                        # main process line runs along y = TY
PUMP_Z = 1.2                       # tank outlet / pump centreline height
HEADER_Z = 3.2                     # pump discharge run height
OUTLET_Z = 1.8                     # treated-water outlet height

# Foundation
box("WaterSource_Foundation_Pad", (16.0, 9.0, PAD_TOP), (0, 0, PAD_TOP / 2), M_CONCRETE)

# Demineralised-water storage tank
cylinder("WaterSource_Tank_Footing", R + 0.3, 0.2, (TX, TY, PAD_TOP + 0.1), M_CONCRETE, segments=32)
cylinder("WaterSource_Tank_Shell", R, SHELL_TOP - SHELL_BOTTOM, (TX, TY, (SHELL_BOTTOM + SHELL_TOP) / 2),
         M_TANK, segments=40)
cylinder("WaterSource_Tank_Roof", R + 0.1, 0.7, (TX, TY, SHELL_TOP + 0.35), M_TANK, segments=40, radius_top=0.35)
cylinder("WaterSource_Tank_Band", R + 0.015, 0.8, (TX, TY, 4.25), M_WATER_BLUE, segments=40)
for k, z in enumerate((2.0, 3.5, 5.0)):
    torus(f"WaterSource_Tank_Stiffener_{k + 1}", R + 0.03, 0.05, (TX, TY, z), M_TANK, seg_minor=6)

# Nameplate on stand-off brackets (flat sign; flat text on a curved shell looks wrong)
plate_y = TY - R - 0.28
box("WaterSource_Nameplate", (2.4, 0.06, 0.7), (TX, plate_y, 4.25), M_WATER_BLUE)
for k, dx in enumerate((-0.9, 0.9)):
    box(f"WaterSource_Nameplate_Bracket_{k + 1}", (0.08, 0.36, 0.08), (TX + dx, plate_y + 0.2, 4.25), M_STEEL)

def text_mesh(name, body, size, loc, rot_euler, mat):
    cu = bpy.data.curves.new(name + "_curve", "FONT")
    cu.body = body
    cu.size = size * S
    cu.extrude = 0.01 * S
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    tmp = bpy.data.objects.new(name + "_tmp", cu)
    COLL.objects.link(tmp)
    tmp.location = Vector(loc) * S
    tmp.rotation_euler = rot_euler
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(depsgraph))
    me.name = name
    me.materials.clear()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    ob.matrix_world = tmp.matrix_world.copy()
    COLL.objects.link(ob)
    bpy.data.objects.remove(tmp, do_unlink=True)
    bpy.data.curves.remove(cu)
    return ob

text_mesh("WaterSource_Nameplate_Text", "DEMIN WATER", 0.34, (TX, plate_y - 0.045, 4.25),
          (math.radians(90), 0, 0), M_WHITE)

# Manway (front, low)
cylinder("WaterSource_Tank_Manway", 0.4, 0.4, (TX, TY - R - 0.1, 1.3), M_TANK, axis=(0, -1, 0))
flange("WaterSource_Tank_Manway_Cover", (TX, TY - R - 0.33, 1.3), (0, -1, 0), 0.52, M_TANK)

# Level gauge (at -60 degrees)
ga = math.radians(-60)
gx, gy = TX + (R + 0.25) * math.cos(ga), TY + (R + 0.25) * math.sin(ga)
pipe("WaterSource_LevelGauge_Tube", (gx, gy, 1.0), (gx, gy, 6.0), 0.07, M_STEEL, segments=10)
for k, z in enumerate((1.0, 6.0)):
    sx, sy = TX + (R - 0.05) * math.cos(ga), TY + (R - 0.05) * math.sin(ga)
    pipe(f"WaterSource_LevelGauge_Stub_{k + 1}", (sx, sy, z), (gx, gy, z), 0.05, M_STEEL, segments=8)

# Roof vent
pipe("WaterSource_Tank_Vent", (TX + 0.9, TY - 0.5, 6.9), (TX + 0.9, TY - 0.5, 7.6), 0.15, M_TANK, segments=12)
cylinder("WaterSource_Tank_Vent_Cap", 0.32, 0.22, (TX + 0.9, TY - 0.5, 7.7), M_TANK, segments=16, radius_top=0.06)

# Roof handrail
rail_r = R - 0.1
for k in range(16):
    a = 2 * math.pi * k / 16
    x, y = TX + rail_r * math.cos(a), TY + rail_r * math.sin(a)
    pipe(f"WaterSource_Roof_Post_{k + 1}", (x, y, 6.55), (x, y, 7.6), 0.03, M_YELLOW, segments=6)
torus("WaterSource_Roof_Rail_Top", rail_r, 0.035, (TX, TY, 7.6), M_YELLOW, seg_minor=6)
torus("WaterSource_Roof_Rail_Mid", rail_r, 0.03, (TX, TY, 7.1), M_YELLOW, seg_minor=6)

# Caged ladder on the +Y side
lad_y = TY + R + 0.35
for k, dx in enumerate((-0.25, 0.25)):
    pipe(f"WaterSource_Ladder_Rail_{k + 1}", (TX + dx, lad_y, SHELL_BOTTOM), (TX + dx, lad_y, 7.7), 0.03,
         M_YELLOW, segments=6)
z = SHELL_BOTTOM + 0.3
n = 1
while z < 7.6:
    box(f"WaterSource_Ladder_Rung_{n}", (0.5, 0.04, 0.04), (TX, lad_y, z), M_YELLOW)
    z += 0.3
    n += 1
cage_r = 0.5
for k, zc in enumerate([2.6 + 0.9 * i for i in range(6)]):
    torus(f"WaterSource_Ladder_Hoop_{k + 1}", cage_r, 0.04, (TX, lad_y, zc), M_YELLOW,
          seg_major=12, seg_minor=5, arc=math.pi)
for k, a in enumerate((math.radians(30), math.radians(90), math.radians(150))):
    x, y = TX + cage_r * math.cos(a), lad_y + cage_r * math.sin(a)
    pipe(f"WaterSource_Ladder_CageBar_{k + 1}", (x, y, 2.6), (x, y, 7.1), 0.035, M_YELLOW, segments=6)
for k, dx in enumerate((-0.25, 0.25)):
    box(f"WaterSource_Ladder_Bracket_{k + 1}", (0.05, 0.4, 0.05), (TX + dx, TY + R + 0.15, 4.0), M_YELLOW)

# ---- raw water in (-X) -> top of tank --------------------------------------------------------

riser_x = TX - R - 0.75
inlet_face = (-8.0, LINE_Y, PUMP_Z)
flange("WaterSource_Inlet_Flange", (-7.95, LINE_Y, PUMP_Z), (1, 0, 0), PIPE_R + 0.2)
# Shorter than it is wide, so kept out of the flow route like the outlet stub.
pipe("WaterSource_Inlet_Stub", (-7.9, LINE_Y, PUMP_Z), (riser_x, LINE_Y, PUMP_Z), PIPE_R, M_PIPE)
elbow("Water_elbow_1", (riser_x, LINE_Y, PUMP_Z))
pipe("Water_pipe_1", (riser_x, LINE_Y, PUMP_Z), (riser_x, LINE_Y, 6.0), PIPE_R, M_PIPE)
elbow("Water_elbow_2", (riser_x, LINE_Y, 6.0))
pipe("Water_pipe_2", (riser_x, LINE_Y, 6.0), (TX - R + 0.05, LINE_Y, 6.0), PIPE_R, M_PIPE)
box("WaterSource_Riser_Support", (0.15, 0.5, 0.15), (riser_x + 0.3, LINE_Y, 4.0), M_STEEL)
marker("WaterSource_Inlet_Point", inlet_face, (-1, 0, 0))

# ---- tank outlet -> feed pump -----------------------------------------------------------------

outlet_nozzle_x = TX + R + 0.4
cylinder("WaterSource_Tank_Outlet_Nozzle", PIPE_R + 0.02, 0.5, (TX + R + 0.15, LINE_Y, PUMP_Z), M_TANK,
         axis=(1, 0, 0))
flange("WaterSource_Tank_Outlet_Flange", (outlet_nozzle_x, LINE_Y, PUMP_Z), (1, 0, 0), PIPE_R + 0.2)

PUMP_X = 0.0
pipe("Water_pipe_3", (outlet_nozzle_x, LINE_Y, PUMP_Z), (PUMP_X - 0.25, LINE_Y, PUMP_Z), PIPE_R, M_PIPE)
valve("WaterSource_Valve_Suction", ((outlet_nozzle_x + PUMP_X - 0.25) / 2, LINE_Y, PUMP_Z))

# Feed pump: end-suction casing, coupling guard, motor, baseplate
box("WaterSource_Pump_Baseplate", (3.0, 1.2, 0.2), (1.0, LINE_Y, PAD_TOP + 0.1), M_STEEL)
box("WaterSource_Pump_Pedestal", (0.5, 0.6, 0.25), (PUMP_X, LINE_Y, 0.525), M_STEEL)
cylinder("WaterSource_Pump_Casing", 0.55, 0.5, (PUMP_X, LINE_Y, PUMP_Z), M_MOTOR, axis=(1, 0, 0), segments=24)
box("WaterSource_Pump_CouplingGuard", (0.65, 0.45, 0.45), (PUMP_X + 0.575, LINE_Y, PUMP_Z), M_YELLOW)
cylinder("WaterSource_Pump_Motor", 0.45, 1.2, (PUMP_X + 1.5, LINE_Y, PUMP_Z), M_MOTOR, axis=(1, 0, 0), segments=24)
cylinder("WaterSource_Pump_Motor_FanCover", 0.47, 0.18, (PUMP_X + 2.19, LINE_Y, PUMP_Z), M_DARK,
         axis=(1, 0, 0), segments=24)
for k, dx in enumerate((1.1, 1.9)):
    box(f"WaterSource_Pump_Motor_Foot_{k + 1}", (0.25, 0.8, 0.3), (PUMP_X + dx, LINE_Y, 0.6), M_MOTOR)
box("WaterSource_Pump_TerminalBox", (0.35, 0.3, 0.25), (PUMP_X + 1.5, LINE_Y, PUMP_Z + 0.55), M_MOTOR)

# ---- pump discharge -> RO skid feed header ----------------------------------------------------

casing_top = PUMP_Z + 0.55
HEADER_IN_X = 2.45
pipe("Water_pipe_4", (PUMP_X, LINE_Y, casing_top), (PUMP_X, LINE_Y, HEADER_Z), PIPE_R, M_PIPE)
valve("WaterSource_Valve_Discharge", (PUMP_X, LINE_Y, 2.55), stem_axis=(0, -1, 0))
elbow("Water_elbow_3", (PUMP_X, LINE_Y, HEADER_Z))
pipe("Water_pipe_5", (PUMP_X, LINE_Y, HEADER_Z), (HEADER_IN_X, LINE_Y, HEADER_Z), PIPE_R, M_PIPE)
elbow("Water_elbow_4", (HEADER_IN_X, LINE_Y, HEADER_Z))
pipe("Water_pipe_6", (HEADER_IN_X, LINE_Y, HEADER_Z), (HEADER_IN_X, LINE_Y, 0.8), PIPE_R * 0.75, M_PIPE)

# ---- reverse-osmosis skid ---------------------------------------------------------------------

SK_X0, SK_X1 = 2.6, 7.2
SK_Y0, SK_Y1 = LINE_Y - 1.0, LINE_Y + 1.0
SK_TOP = 3.0
post = 0.12
for k, (x, y) in enumerate([(SK_X0, SK_Y0), (SK_X0, SK_Y1), ((SK_X0 + SK_X1) / 2, SK_Y0),
                            ((SK_X0 + SK_X1) / 2, SK_Y1), (SK_X1, SK_Y0), (SK_X1, SK_Y1)]):
    box(f"WaterSource_RO_Post_{k + 1}", (post, post, SK_TOP - PAD_TOP), (x, y, (PAD_TOP + SK_TOP) / 2), M_STEEL)
for k, (z, name) in enumerate([(0.4, "Base"), (SK_TOP, "Top")]):
    for j, y in enumerate((SK_Y0, SK_Y1)):
        box(f"WaterSource_RO_{name}Beam_X_{j + 1}", (SK_X1 - SK_X0 + post, post, post),
            ((SK_X0 + SK_X1) / 2, y, z), M_STEEL)
    for j, x in enumerate((SK_X0, (SK_X0 + SK_X1) / 2, SK_X1)):
        box(f"WaterSource_RO_{name}Beam_Y_{j + 1}", (post, SK_Y1 - SK_Y0, post), (x, LINE_Y, z), M_STEEL)

# Pressure-vessel membrane housings: 3 high x 2 wide
housing_x0, housing_x1 = 2.85, 6.95
tube_rows = (0.9, 1.5, 2.1)
tube_cols = (LINE_Y - 0.5, LINE_Y + 0.5)
n = 1
for z in tube_rows:
    for y in tube_cols:
        pipe(f"WaterSource_RO_Membrane_{n}", (housing_x0, y, z), (housing_x1, y, z), 0.2, M_TANK, segments=18)
        for e, x in enumerate((housing_x0, housing_x1)):
            cylinder(f"WaterSource_RO_Membrane_{n}_EndCap_{e + 1}", 0.24, 0.12, (x, y, z), M_FRP_BLUE,
                     axis=(1, 0, 0), segments=18)
        pipe(f"WaterSource_RO_FeedStub_{n}", (HEADER_IN_X, LINE_Y, z), (housing_x0 - 0.06, y, z), 0.07, M_PIPE,
             segments=8)
        pipe(f"WaterSource_RO_PermeateStub_{n}", (housing_x1 + 0.06, y, z), (7.4, LINE_Y, z), 0.07, M_PIPE,
             segments=8)
        n += 1

# Permeate header -> treated-water outlet (+X)
pipe("Water_pipe_7", (7.4, LINE_Y, 0.8), (7.4, LINE_Y, 2.4), PIPE_R * 0.75, M_PIPE)
# Shorter than it is wide, so it is kept out of the flow route (route direction comes from
# each segment's longest axis). The Unity-side connection continues the route from here.
pipe("WaterSource_Outlet_Stub", (7.4, LINE_Y, OUTLET_Z), (7.9, LINE_Y, OUTLET_Z), PIPE_R, M_PIPE)
flange("WaterSource_Outlet_Flange", (7.95, LINE_Y, OUTLET_Z), (1, 0, 0), PIPE_R + 0.2)
marker("WaterSource_Outlet_Point", (8.0, LINE_Y, OUTLET_Z), (1, 0, 0))

# ---- mixed-bed polishers (beside the skid) ----------------------------------------------------

for k, x in enumerate((3.4, 5.2)):
    y = 3.3
    cylinder(f"WaterSource_Polisher_{k + 1}_Shell", 0.55, 1.9, (x, y, 1.65), M_FRP_BLUE, segments=24)
    sphere(f"WaterSource_Polisher_{k + 1}_Dome", 0.55, (x, y, 2.6), M_FRP_BLUE, scale_z=0.55)
    sphere(f"WaterSource_Polisher_{k + 1}_Bottom", 0.55, (x, y, 0.7), M_FRP_BLUE, scale_z=0.35)
    for j in range(4):
        a = math.radians(45 + 90 * j)
        box(f"WaterSource_Polisher_{k + 1}_Leg_{j + 1}", (0.08, 0.08, 0.45),
            (x + 0.45 * math.cos(a), y + 0.45 * math.sin(a), 0.52), M_STEEL)
    pipe(f"WaterSource_Polisher_{k + 1}_Link", (x, y, 2.85), (x, SK_Y1, 2.85), 0.08, M_PIPE, segments=8)

# ---- local control panel ----------------------------------------------------------------------

PX, PY = 6.6, -3.3
box("WaterSource_ControlPanel", (1.2, 0.5, 2.0), (PX, PY, PAD_TOP + 1.0), M_PANEL)
box("WaterSource_ControlPanel_Plinth", (1.3, 0.6, 0.15), (PX, PY, PAD_TOP + 0.075), M_CONCRETE)
box("WaterSource_ControlPanel_Screen", (0.55, 0.02, 0.38), (PX, PY - 0.26, PAD_TOP + 1.55), M_DARK)
box("WaterSource_ControlPanel_DoorSeam", (0.01, 0.02, 1.8), (PX, PY - 0.26, PAD_TOP + 1.0), M_DARK)
for k, (dx, mat) in enumerate(((-0.3, M_GREEN), (-0.15, M_YELLOW), (0.0, M_RED))):
    cylinder(f"WaterSource_ControlPanel_Lamp_{k + 1}", 0.04, 0.04, (PX + dx, PY - 0.27, PAD_TOP + 1.15), mat,
             axis=(0, -1, 0), segments=10)

# ---- export -----------------------------------------------------------------------------------

print(f"WaterSource: built {len(COLL.objects)} objects "
      f"({sum(len(o.data.polygons) for o in COLL.objects if o.type == 'MESH')} faces)")

if EXPORT_FBX:
    for ob in bpy.context.view_layer.objects:
        ob.select_set(False)
    for ob in COLL.objects:
        ob.select_set(True)
    os.makedirs(os.path.dirname(EXPORT_PATH), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=EXPORT_PATH,
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
    )
    print("WaterSource: exported", EXPORT_PATH)
