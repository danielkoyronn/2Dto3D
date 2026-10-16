from __future__ import annotations

"""Build the minimal factory placement table used by Process Simulate.

Workflow
--------
1. Read the source DXF.
2. Reuse verified/corrected robot frames when available; otherwise detect Robot
   INSERTs directly from the DXF.
3. Detect Safe Fence placements from fence INSERTs and fence-layer line/polyline
   segments.
4. Write one deterministic semicolon-separated frame table for Process Simulate.

Output naming standard
----------------------
Robot:       <AREA>_ROBOT_<NNN>
Track robot: <AREA>_TRACK_ROBOT_<NNN>
Fence:       <AREA>_FENCE_<NNN>

Corrected MID_MAIN names such as MID_MAIN_BM02_ROBOT_R01 are preserved.
"""

import argparse
import math
import re
from dataclasses import dataclass
from pathlib import Path

import ezdxf
import pandas as pd


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUT = REPO_ROOT / "output" / "1013"

ROBOT_LAYER_RE = re.compile(r"(?i)(?:^|[_\- ])ROBOT(?:$|[_\- ])|010_ROBOT|機器人|机器人")
ROBOT_BLOCK_RE = re.compile(r"(?i)(R-2000|KR\s*\d+|ROBOT)")
TRACK_RE = re.compile(r"(?i)(TRACK|TRACKER|RAIL|7TH|7TH_AXIS|軌道|轨道|履帶|履带)")
FENCE_RE = re.compile(r"(?i)(FENCE|SAFE[_\- ]?FENCE|SAFETY[_\- ]?FENCE|SF[-_ ]?\d*|安全圍籬|安全围栏|安全柵欄|安全栅栏|安全門|安全门)")

# Current 1013 layout areas. More specific/overlapping areas are checked first.
AREA_BOUNDS = [
    ("BSL_AUX", 941617.149078, 959617.149078, -1952582.839094, -1942582.839094),
    ("BSR_AUX", 963617.149078, 983617.149078, -1952582.839094, -1942582.839094),
    ("MID_AUX", 1012617.149078, 1034617.149078, -1988582.839094, -1977582.787282),
    ("BOTTOM", 941617.149078, 1034617.149078, -2001582.839094, -1994582.839094),
    ("BSL", 939617.149078, 959617.149078, -1939582.839094, -1896582.839094),
    ("BSR", 963617.149078, 983617.149078, -1939582.839094, -1896582.839094),
    ("EC", 988617.149078, 1009017.149078, -1952582.839094, -1896582.839094),
    ("RR", 1014017.149050, 1030617.149050, -1952582.839094, -1896582.839094),
    ("MID_MAIN", 941617.149078, 1034617.149078, -1988582.839094, -1957582.839094),
]


@dataclass(frozen=True)
class FrameSpec:
    name: str
    type: str
    area: str
    x: float
    y: float
    z: float = 0.0
    rx: float = 0.0
    ry: float = 0.0
    rz: float = 0.0
    source_layer: str = ""
    source_block: str = ""
    source_entity: str = ""
    confidence: str = ""


def area_for(x: float, y: float) -> str:
    for name, xmin, xmax, ymin, ymax in AREA_BOUNDS:
        if xmin <= x <= xmax and ymin <= y <= ymax:
            return name
    return "FACTORY"


def norm_deg(value: float) -> float:
    value = float(value) % 360.0
    return 0.0 if abs(value - 360.0) < 1e-9 else value


def is_robot_insert(layer: str, block: str) -> bool:
    return bool(ROBOT_LAYER_RE.search(layer) or ROBOT_BLOCK_RE.search(block))


def is_track(layer: str, block: str) -> bool:
    return bool(TRACK_RE.search(layer) or TRACK_RE.search(block))


def is_fence(layer: str, block: str = "") -> bool:
    return bool(FENCE_RE.search(layer) or FENCE_RE.search(block))


def load_corrected_robot_frames(path: Path) -> list[FrameSpec]:
    df = pd.read_csv(path)
    name_col = "CorrectedFrameName" if "CorrectedFrameName" in df.columns else "FrameName"
    x_col = "x" if "x" in df.columns else "X"
    y_col = "y" if "y" in df.columns else "Y"
    z_col = next((c for c in ("z", "Z", "Z_mm") if c in df.columns), None)
    rz_col = next((c for c in ("Rz_deg", "rz", "Rz", "rotation", "Rotation") if c in df.columns), None)
    area_col = "Area" if "Area" in df.columns else None
    layer_col = "SourceLayer" if "SourceLayer" in df.columns else None
    block_col = "SourceBlock" if "SourceBlock" in df.columns else None

    result: list[FrameSpec] = []
    for _, row in df.iterrows():
        name = str(row[name_col]).strip()
        if not name or "_ROBOT_" not in name.upper():
            continue
        x = float(row[x_col])
        y = float(row[y_col])
        z = float(row[z_col]) if z_col and pd.notna(row[z_col]) else 0.0
        rz = float(row[rz_col]) if rz_col and pd.notna(row[rz_col]) else 0.0
        area = str(row[area_col]) if area_col and pd.notna(row[area_col]) else area_for(x, y)
        ftype = "TRACK_ROBOT" if "_TRACK_ROBOT_" in name.upper() else "ROBOT"
        result.append(
            FrameSpec(
                name=name,
                type=ftype,
                area=area,
                x=x,
                y=y,
                z=z,
                rz=norm_deg(rz),
                source_layer=str(row[layer_col]) if layer_col and pd.notna(row[layer_col]) else "",
                source_block=str(row[block_col]) if block_col and pd.notna(row[block_col]) else "",
                source_entity="CORRECTED_ROBOT_TABLE",
                confidence="VERIFIED/CORRECTED",
            )
        )
    return result


def detect_robot_frames_from_dxf(doc: ezdxf.document.Drawing) -> list[FrameSpec]:
    grouped: dict[tuple[str, str], list[tuple[float, float, float, str, str]]] = {}
    for entity in doc.modelspace().query("INSERT"):
        layer = str(entity.dxf.layer or "")
        block = str(entity.dxf.name or "")
        if not is_robot_insert(layer, block):
            continue
        p = entity.dxf.insert
        x, y = float(p.x), float(p.y)
        area = area_for(x, y)
        kind = "TRACK_ROBOT" if is_track(layer, block) else "ROBOT"
        grouped.setdefault((area, kind), []).append(
            (x, y, norm_deg(float(entity.dxf.rotation or 0.0)), layer, block)
        )

    result: list[FrameSpec] = []
    for (area, kind), rows in sorted(grouped.items()):
        rows.sort(key=lambda r: (r[1], r[0], r[4]))
        for i, (x, y, rz, layer, block) in enumerate(rows, 1):
            token = "TRACK_ROBOT" if kind == "TRACK_ROBOT" else "ROBOT"
            result.append(
                FrameSpec(
                    name=f"{area}_{token}_{i:03d}",
                    type=kind,
                    area=area,
                    x=x,
                    y=y,
                    rz=rz,
                    source_layer=layer,
                    source_block=block,
                    source_entity="INSERT",
                    confidence="DXF_RULE",
                )
            )
    return result


def segment_frame(area: str, idx: int, x1: float, y1: float, x2: float, y2: float, layer: str, entity_type: str) -> FrameSpec | None:
    dx, dy = x2 - x1, y2 - y1
    length = math.hypot(dx, dy)
    if length < 1.0:
        return None
    # JT fence origin is assumed to be at the segment midpoint, +X along the segment.
    # If the supplied JT uses a different local origin, adjust FenceOffset in C#.
    x = (x1 + x2) / 2.0
    y = (y1 + y2) / 2.0
    rz = norm_deg(math.degrees(math.atan2(dy, dx)))
    return FrameSpec(
        name=f"{area}_FENCE_{idx:03d}",
        type="FENCE",
        area=area,
        x=x,
        y=y,
        rz=rz,
        source_layer=layer,
        source_entity=entity_type,
        confidence="DXF_FENCE_GEOMETRY",
    )


def detect_fence_frames(doc: ezdxf.document.Drawing) -> list[FrameSpec]:
    raw: list[FrameSpec] = []

    # Preferred: explicit fence block INSERTs.
    for entity in doc.modelspace().query("INSERT"):
        layer = str(entity.dxf.layer or "")
        block = str(entity.dxf.name or "")
        if not is_fence(layer, block):
            continue
        p = entity.dxf.insert
        x, y = float(p.x), float(p.y)
        raw.append(
            FrameSpec(
                name="",
                type="FENCE",
                area=area_for(x, y),
                x=x,
                y=y,
                rz=norm_deg(float(entity.dxf.rotation or 0.0)),
                source_layer=layer,
                source_block=block,
                source_entity="INSERT",
                confidence="DXF_FENCE_BLOCK",
            )
        )

    # Fallback: fence-layer LINE/LWPOLYLINE segments.
    for entity in doc.modelspace():
        etype = entity.dxftype()
        layer = str(entity.dxf.layer or "")
        if etype not in {"LINE", "LWPOLYLINE"} or not is_fence(layer):
            continue

        if etype == "LINE":
            s, e = entity.dxf.start, entity.dxf.end
            segments = [(float(s.x), float(s.y), float(e.x), float(e.y))]
        else:
            pts = [(float(x), float(y)) for x, y, *_ in entity.get_points("xy")]
            segments = list(zip(pts, pts[1:]))
            segments = [(a[0], a[1], b[0], b[1]) for a, b in segments]
            if entity.closed and len(pts) > 2:
                a, b = pts[-1], pts[0]
                segments.append((a[0], a[1], b[0], b[1]))

        for x1, y1, x2, y2 in segments:
            x = (x1 + x2) / 2.0
            y = (y1 + y2) / 2.0
            area = area_for(x, y)
            candidate = segment_frame(area, 0, x1, y1, x2, y2, layer, etype)
            if candidate:
                raw.append(candidate)

    # Stable per-area naming and light duplicate suppression.
    seen: set[tuple[str, int, int, int]] = set()
    grouped: dict[str, list[FrameSpec]] = {}
    for f in raw:
        key = (f.area, round(f.x), round(f.y), round(f.rz))
        if key in seen:
            continue
        seen.add(key)
        grouped.setdefault(f.area, []).append(f)

    result: list[FrameSpec] = []
    for area, rows in sorted(grouped.items()):
        rows.sort(key=lambda f: (f.y, f.x, f.rz, f.source_block))
        for i, f in enumerate(rows, 1):
            result.append(
                FrameSpec(
                    name=f"{area}_FENCE_{i:03d}",
                    type="FENCE",
                    area=area,
                    x=f.x,
                    y=f.y,
                    z=f.z,
                    rx=f.rx,
                    ry=f.ry,
                    rz=f.rz,
                    source_layer=f.source_layer,
                    source_block=f.source_block,
                    source_entity=f.source_entity,
                    confidence=f.confidence,
                )
            )
    return result


def write_table(frames: list[FrameSpec], path: Path) -> None:
    rows = [f.__dict__ for f in frames]
    df = pd.DataFrame(rows)
    if df.empty:
        df = pd.DataFrame(columns=[
            "name", "type", "area", "x", "y", "z", "rx", "ry", "rz",
            "source_layer", "source_block", "source_entity", "confidence",
        ])
    path.parent.mkdir(parents=True, exist_ok=True)
    # Semicolon delimiter keeps the Process Simulate C# reader intentionally simple.
    df.to_csv(path, index=False, sep=";", encoding="utf-8-sig")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("dxf", type=Path, help="Factory DXF file")
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--robot-table", type=Path, default=None,
                        help="Verified/corrected robot CSV. Default: <out-dir>/FACTORY_RobotFrames_Corrected.csv")
    args = parser.parse_args()

    dxf_path = args.dxf.resolve()
    if not dxf_path.exists():
        raise FileNotFoundError(dxf_path)

    out_dir = args.out_dir.resolve()
    robot_table = (args.robot_table or (out_dir / "FACTORY_RobotFrames_Corrected.csv")).resolve()

    print(f"Reading DXF: {dxf_path}")
    doc = ezdxf.readfile(dxf_path)

    if robot_table.exists():
        robots = load_corrected_robot_frames(robot_table)
        print(f"Robot source: corrected table ({len(robots)})")
    else:
        robots = detect_robot_frames_from_dxf(doc)
        print(f"Robot source: DXF rule detection ({len(robots)})")

    fences = detect_fence_frames(doc)
    print(f"Fence placements: {len(fences)}")

    frames = robots + fences
    names = [f.name.upper() for f in frames]
    if len(names) != len(set(names)):
        dup = sorted({n for n in names if names.count(n) > 1})
        raise RuntimeError(f"Duplicate frame names: {dup}")

    output = out_dir / "FactoryPlacementFrames.csv"
    write_table(frames, output)
    print(f"Output: {output}")
    print(f"Total frames: {len(frames)} | robot={len(robots)} | fence={len(fences)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
