# Project Specification

## User workflow

### Before
Open CAD -> locate equipment -> find center -> read/copy coordinates -> determine direction ->
manually name/create frames -> manually reproduce fence/environment -> finally start resource placement.

### Target workflow
DXF -> Analyze -> Frames + Frame Check + Environment STEP -> verified station/label corrections ->
Process Simulate -> optionally batch-place explicitly approved robot resources on verified robot Frames ->
manual/native simulation engineering.

The batch-placement stage is deliberately narrow: it may reuse a known user-supplied COJT package on already verified robot Frames, but it must not guess which robot model belongs to which CAD symbol.

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

For audited production areas, the project may also emit a corrected frame table that preserves original names/coordinates for traceability while adding deterministic station and nearby CAD-label corrections.

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

### 5. Optional Process Simulate batch placement script
For explicitly approved robot resources only:
- read verified robot Frames
- insert the configured complete COJT package
- move the COJT root as one unit
- align the actual Robot Baseframe to the target Frame
- validate residual position/angle error
- skip/report suspicious coordinates or duplicate generated instances

### 6. Deterministic station correction output
When CAD text has been manually audited and station reference rules are approved:
- apply the approved station map to the robot-detection audit
- match unique nearby bare robot labels only within a configured distance
- never reuse one text label for multiple robot Frames
- retain the original frame identifier when the label match is ambiguous
- regenerate both the corrected CSV and Process Simulate frame-creation C# from the same corrected data

Current implementation: `scripts/apply_mid_main_station_corrections.py` for MID_MAIN BM/FM.

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
12. Apply any user-verified station/robot-label correction rules and regenerate corrected frame data.
13. Build static environment geometry and export STEP.
14. Generate Process Simulate frame-creation script/data from the corrected frame table where available.
15. After Frames are verified, optionally batch-place a user-approved robot COJT package on robot Frames.
16. Keep model selection, kinematics, TCP/tool, collision, operations and path planning as explicit Process Simulate engineering work unless separately approved.

## Validation
Start with one small production area (for example one OP with 1-3 robots, fixture/TTR and a small fence section).
Compare generated frames against a manually verified Process Simulate study before scaling to a full line/factory.

For station corrections, keep both the original frame name/value and correction metadata in the CSV so every rename/repair is auditable.

For batch robot placement, first run in dry-run mode, then test on one line/small cell before populating the full factory.
