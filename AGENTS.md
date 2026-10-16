# AGENTS.md

## Project
CAD to Process Layout Assistant

## Final goal
The primary deliverable is now **one Process Simulate 2512 .NET Script** that performs the whole reduced workflow in a single run. Do not require a separate Python correction stage, CSV handoff, or separate frame/fence placement scripts for the final user workflow.

The canonical script is:

`process_simulate/AllInOneDxfRobotFence.cs`

## Required single-script workflow
1. Read the factory **ASCII DXF directly** from the configured local path.
2. Detect Robot / Track-Robot / Safe-Fence placement data from DXF entities.
3. Apply the already verified 1013 naming/correction rules, including MID_MAIN BM/FM station mapping and nearby bare `Rxx` labels where available.
4. Create all required Process Simulate Native Frames.
5. Insert one approved Robot COJT template at world origin `(0,0,0)`.
6. Instantiate the same approved Robot package at every Robot Frame and align its real `Robot.Baseframe` to the target Frame.
7. Insert one supplied Safe-Fence JT template at world origin `(0,0,0)`.
8. Instantiate the same Safe-Fence JT at every Fence Frame using the Fence Frame transform plus explicit calibration offsets if needed.
9. Report create/skip/fail counts and reject suspicious coordinates instead of silently placing resources incorrectly.

## Naming standard
- Normal robot frame: `<AREA>_ROBOT_<NNN>`
- Track robot frame: `<AREA>_TRACK_ROBOT_<NNN>`
- Corrected MID_MAIN robot: `MID_MAIN_<STATION>_ROBOT_R<NN>` when a unique nearby CAD `Rxx` label is available
- MID_MAIN fallback: `MID_MAIN_<STATION>_ROBOT_<NNN>`
- Fence frame: `<AREA>_FENCE_<NNN>`
- Robot instance: `AUTO_<FRAME_NAME>_<ROBOT_MODEL_TAG>`
- Fence instance: `AUTO_<FRAME_NAME>_<FENCE_MODEL_TAG>`

## Current approved resources
- Robot package: `BSR_OP10_R01_KR210_R2700.cojt`
- Safe Fence model: `BSL_SF-0044.jt`

Paths are local-machine configuration values at the top of the .NET script and must not be guessed if the user changes resources.

## Robot placement rule
Move the complete imported COJT root as one unit. Preserve the package's internal links/joints. Align the imported Robot's actual `Robot.Baseframe` to the target TxFrame and validate residual position/angle error.

## Fence placement rule
The JT has no Robot Baseframe. Treat its JT root as the placement origin and apply the target Fence Frame transform. Keep explicit `FenceOffsetX/Y/Z/RzDeg` constants for one-time origin calibration.

## DXF recognition rules
- Prefer structural DXF data: INSERT block, layer, insertion X/Y/Z, rotation.
- Current 1013 Robot evidence includes layer `010_ROBOT`, Robot-like block names such as `R-2000...`, `KR...`, `ROBOT`, and verified block `DTL0033`.
- Track Robot detection uses track/rail/7th-axis keywords.
- Safe Fence detection uses explicit fence/safety/SF block/layer names. If an area has explicit Fence INSERTs, prefer them. Otherwise use Fence-layer LINE/LWPOLYLINE segments as fallback.
- Preserve factory CAD coordinates; do not translate the whole factory to `(0,0)`.

## Verified MID_MAIN rules
Upper group (`Y > -1975000`): BM02, BM01, FM07, FM05, FM04, FM03, FM02, FM01 using the verified station-X references already encoded in the script.

Lower group (`Y <= -1975000`): BM05, BM06, BM07, BM08, BM09, BM10, BM12 using the verified station-X references already encoded in the script.

Bare CAD labels `R01`, `R02`, etc. are matched one-to-one within the assigned station by nearest XY distance up to 3000 mm. Never reuse one CAD label or one robot number in the same station. If matching is ambiguous/unavailable, retain deterministic numeric fallback naming rather than guessing.

## Safety / robustness rules
- Reject binary DXF; require ASCII DXF for the built-in parser.
- Ignore entities explicitly marked PaperSpace (`67=1`).
- Repair obviously corrupt 2D Robot Z values only when `|Z| > 10000 mm`, using configured default Z=0.
- Skip existing named Frames/resources on repeated runs where practical.
- Keep the Robot template and Fence template at `(0,0,0)` by default for inspection.
- Track Robot Frames currently receive the Robot arm only; do not invent a rail/7th-axis resource.
- Fence LWPOLYLINE bulges may be treated as straight chords unless a later verified requirement adds arc handling.

## Out of scope
Do not expand the final goal into automatic robot-model guessing, arbitrary resource mapping, kinematics authoring, TCP/tool setup, collision checking, path planning, weld operations, cycle-time simulation, or full digital-twin automation unless the user explicitly changes scope again.

## Development principle
The user wants the final operational workflow to be **one pasteable Process Simulate .NET Script**. Supporting Python utilities and older split scripts may remain for debugging/reference, but they are not the primary final workflow.
