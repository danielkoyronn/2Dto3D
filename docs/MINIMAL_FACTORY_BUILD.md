# Minimal DXF -> Frames -> Robot/Fence workflow

This is the reduced workflow requested for the current Process Simulate project.

## Required functions only

1. Read `.dxf`.
2. Create placement Frames.
3. Import the approved Robot at world origin `(0,0,0)` and create one identical resource instance at every Robot Frame.
4. Import the supplied Safe Fence JT at world origin and create one identical JT instance at every Fence Frame.

## Files

DXF preprocessing:

```text
scripts/build_factory_layout.py
```

Process Simulate 2512 script:

```text
process_simulate/BuildFactoryFromFrames.cs
```

## Naming standard

```text
<AREA>_ROBOT_<NNN>
<AREA>_TRACK_ROBOT_<NNN>
<AREA>_FENCE_<NNN>
```

Examples:

```text
BSR_ROBOT_005
EC_ROBOT_014
MID_MAIN_TRACK_ROBOT_003
BSL_FENCE_001
BSL_FENCE_002
```

Verified MID_MAIN corrected names are preserved, for example:

```text
MID_MAIN_BM02_ROBOT_R01
MID_MAIN_FM05_ROBOT_R03
```

## Stage 1 - Read DXF and build Frame table

Run:

```powershell
python scripts/build_factory_layout.py "D:\path\1013.dxf"
```

Default output:

```text
output/1013/FactoryPlacementFrames.csv
```

The file is semicolon-separated even though the extension is `.csv`.

Robot source priority:

1. If `output/1013/FACTORY_RobotFrames_Corrected.csv` exists, use those verified/corrected Robot Frames.
2. Otherwise detect Robot INSERTs directly from DXF layers/block names.

Fence detection:

1. Prefer explicit fence block INSERTs.
2. Also support LINE/LWPOLYLINE entities on fence/safety-fence layers.
3. A line/polyline segment creates a Fence Frame at its midpoint with +X along the segment.

## Stage 2 - Process Simulate

Copy these generated/resource files to the local Process library used by the C# constants:

```text
FactoryPlacementFrames.csv
BSR_OP10_R01_KR210_R2700.cojt
BSL_SF-0044.jt
```

Default paths in `BuildFactoryFromFrames.cs`:

```text
D:\WEDING\MOVING\work261007\Library\FactoryPlacementFrames.csv
D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt
D:\WEDING\MOVING\work261007\Library\BSL_SF-0044.jt
```

Then run:

```text
process_simulate/BuildFactoryFromFrames.cs
```

The script does the following in order:

1. Creates missing `TxFrame` objects.
2. Inserts `TEMPLATE_ROBOT_KR210_R2700` at world origin.
3. Inserts `TEMPLATE_FENCE_BSL_SF_0044` at world origin.
4. For each Robot Frame, inserts another instance of the same Robot COJT and aligns its real `Robot.Baseframe` exactly to the target Frame.
5. For each Fence Frame, inserts another instance of `BSL_SF-0044.jt` and places its JT root on the Fence Frame.
6. Existing named Frames/resources are skipped, making repeated runs safer.

## Why repeated InsertComponent is used instead of API Copy/Paste

The result is intentionally equivalent to copying the origin template, but the implementation reinserts the same approved source package/JT for every placement. This is more deterministic across Process Simulate resource types and avoids depending on version-specific clipboard/copy APIs.

The template remains at `(0,0,0)` for inspection by default.

## Fence origin calibration

A static JT does not expose a Robot-like Baseframe. Therefore the current rule is:

```text
Fence JT local origin -> Fence Frame
```

If `BSL_SF-0044.jt` appears consistently offset after the first test, change only these constants in `BuildFactoryFromFrames.cs`:

```csharp
FenceOffsetX
FenceOffsetY
FenceOffsetZ
FenceOffsetRzDeg
```

Do not manually change every Fence Frame.

## Supplied Fence file

The supplied resource is:

```text
BSL_SF-0044.jt
```

It is treated as a static JT fence resource. The binary JT is not committed automatically to the repository; keep the approved file in the local Process Simulate library and point `FenceJtPath` to it.
