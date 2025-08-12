#! python 3
# Rhino → Maestro XCS-Generator (Selektion → XCS)
# Features:
#   WK_PIECE                                         -> Außenkontur (DX/DY)
#   CUT_E010[_Z20][_S5][_D9.5]                       -> Konturfräsen
#   POCKET_E010[_Z12][_S3][_D8][_O5]                 -> Pocket (konzentrische Offsets)
#   DRILL_D4.5[_Z17][_C P|L]                         -> Einzelbohrungen (Kreis/Punkt)
#   DRILLROW_D5_Z17_P32[_N10]                        -> Lochreihe entlang Kurve
#   RBNUT_CH_X_W6[_Z8][_S2][_E015]_[M|P]             -> Rückwandnut (Channel), Linie→Rechteck
#   RBNUT_CH_Y_W6[_Z8][_S2][_E015]_[M|P]
#   RBNUT_RNT_X_W6[_Z8]_C066_[M|P]                   -> Rückwandnut via RNT-Makro (X/Y, Mitte/Positiv)
#   RBNUT_RNT_Y_W6[_Z8]_C066_[M|P]

import rhinoscriptsyntax as rs
import Rhino
import System
import re, math
from math import isclose

# ---------------- Einstellungen ----------------
DZ = 19.0                               # Werkstückdicke fix
SETUP_OFFSET = (2.5, 2.5, 0.0, 0.0)     # x, y, z, rot
LAYER_PIECE = "WK_PIECE"
DEFAULT_TOOL_DIA = 9.5                  # mm (Fallback)
DEFAULT_POCKET_STEPOVER = 0.7           # *ToolØ (für Pocket-Offsetschritt)
GROOVE_OVERTRAVEL = 5.0                 # mm Überlauf an Nutenden
POLY_TOL = 0.05                         # mm – Kurve→Polyline
DOC = Rhino.RhinoDoc.ActiveDoc
TOL = DOC.ModelAbsoluteTolerance if DOC else 0.01
USE_CORNER_ROUNDING = False             # False = CreateIso gar nicht ausgeben

# --- RNT Makro Steuerung ---

USE_RNT_MACRO = True

RNT_TEMPLATE_X = (
    'CreateMacro("{name}","RNT",'
    '{x_start:.3f},{y_center:.3f},{width:.3f},-1,-1,-1,{x_len:.3f},{depth:.3f},true,'
    '"{code}","-1",false,false,true,{y_center:.3f},null,null,null,null,true);'
)

RNT_TEMPLATE_Y = (
    'CreateMacro("{name}","RNT",'
    '{x_center:.3f},{y_start:.3f},{width:.3f},-1,-1,-1,{y_len:.3f},{depth:.3f},true,'
    '"{code}","-1",false,false,true,{x_center:.3f},null,null,null,null,true);'
)


# ---------------- Eindeutige Namen ----------------
MAX_NAME_LEN = 31

class UniqueNames:
    def __init__(self, max_len=MAX_NAME_LEN):
        self.used = set()
        self.counts = {}
        self.max_len = max_len
    def sanitize(self, s):
        s = re.sub(r'[^A-Za-z0-9_]', '_', s)
        return s[:self.max_len] if len(s) > self.max_len else s
    def get(self, base):
        base = self.sanitize(base)
        if base not in self.used and self.counts.get(base, 0) == 0:
            self.used.add(base); self.counts[base] = 1
            return base
        n = self.counts.get(base, 1) + 1
        while True:
            cand = self.sanitize(f"{base}_{n}")
            if cand not in self.used:
                self.used.add(cand); self.counts[base] = n
                return cand
            n += 1

NAME = UniqueNames()
def UNIQ(base): return NAME.get(base)

# ---------------- Regex für Layer ----------------
CUT_RX    = re.compile(r"^CUT_(?P<tech>E(?P<num>\d{2,3}))(?:_Z(?P<depth>\d+(?:\.\d+)?))?(?:_S(?P<sd>\d+(?:\.\d+)?))?(?:_D(?P<dia>\d+(?:\.\d+)?))?$", re.I)
DRILL_RX  = re.compile(r"^DRILL_D(?P<dia>\d+(?:\.\d+)?)(?:_Z(?P<depth>\d+(?:\.\d+)?))?(?:_C(?P<code>[PL]))?$", re.I)
ROW_RX    = re.compile(r"^DRILLROW_D(?P<dia>\d+(?:\.\d+)?)(?:_Z(?P<depth>\d+(?:\.\d+)?))?_P(?P<pitch>\d+(?:\.\d+)?)(?:_N(?P<count>\d+))?$", re.I)
POCKET_RX = re.compile(r"^POCKET_(?P<tech>E(?P<num>\d{2,3}))(?:_Z(?P<depth>\d+(?:\.\d+)?))?(?:_S(?P<sd>\d+(?:\.\d+)?))?(?:_D(?P<dia>\d+(?:\.\d+)?))?(?:_O(?P<ostep>\d+(?:\.\d+)?))?$", re.I)
RBNUT_CH_RX = re.compile(
    r"^RBNUT_CH_(?P<axis>[XY])_W(?P<w>\d+(?:\.\d+)?)"
    r"(?:_Z(?P<depth>\d+(?:\.\d+)?))?"
    r"(?:_S(?P<sd>\d+(?:\.\d+)?))?"
    r"(?:_E(?P<technum>\d{2,3}))?"
    r"(?:_(?P<place>[MP]))?$",
    re.IGNORECASE
)
# RNT-Makro-Layer
RBNUT_RNT_RX = re.compile(
    r"^RBNUT_RNT_(?P<axis>[XY])_W(?P<w>\d+(?:\.\d+)?)"
    r"(?:_Z(?P<depth>\d+(?:\.\d+)?))?_C(?P<code>\d{3})"
    r"(?:_(?P<place>[MP]))?$",
    re.IGNORECASE
)

def normalize_tech(tech, num_str):
    try: return "E{:03d}".format(int(num_str))
    except: return tech.upper()

# ---------------- Geometrie-Helfer ----------------
def curve_is_closed(crv):
    try: return crv.IsClosed
    except: return False

def curve_area(crv):
    amp = Rhino.Geometry.AreaMassProperties.Compute(crv)
    return amp.Area if amp else 0.0

def to_poly_points(crv, tol=0.1):
    plc = crv.ToPolyline(tol, tol, 0, 0)
    if not plc:
        plc = crv.ToPolyline(tol*0.5, tol*0.5, 0, 0)
        if not plc: return None
    pts = [(pt.X, pt.Y) for pt in plc.ToPolyline()]
    if pts and (not isclose(pts[0][0], pts[-1][0]) or not isclose(pts[0][1], pts[-1][1])):
        pts.append(pts[0])
    return pts if len(pts) >= 4 else None

def bbox_xy(crv):
    bb = crv.GetBoundingBox(True)
    return (bb.Max.X - bb.Min.X), (bb.Max.Y - bb.Min.Y)

def orientation_sign(crv):
    ori = Rhino.Geometry.Curve.ClosedCurveOrientation(crv, Rhino.Geometry.Plane.WorldXY)
    return -1 if ori == Rhino.Geometry.CurveOrientation.CounterClockwise else +1

def inside_offsets(curv, step_dist):
    res, current = [], curv
    sign = orientation_sign(current)
    dist = -sign * step_dist  # innen
    while True:
        arr = current.Offset(Rhino.Geometry.Plane.WorldXY, dist, TOL, Rhino.Geometry.CurveOffsetCornerStyle.Sharp)
        if not arr or arr.Count == 0: break
        best = max(arr, key=lambda c: curve_area(c))
        if curve_area(best) <= 1e-6: break
        res.append(best); current = best
    return res

# Rückwandnut: Linie → Rechteckpunkte (geschlossen)
def build_groove_rect_from_line(line_crv, axis, w, place, overtravel):
    p0 = line_crv.PointAtStart
    p1 = line_crv.PointAtEnd
    x0, y0, x1, y1 = p0.X, p0.Y, p1.X, p1.Y

    if axis == 'X':
        y = 0.5*(y0 + y1)
        x_start = min(x0, x1) - overtravel
        x_end   = max(x0, x1) + overtravel
        if place == 'M':
            y_lo, y_hi = y - 0.5*w, y + 0.5*w
        else:  # P → Y+
            y_lo, y_hi = y, y + w
        return [(x_start, y_lo), (x_end, y_lo), (x_end, y_hi), (x_start, y_hi), (x_start, y_lo)]

    else:  # axis == 'Y'
        x = 0.5*(x0 + x1)
        y_start = min(y0, y1) - overtravel
        y_end   = max(y0, y1) + overtravel
        if place == 'M':
            x_lo, x_hi = x - 0.5*w, x + 0.5*w
        else:  # P → X+
            x_lo, x_hi = x, x + w
        return [(x_lo, y_start), (x_hi, y_start), (x_hi, y_end), (x_lo, y_end), (x_lo, y_start)]

# Für RNT: Start/Ende/Mitte bestimmen
def groove_endpoints_from_line(line_crv, axis, place, width, overtravel):
    p0 = line_crv.PointAtStart; p1 = line_crv.PointAtEnd
    x0,y0,x1,y1 = p0.X, p0.Y, p1.X, p1.Y
    if axis == 'X':
        y_center = 0.5*(y0+y1)
        x_start  = min(x0,x1) - overtravel
        x_end    = max(x0,x1) + overtravel
        if place == 'M':
            return dict(x_start=x_start,x_end=x_end,
                        y_center=y_center, y_start=y_center-width*0.5, y_end=y_center+width*0.5)
        else:  # P → Y+
            return dict(x_start=x_start,x_end=x_end,
                        y_center=y_center, y_start=y_center, y_end=y_center+width)
    else:  # 'Y'
        x_center = 0.5*(x0+x1)
        y_start  = min(y0,y1) - overtravel
        y_end    = max(y0,y1) + overtravel
        if place == 'M':
            return dict(y_start=y_start,y_end=y_end,
                        x_center=x_center, x_start=x_center-width*0.5, x_end=x_center+width*0.5)
        else:  # P → X+
            return dict(y_start=y_start,y_end=y_end,
                        x_center=x_center, x_start=x_center, x_end=x_center+width)

# ---------------- XCS-Ausgabe ----------------
def xcs_header(name, dx, dy, dz):
    return "\n".join([
        '// *** Programm created by Rhino→Maestro Generator ***',
        'SetMachiningParameters("IJ",1,10,196608,false);',
        f'CreateFinishedWorkpieceBox("{name}", {dx:.3f}, {dy:.3f}, {dz:.3f});',
        f'double DZ = {dz:.3f};',
        f'SetWorkpieceSetupPosition({SETUP_OFFSET[0]},{SETUP_OFFSET[1]},{SETUP_OFFSET[2]},{SETUP_OFFSET[3]});',
        ''
    ])

def xcs_polyline_pass(poly_name, op_name, pts, tech, depth, tool_dia, plane="Top"):
    lines = [f'SelectWorkplane("{plane}");']
    x0, y0 = pts[0]
    lines.append(f'CreatePolyline("{poly_name}", {x0:.3f},{y0:.3f});')
    for x, y in pts[1:]:
        lines.append(f'AddSegmentToPolyline({x:.3f},{y:.3f});')

    if USE_CORNER_ROUNDING:
        iso_name = UNIQ(f"ISO_AEA_{op_name}")  # eindeutig
        lines.append(f'CreateIso("{iso_name}","CUTOCOK=0","",false);')

    lines += [
        'SetCompensationMode(false);',
        'SetApproachStrategy(false,true,2);',
        'SetRetractStrategy(false,true,2.0,2);',
        'SetPneumaticHoodPosition(null);',
        f'CreateRoughFinish("{op_name}",{depth:.3f},"", TypeOfProcess.GeneralRouting ,"{tech}","-1",2,-1,-1,-1,0);',
        'ResetApproachStrategy();',
        'ResetRetractStrategy();',
        ''
    ]
    return "\n".join(lines)

def xcs_drill(name, x, y, depth, dia, plane="Top", side="P"):
    return "\n".join([
        f'SelectWorkplane("{plane}");',
        f'CreateDrill("{name}",{x:.3f},{y:.3f},{depth:.3f},{dia:.3f},"",TypeOfProcess.Drilling,"-1","-1",1,-1,-1,"{side}");',
        'ResetPattern();',
        ''
    ])

# ---------------- Layer-Parser ----------------
def parse_cut_layer(layer_name, dz_default=DZ):
    m = CUT_RX.match(layer_name or "")
    if not m: return None
    tech = normalize_tech(m.group("tech"), m.group("num"))
    depth = float(m.group("depth")) if m.group("depth") else dz_default
    sd = float(m.group("sd")) if m.group("sd") else None
    dia = float(m.group("dia")) if m.group("dia") else DEFAULT_TOOL_DIA
    return tech, depth, sd, dia

def parse_drill_layer(layer_name, dz_default=DZ):
    m = DRILL_RX.match(layer_name or "")
    if not m: return None
    dia = float(m.group("dia"))
    depth = float(m.group("depth")) if m.group("depth") else dz_default
    code = (m.group("code") or "P").upper()
    return dia, depth, code

def parse_row_layer(layer_name, dz_default=DZ):
    m = ROW_RX.match(layer_name or "")
    if not m: return None
    dia = float(m.group("dia"))
    depth = float(m.group("depth")) if m.group("depth") else dz_default
    pitch = float(m.group("pitch"))
    count = int(m.group("count")) if m.group("count") else None
    return dia, depth, pitch, count

def parse_pocket_layer(layer_name, dz_default=DZ):
    m = POCKET_RX.match(layer_name or "")
    if not m: return None
    tech = normalize_tech(m.group("tech"), m.group("num"))
    depth = float(m.group("depth")) if m.group("depth") else dz_default
    sd = float(m.group("sd")) if m.group("sd") else None
    dia = float(m.group("dia")) if m.group("dia") else DEFAULT_TOOL_DIA
    ostep = float(m.group("ostep")) if m.group("ostep") else dia * DEFAULT_POCKET_STEPOVER
    return tech, depth, sd, dia, ostep

def parse_rbnut_ch_layer(layer_name, dz_default=DZ):
    m = RBNUT_CH_RX.match(layer_name or "")
    if not m: return None
    axis = m.group("axis").upper()
    w = float(m.group("w"))
    depth = float(m.group("depth")) if m.group("depth") else dz_default
    sd = float(m.group("sd")) if m.group("sd") else None
    tech = "E{:03d}".format(int(m.group("technum"))) if m.group("technum") else "E010"
    place = (m.group("place") or "M").upper()   # M = mittig, P = positiv (Y+ bei X-Nut, X+ bei Y-Nut)
    return axis, w, depth, sd, tech, place

def parse_rbnut_rnt_layer(layer_name, dz_default=DZ):
    m = RBNUT_RNT_RX.match(layer_name or "")
    if not m: return None
    axis  = m.group("axis").upper()
    w     = float(m.group("w"))
    depth = float(m.group("depth")) if m.group("depth") else dz_default
    code  = m.group("code")
    place = (m.group("place") or "M").upper()  # M=center, P=positive side
    tech  = "E010"  # irrelevant für Makro; halten für Konsistenz
    return axis, w, depth, code, place, tech

# ---------------- Emitter ----------------
def emit_cut_operation(parts, base_name, pts, layer_name, mode_layer_stepdown):
    parsed = parse_cut_layer(layer_name, DZ)
    if not parsed: return
    tech, depth_total, stepdown, tool_dia = parsed
    if mode_layer_stepdown and stepdown is not None:
        n = int(math.ceil(depth_total / stepdown))
        for i in range(1, n+1):
            z_i = min(i*stepdown, depth_total)
            poly_name = UNIQ(f"{base_name}_Z{z_i:.1f}")
            op_name   = UNIQ(f"{base_name}_Z{z_i:.1f}_OP")
            parts.append(xcs_polyline_pass(poly_name, op_name, pts, tech, z_i, tool_dia))
    else:
        poly_name = UNIQ(f"{base_name}")
        op_name   = UNIQ(f"{base_name}_OP")
        parts.append(xcs_polyline_pass(poly_name, op_name, pts, tech, depth_total, tool_dia))

def emit_drill(parts, base_name, x, y, layer_name):
    parsed = parse_drill_layer(layer_name, DZ)
    if not parsed: return
    dia, depth, code = parsed
    unique = UNIQ(base_name)
    parts.append(xcs_drill(unique, x, y, depth, dia, "Top", code))

def emit_drill_row(parts, base_name, crv, layer_name):
    parsed = parse_row_layer(layer_name, DZ)
    if not parsed: return
    dia, depth, pitch, count = parsed
    length = crv.GetLength()
    if count is None:
        count = int(math.floor(length / pitch)) + 1
    for i in range(count):
        s = min(i * pitch, length)
        ok, t = crv.LengthParameter(s)
        if not ok: continue
        pt = crv.PointAt(t)
        unique = UNIQ(f"{base_name}_{i+1}")
        parts.append(xcs_drill(unique, pt.X, pt.Y, depth, dia, "Top", "P"))

def emit_pocket(parts, base_name, crv, layer_name, mode_layer_stepdown):
    parsed = parse_pocket_layer(layer_name, DZ)
    if not parsed: return
    tech, depth_total, stepdown, tool_dia, ostep = parsed
    pts_outer = to_poly_points(crv, POLY_TOL)
    if not pts_outer: return

    def passes_at_depth(label, depth_val, curve):
        loops = [curve] + inside_offsets(curve, ostep)
        for j, loop in enumerate(loops):
            pts = to_poly_points(loop, POLY_TOL)
            if not pts: continue
            poly_name = UNIQ(f"{label}_ring{j+1}")
            op_name   = UNIQ(f"{label}_ring{j+1}_OP")
            parts.append(xcs_polyline_pass(poly_name, op_name, pts, tech, depth_val, tool_dia))

    if mode_layer_stepdown and stepdown is not None:
        n = int(math.ceil(depth_total / stepdown))
        for i in range(1, n+1):
            z_i = min(i*stepdown, depth_total)
            passes_at_depth(f"{base_name}_Z{z_i:.1f}", z_i, crv)
    else:
        passes_at_depth(f"{base_name}", depth_total, crv)

def emit_rbnut_channel(parts, base_name, crv, layer_name, mode_layer_stepdown):
    parsed = parse_rbnut_ch_layer(layer_name, DZ)
    if not parsed: return
    axis, w, depth_total, stepdown, tech, place = parsed

    rect_pts = build_groove_rect_from_line(crv, axis, w, place, GROOVE_OVERTRAVEL)
    if not rect_pts: return

    if mode_layer_stepdown and stepdown is not None:
        n = int(math.ceil(depth_total / stepdown))
        for i in range(1, n+1):
            z_i = min(i*stepdown, depth_total)
            poly_name = UNIQ(f"{base_name}_Z{z_i:.1f}")
            op_name   = UNIQ(f"{base_name}_Z{z_i:.1f}_OP")
            parts.append(xcs_polyline_pass(poly_name, op_name, rect_pts, tech, z_i, DEFAULT_TOOL_DIA))
    else:
        poly_name = UNIQ(f"{base_name}")
        op_name   = UNIQ(f"{base_name}_OP")
        parts.append(xcs_polyline_pass(poly_name, op_name, rect_pts, tech, depth_total, DEFAULT_TOOL_DIA))

def emit_rbnut_rnt(parts, base_name, crv, layer_name):
    if not USE_RNT_MACRO: return
    parsed = parse_rbnut_rnt_layer(layer_name, DZ)
    if not parsed: return
    axis, w, depth, code, place, _tech = parsed

    ends = groove_endpoints_from_line(crv, axis, place, w, GROOVE_OVERTRAVEL)

    # Name – gern sprechend wie bei CAD+T:
    nice = "Nut in X-Richtung" if axis == 'X' else "Nut in Y-Richtung"
    macro_name = UNIQ(nice)

    data = {
        'name': macro_name,
        'width': w,
        'depth': depth,
        'code': code,
        'x_start':  ends.get('x_start',  0.0),
        'x_end':    ends.get('x_end',    0.0),
        'y_start':  ends.get('y_start',  0.0),
        'y_end':    ends.get('y_end',    0.0),
        'x_center': ends.get('x_center', 0.0),
        'y_center': ends.get('y_center', 0.0),
        'x_len':    abs(ends.get('x_end',0.0) - ends.get('x_start',0.0)),
        'y_len':    abs(ends.get('y_end',0.0) - ends.get('y_start',0.0)),
    }
    parts.append('SelectWorkplane("Top");')
    if axis == 'X':
        parts.append(RNT_TEMPLATE_X.format(**data))
    else:
        parts.append(RNT_TEMPLATE_Y.format(**data))
    parts.append('')

# ---------------- Hauptprogramm ----------------
def main():
    ids = rs.GetObjects("Wähle Objekte (WK_PIECE, CUT_*, POCKET_*, DRILL_*, DRILLROW_*, RBNUT_CH_*, RBNUT_RNT_*)",
                        preselect=True, select=False, filter=rs.filter.allobjects)
    if not ids:
        rs.MessageBox("Keine Objekte ausgewählt.", 48, "Abbruch"); return

    choice = rs.ListBox(
        items=["A: Technologie-Stepdown (Standard)", "B: Layer-Stepdown (_Sxx)"],
        message="Wie sollen Z-Schritte erzeugt werden?",
        title="Zustellstrategie wählen"
    )
    if not choice: return
    mode_layer_stepdown = choice.startswith("B")

    piece_curves = []
    cuts, drills, rows, pockets, grooves, grooves_rnt = [], [], [], [], [], []

    for obj_id in ids:
        layer = rs.ObjectLayer(obj_id) or ""
        obj = rs.coercerhinoobject(obj_id)
        if not obj: continue
        crv = rs.coercecurve(obj_id)

        if crv and layer == LAYER_PIECE and curve_is_closed(crv):
            piece_curves.append(crv); continue
        if crv and CUT_RX.match(layer) and curve_is_closed(crv):
            cuts.append((crv, layer)); continue
        if crv and POCKET_RX.match(layer) and curve_is_closed(crv):
            pockets.append((crv, layer)); continue
        if crv and ROW_RX.match(layer) and not curve_is_closed(crv):
            rows.append((crv, layer)); continue
        if DRILL_RX.match(layer):
            if obj.Geometry.ObjectType == Rhino.DocObjects.ObjectType.Circle:
                c = obj.Geometry; center = c.Center
                drills.append((center.X, center.Y, layer)); continue
            if obj.Geometry.ObjectType == Rhino.DocObjects.ObjectType.Point:
                p = obj.Geometry.Location
                drills.append((p.X, p.Y, layer)); continue
            if crv and isinstance(crv, Rhino.Geometry.ArcCurve) and crv.IsCircle():
                circ = crv.Circle; center = circ.Center
                drills.append((center.X, center.Y, layer)); continue
        if crv and RBNUT_CH_RX.match(layer):
            grooves.append((crv, layer)); continue
        if crv and RBNUT_RNT_RX.match(layer):
            grooves_rnt.append((crv, layer)); continue

    if not piece_curves:
        rs.MessageBox("Keine geschlossene Außenkontur auf Layer 'WK_PIECE' gefunden.", 48, "Abbruch")
        return

    outer = max(piece_curves, key=curve_area)
    dx, dy = bbox_xy(outer)

    filt = "Xilog Script (*.xcs)|*.xcs||"
    xcs_path = rs.SaveFileName("Speichere XCS", filt)
    if not xcs_path: return
    if not xcs_path.lower().endswith(".xcs"): xcs_path += ".xcs"
    name = System.IO.Path.GetFileNameWithoutExtension(xcs_path)

    parts = [xcs_header(name, dx, dy, DZ)]

    # Fallback: wenn gar nichts außer WK_PIECE da ist → Außenkontur fräsen
    if not cuts and not pockets and not drills and not rows and not grooves and not grooves_rnt:
        pts = to_poly_points(outer, POLY_TOL)
        if pts:
            poly_name = UNIQ("Aussenkontur")
            op_name   = UNIQ("Aussenkontur_OP")
            parts.append(xcs_polyline_pass(poly_name, op_name, pts, "E010", DZ, DEFAULT_TOOL_DIA))

    # CUT
    for idx, (crv, layer_name) in enumerate(cuts, 1):
        pts = to_poly_points(crv, POLY_TOL)
        if pts:
            emit_cut_operation(parts, f"CUT_{idx}", pts, layer_name, mode_layer_stepdown)

    # POCKET
    for idx, (crv, layer_name) in enumerate(pockets, 1):
        emit_pocket(parts, f"POCKET_{idx}", crv, layer_name, mode_layer_stepdown)

    # DRILL
    for idx, (x, y, layer_name) in enumerate(drills, 1):
        emit_drill(parts, f"DRILL_{idx}", x, y, layer_name)

    # DRILLROW
    for idx, (crv, layer_name) in enumerate(rows, 1):
        emit_drill_row(parts, f"DRILLROW_{idx}", crv, layer_name)

    # RBNUT per RNT-Makro
    for idx, (crv, layer_name) in enumerate(grooves_rnt, 1):
        emit_rbnut_rnt(parts, f"RBNUT_RNT_{idx}", crv, layer_name)

    # RBNUT (Channel)
    for idx, (crv, layer_name) in enumerate(grooves, 1):
        emit_rbnut_channel(parts, f"RBNUT_{idx}", crv, layer_name, mode_layer_stepdown)

    parts.append('CreateMacro("Wegfahrschritt","XPARK");\n')

    try:
        with open(xcs_path, "w", encoding="utf-8") as f:
            f.write("\n".join(parts))
        rs.MessageBox("XCS erstellt:\n{}".format(xcs_path), 64, "Fertig")
    except Exception as ex:
        rs.MessageBox("Fehler beim Schreiben:\n{}".format(ex), 16, "Fehler")

if __name__ == "__main__":
    main()
