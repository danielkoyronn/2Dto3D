from __future__ import annotations

"""Apply the verified MID_MAIN BM/FM station rules to the robot audit output.

Input files (default: output/1013):
- DXF_Text_Audit.csv
- FACTORY_RobotDetection_Audit.csv

Outputs:
- FACTORY_RobotFrames_Corrected.csv
- process_simulate/FactoryRobotFrames_Corrected.cs

The script keeps all non-MID_MAIN frame names unchanged. MID_MAIN normal robot
frames are assigned to BM/FM stations using the verified X reference map and the
Y split at -1,975,000 mm. When a unique nearby bare CAD robot label (R01, R02,
...) can be matched deterministically, it is included in the corrected frame
name while preserving the `_ROBOT_` token required by the batch placement
script.
"""

import argparse
import math
import re
import sys
from pathlib import Path
from typing import Iterable

import numpy as np
import pandas as pd


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUT = REPO_ROOT / "output" / "1013"

MID_MAIN_WINDOW = (935000.0, 1040000.0, -1992000.0, -1953000.0)
MID_MAIN_Y_SPLIT = -1975000.0
ROBOT_LABEL_MAX_DISTANCE_MM = 3000.0
MAX_ABS_Z_MM = 10000.0
DEFAULT_2D_ROBOT_Z_MM = 0.0

UPPER_STATION_X = {
    "BM02": 957496.0,
    "BM01": 972315.0,
    "FM07": 982662.0,
    "FM05": 995953.0,
    "FM04": 1001809.0,
    "FM03": 1008036.0,
    "FM02": 1015611.0,
    "FM01": 1025683.0,
}

LOWER_STATION_X = {
    "BM05": 950331.0,
    "BM06": 958850.0,
    "BM07": 965237.0,
    "BM08": 971295.0,
    "BM09": 977535.0,
    "BM10": 984581.0,
    "BM12": 996952.0,
}


# ---------------------------------------------------------------------------
# Column helpers
# ---------------------------------------------------------------------------

def first_existing(columns: Iterable[str], choices: Iterable[str]) -> str | None:
    existing = {str(c).lower(): str(c) for c in columns}
    for choice in choices:
        hit = existing.get(choice.lower())
        if hit:
            return hit
    return None


def require_column(df: pd.DataFrame, choices: Iterable[str], label: str) -> str:
    col = first_existing(df.columns, choices)
    if col is None:
        raise KeyError(f"Missing {label} column; tried: {list(choices)}")
    return col


# ---------------------------------------------------------------------------
# Verified station rules from CAD text analysis
# ---------------------------------------------------------------------------

def station_for(x: float, y: float) -> str:
    group = UPPER_STATION_X if y > MID_MAIN_Y_SPLIT else LOWER_STATION_X
    return min(group, key=lambda name: abs(x - group[name]))


def robot_label_number(text: str) -> int | None:
    m = re.fullmatch(r"(?i)R\s*0*(\d{1,2})", str(text).strip())
    if not m:
        return None
    return int(m.group(1))


def is_track_frame(name: str) -> bool:
    return "_TRACK_ROBOT_" in str(name).upper()


def old_numeric_suffix(name: str) -> str:
    m = re.search(r"(\d+)$", str(name))
    if not m:
        return "000"
    return f"{int(m.group(1)):03d}"


# ---------------------------------------------------------------------------
# One-to-one CAD label matching
# ---------------------------------------------------------------------------

def assign_bare_robot_labels(
    robot_rows: pd.DataFrame,
    label_rows: pd.DataFrame,
    x_col: str,
    y_col: str,
) -> dict[int, int]:
    """Greedy deterministic one-to-one nearest matching.

    We intentionally do not reuse a CAD text label for multiple frames. Pairs
    farther than ROBOT_LABEL_MAX_DISTANCE_MM are ignored and the original frame
    sequence is preserved instead of guessing.
    """

    pairs: list[tuple[float, int, int]] = []
    for ridx, r in robot_rows.iterrows():
        rx, ry = float(r[x_col]), float(r[y_col])
        for lidx, l in label_rows.iterrows():
            lx, ly = float(l[x_col]), float(l[y_col])
            d = math.hypot(rx - lx, ry - ly)
            if d <= ROBOT_LABEL_MAX_DISTANCE_MM:
                pairs.append((d, int(ridx), int(lidx)))

    pairs.sort(key=lambda item: (item[0], item[1], item[2]))
    used_robots: set[int] = set()
    used_labels: set[int] = set()
    result: dict[int, int] = {}

    for _, ridx, lidx in pairs:
        if ridx in used_robots or lidx in used_labels:
            continue
        label_no = int(label_rows.loc[lidx, "_robot_no"])
        used_robots.add(ridx)
        used_labels.add(lidx)
        result[ridx] = label_no

    return result


# ---------------------------------------------------------------------------
# Main correction stage
# ---------------------------------------------------------------------------

def apply_corrections(texts: pd.DataFrame, robots: pd.DataFrame) -> pd.DataFrame:
    result = robots.copy()

    frame_col = require_column(result, ["FrameName", "Name"], "frame name")
    area_col = require_column(result, ["Area", "Line"], "area")
    x_col = require_column(result, ["x", "X", "X_mm"], "X")
    y_col = require_column(result, ["y", "Y", "Y_mm"], "Y")

    tx = require_column(texts, ["x", "X", "X_mm"], "text X")
    ty = require_column(texts, ["y", "Y", "Y_mm"], "text Y")
    text_col = require_column(texts, ["text", "Text", "value", "Value"], "text")

    result["OriginalFrameName"] = result[frame_col].astype(str)
    result["Station"] = ""
    result["RobotLabel"] = ""
    result["CorrectedFrameName"] = result[frame_col].astype(str)
    result["CorrectionStatus"] = "unchanged"
    result["CorrectionRemark"] = ""

    mid_mask = result[area_col].astype(str).str.upper().eq("MID_MAIN")
    mid = result[mid_mask].copy()
    if mid.empty:
        return result

    # Apply the verified BM/FM station map.
    mid["Station"] = [
        station_for(float(x), float(y))
        for x, y in zip(mid[x_col], mid[y_col])
    ]

    # Extract bare robot number labels from the same expanded MID_MAIN window.
    xmin, xmax, ymin, ymax = MID_MAIN_WINDOW
    t = texts[
        texts[tx].between(xmin, xmax)
        & texts[ty].between(ymin, ymax)
    ].copy()
    t["_robot_no"] = t[text_col].map(robot_label_number)
    bare = t[t["_robot_no"].notna()].copy()
    if not bare.empty:
        bare["_robot_no"] = bare["_robot_no"].astype(int)
        bare["Station"] = [
            station_for(float(x), float(y))
            for x, y in zip(bare[tx], bare[ty])
        ]

    # Work station-by-station and only rename normal robots. Track robot naming is
    # kept intact because the track/7th-axis workflow is separate.
    assigned_labels: dict[int, int] = {}
    for station, station_group in mid.groupby("Station", sort=False):
        normal = station_group[
            ~station_group[frame_col].astype(str).map(is_track_frame)
        ]
        labels = bare[bare["Station"].eq(station)] if not bare.empty else bare
        if normal.empty or labels.empty:
            continue

        labels_for_match = labels.rename(columns={tx: x_col, ty: y_col})
        assigned_labels.update(
            assign_bare_robot_labels(normal, labels_for_match, x_col, y_col)
        )

    for idx, row in mid.iterrows():
        original = str(row[frame_col])
        station = str(row["Station"])
        result.at[idx, "Station"] = station

        if is_track_frame(original):
            result.at[idx, "CorrectionStatus"] = "station-tagged-track-name-kept"
            result.at[idx, "CorrectionRemark"] = (
                "MID_MAIN station classified; track frame name preserved."
            )
            continue

        label_no = assigned_labels.get(int(idx))
        if label_no is not None:
            robot_token = f"R{label_no:02d}"
            corrected = f"MID_MAIN_{station}_ROBOT_{robot_token}"
            result.at[idx, "RobotLabel"] = robot_token
            status = "station+robot-label"
            remark = "Station from verified BM/FM X map; robot number from nearest unique bare R label."
        else:
            corrected = f"MID_MAIN_{station}_ROBOT_{old_numeric_suffix(original)}"
            status = "station-only"
            remark = "Station applied; no unique nearby bare R label, so original numeric suffix was retained."

        result.at[idx, "CorrectedFrameName"] = corrected
        result.at[idx, "CorrectionStatus"] = status
        result.at[idx, "CorrectionRemark"] = remark

    # Repair obviously corrupt 2D-derived robot Z only. Keep the original value
    # for audit. Normal small non-zero values are retained.
    z_col = first_existing(result.columns, ["z", "Z", "Z_mm", "z_mm"])
    if z_col:
        result["OriginalZ"] = pd.to_numeric(result[z_col], errors="coerce")
        z = pd.to_numeric(result[z_col], errors="coerce")
        bad = z.abs() > MAX_ABS_Z_MM
        result.loc[bad, z_col] = DEFAULT_2D_ROBOT_Z_MM
        result.loc[bad, "CorrectionStatus"] = (
            result.loc[bad, "CorrectionStatus"].astype(str) + "+z-repaired"
        ).str.strip("+")
        result.loc[bad, "CorrectionRemark"] = (
            result.loc[bad, "CorrectionRemark"].astype(str)
            + f" Suspicious |Z|>{MAX_ABS_Z_MM:g} mm replaced with explicit 2D robot-base default Z={DEFAULT_2D_ROBOT_Z_MM:g} mm; OriginalZ retained."
        ).str.strip()

    # Corrected frame names must be unique. If CAD labels collide, keep the
    # original suffix for the later duplicate rather than silently duplicating.
    duplicates = result["CorrectedFrameName"].duplicated(keep=False)
    if duplicates.any():
        for idx in result[duplicates].index:
            original = str(result.at[idx, "OriginalFrameName"])
            station = str(result.at[idx, "Station"])
            if station and not is_track_frame(original):
                fallback = f"MID_MAIN_{station}_ROBOT_{old_numeric_suffix(original)}"
                result.at[idx, "CorrectedFrameName"] = fallback
                result.at[idx, "CorrectionStatus"] = "station-only-duplicate-fallback"
                result.at[idx, "CorrectionRemark"] = (
                    str(result.at[idx, "CorrectionRemark"])
                    + " Duplicate corrected name detected; original suffix used as deterministic fallback."
                ).strip()

    return result


# ---------------------------------------------------------------------------
# Process Simulate C# generator
# ---------------------------------------------------------------------------

def numeric_series(df: pd.DataFrame, choices: list[str], default: float) -> pd.Series:
    col = first_existing(df.columns, choices)
    if col is None:
        return pd.Series([default] * len(df), index=df.index, dtype=float)
    return pd.to_numeric(df[col], errors="coerce").fillna(default).astype(float)


def build_csharp(corrected: pd.DataFrame) -> str:
    x = numeric_series(corrected, ["x", "X", "X_mm"], 0.0)
    y = numeric_series(corrected, ["y", "Y", "Y_mm"], 0.0)
    z = numeric_series(corrected, ["z", "Z", "Z_mm", "z_mm"], DEFAULT_2D_ROBOT_Z_MM)
    rz = numeric_series(corrected, ["Rz_deg", "rz", "Rz", "rotation", "Rotation"], 0.0)

    specs: list[str] = []
    for idx, row in corrected.iterrows():
        name = str(row.get("CorrectedFrameName", row.get("FrameName", ""))).replace('"', '\\"')
        specs.append(
            f'        new FrameSpec("{name}", {x.at[idx]:.6f}, {y.at[idx]:.6f}, {z.at[idx]:.6f}, {rz.at[idx]:.6f}),'
        )

    spec_text = "\n".join(specs)

    return f'''using System;\nusing System.IO;\nusing System.Reflection;\nusing System.Windows.Forms;\nusing Tecnomatix.Engineering;\n\n// AUTO-GENERATED by scripts/apply_mid_main_station_corrections.py\n// Corrected robot Frames only. Run this before BatchPlaceRobotOnFrames.cs.\n\npublic class MainScript\n{{\n    private sealed class FrameSpec\n    {{\n        public string Name;\n        public double X, Y, Z, Rz;\n        public FrameSpec(string name, double x, double y, double z, double rz)\n        {{ Name = name; X = x; Y = y; Z = z; Rz = rz; }}\n    }}\n\n    private static readonly FrameSpec[] Specs = new FrameSpec[]\n    {{\n{spec_text}\n    }};\n\n    public static void Main()\n    {{\n        StringWriter output = new StringWriter();\n        TxDocument doc = TxApplication.ActiveDocument;\n        if (doc == null || doc.PhysicalRoot == null)\n        {{\n            output.WriteLine("ERROR: No active study / PhysicalRoot.");\n            TxMessageBox.Show(output.ToString(), "Corrected Robot Frames", MessageBoxButtons.OK, MessageBoxIcon.Error);\n            return;\n        }}\n\n        object root = doc.PhysicalRoot;\n        foreach (FrameSpec s in Specs)\n            CreateOne(root, s.Name, s.X, s.Y, s.Z, 0.0, 0.0, s.Rz, ref output);\n\n        output.WriteLine("");\n        output.WriteLine("Corrected robot Frame generation finished: " + Specs.Length);\n        TxMessageBox.Show(output.ToString(), "Corrected Robot Frames", MessageBoxButtons.OK, MessageBoxIcon.Information);\n    }}\n\n    private static void CreateOne(object physicalRoot, string name, double x, double y, double z, double rxDeg, double ryDeg, double rzDeg, ref StringWriter output)\n    {{\n        try\n        {{\n            if (FrameExists(physicalRoot, name))\n            {{ output.WriteLine("SKIP " + name + ": frame already exists."); return; }}\n\n            Type frameDataType = FindType("TxFrameCreationData");\n            if (frameDataType == null)\n            {{ output.WriteLine("FAIL " + name + ": TxFrameCreationData type not found."); return; }}\n\n            double d2r = Math.PI / 180.0;\n            TxTransformation tr = new TxTransformation(\n                new TxVector(x, y, z),\n                new TxVector(rxDeg*d2r, ryDeg*d2r, rzDeg*d2r),\n                TxTransformation.TxRotationType.RPY_XYZ);\n\n            object data = MakeCreationData(frameDataType, name, tr);\n            if (data == null)\n            {{ output.WriteLine("FAIL " + name + ": cannot construct TxFrameCreationData."); return; }}\n\n            MethodInfo create = FindCreateFrameMethod(physicalRoot.GetType(), frameDataType);\n            if (create == null)\n            {{ output.WriteLine("FAIL " + name + ": no compatible CreateFrame method."); return; }}\n\n            object frame = create.Invoke(physicalRoot, new object[] {{ data }});\n            if (frame == null)\n            {{ output.WriteLine("FAIL " + name + ": CreateFrame returned null."); return; }}\n\n            SetProperty(frame, "AbsoluteLocation", tr);\n            SetProperty(frame, "Location", tr);\n            output.WriteLine("OK   " + name);\n        }}\n        catch (Exception ex)\n        {{\n            Exception e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;\n            output.WriteLine("FAIL " + name + ": " + e.GetType().Name + " - " + e.Message);\n        }}\n    }}\n\n    private static bool FrameExists(object physicalRoot, string name)\n    {{\n        TxPhysicalRoot root = physicalRoot as TxPhysicalRoot;\n        if (root == null) return false;\n        TxObjectList frames = root.GetAllDescendants(new TxTypeFilter(typeof(TxFrame)));\n        foreach (ITxObject item in frames)\n        {{\n            TxFrame frame = item as TxFrame;\n            if (frame != null && String.Equals(frame.Name, name, StringComparison.OrdinalIgnoreCase)) return true;\n        }}\n        return false;\n    }}\n\n    private static Type FindType(string shortName)\n    {{\n        foreach (Type t in typeof(TxApplication).Assembly.GetTypes())\n            if (t.Name == shortName) return t;\n        return null;\n    }}\n\n    private static object MakeCreationData(Type t, string name, TxTransformation tr)\n    {{\n        foreach (ConstructorInfo ctor in t.GetConstructors())\n        {{\n            ParameterInfo[] ps = ctor.GetParameters();\n            object[] args = new object[ps.Length];\n            try\n            {{\n                for (int j = 0; j < ps.Length; j++)\n                {{\n                    Type pt = ps[j].ParameterType;\n                    if (pt == typeof(string)) args[j] = name;\n                    else if (pt == typeof(TxTransformation)) args[j] = tr;\n                    else if (ps[j].HasDefaultValue) args[j] = ps[j].DefaultValue;\n                    else if (pt.IsValueType) args[j] = Activator.CreateInstance(pt);\n                    else args[j] = null;\n                }}\n                object data = ctor.Invoke(args);\n                SetProperty(data, "Name", name);\n                SetProperty(data, "AbsoluteLocation", tr);\n                SetProperty(data, "Location", tr);\n                SetProperty(data, "Transformation", tr);\n                return data;\n            }}\n            catch {{ }}\n        }}\n        return null;\n    }}\n\n    private static MethodInfo FindCreateFrameMethod(Type rootType, Type dataType)\n    {{\n        foreach (MethodInfo m in rootType.GetMethods(BindingFlags.Instance | BindingFlags.Public))\n        {{\n            if (m.Name != "CreateFrame") continue;\n            ParameterInfo[] ps = m.GetParameters();\n            if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(dataType)) return m;\n        }}\n        return null;\n    }}\n\n    private static void SetProperty(object obj, string name, object value)\n    {{\n        if (obj == null || value == null) return;\n        PropertyInfo p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);\n        if (p == null || !p.CanWrite || !p.PropertyType.IsAssignableFrom(value.GetType())) return;\n        try {{ p.SetValue(obj, value, null); }} catch {{ }}\n    }}\n}}\n'''


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--out-dir",
        type=Path,
        default=DEFAULT_OUT,
        help="Directory containing the two audit CSV files (default: output/1013).",
    )
    parser.add_argument(
        "--csv-output",
        type=Path,
        default=None,
        help="Corrected CSV path; defaults inside --out-dir.",
    )
    parser.add_argument(
        "--cs-output",
        type=Path,
        default=None,
        help="Generated Process Simulate C# path; defaults to process_simulate/FactoryRobotFrames_Corrected.cs.",
    )
    args = parser.parse_args()

    out_dir = args.out_dir.resolve()
    text_path = out_dir / "DXF_Text_Audit.csv"
    robot_path = out_dir / "FACTORY_RobotDetection_Audit.csv"
    csv_output = args.csv_output or (out_dir / "FACTORY_RobotFrames_Corrected.csv")
    cs_output = args.cs_output or (REPO_ROOT / "process_simulate" / "FactoryRobotFrames_Corrected.cs")

    for path in (text_path, robot_path):
        if not path.exists():
            print(f"ERROR: missing input: {path}", file=sys.stderr)
            return 2

    texts = pd.read_csv(text_path)
    robots = pd.read_csv(robot_path)
    corrected = apply_corrections(texts, robots)

    csv_output.parent.mkdir(parents=True, exist_ok=True)
    cs_output.parent.mkdir(parents=True, exist_ok=True)
    corrected.to_csv(csv_output, index=False, encoding="utf-8-sig")
    cs_output.write_text(build_csharp(corrected), encoding="utf-8-sig")

    changed = corrected[corrected["CorrectionStatus"].ne("unchanged")]
    print(f"Input robot rows : {len(corrected)}")
    print(f"Corrected rows   : {len(changed)}")
    print(f"Corrected CSV    : {csv_output}")
    print(f"Generated C#     : {cs_output}")
    print("\nMID_MAIN station counts:")
    print(corrected[corrected["Station"].ne("")]["Station"].value_counts().sort_index().to_string())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
