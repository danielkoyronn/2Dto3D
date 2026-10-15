# Project Specification

## User workflow

### Before
Open CAD -> locate equipment -> find center -> read/copy coordinates -> determine direction ->
manually name/create frames -> manually reproduce fence/environment -> finally start resource placement.

### Target workflow
DXF -> Analyze -> Frames + Frame Check + Environment STEP -> Process Simulate ->
manual/native resource placement and simulation engineering.

## Primary outputs

### 1. Frame table
Recommended columns:
- Name
- Type
- Line
- Station
- X
- Y
- Z
- Rx
- Ry
- Rz
- SourceLayer
- SourceBlock
- Confidence
- Remark

### 2. Frame check
A CAD-derived visual output containing:
- frame origin marker
- frame name
- X/Y direction
- source equipment outline
- warnings for ambiguous detections

### 3. Factory environment STEP
Static geometry only:
- safety fence
- floor
- wall
- column
- platform
- optional static placeholders

### 4. Validation report
Compare reference Process Simulate frame data with CAD-derived frames and report positional/rotational deviation.

## Recommended processing pipeline
1. Load DXF.
2. Read units and preserve global coordinates.
3. Recursively resolve INSERT/BLOCK transforms.
4. Collect geometry, layer, block, text and rotation metadata.
5. Detect equipment candidates.
6. Determine base/reference center.
7. Determine orientation.
8. Associate nearby station/equipment labels where reliable.
9. Apply naming rules.
10. Export frame table.
11. Render/check frames against CAD.
12. Build static environment geometry and export STEP.
13. Optionally generate Process Simulate frame-creation script/data.

## Validation
Start with one small production area (for example one OP with 1-3 robots, fixture/TTR and a small fence section).
Compare generated frames against a manually verified Process Simulate study before scaling to a full line/factory.
