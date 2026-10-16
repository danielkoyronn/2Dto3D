# AGENTS.md

## Project
CAD to Process Layout Assistant

## Final goal
Convert a 2D factory DXF into the positioning data and static 3D environment needed to build a factory efficiently in Siemens Tecnomatix Process Simulate.

The project is NOT intended to guess arbitrary Process Simulate resources or fully automate simulation engineering. A limited exception is allowed for explicit, user-approved resource placement: when the user supplies a known COJT robot package and verified robot Frames already exist, the tool may batch-place that known package on those Frames and align the robot Baseframe exactly.

## Core responsibilities
1. Read and analyze factory DXF files.
2. Detect relevant equipment such as robots, fixtures, TTRs, conveyors, and other locatable resources.
3. Determine equipment/base-frame center coordinates from CAD geometry.
4. Extract or infer X, Y and Rz from reliable DXF entities/block transforms.
5. Generate consistent frame names.
6. Export frame data to CSV/XLSX-compatible tables.
7. Generate a visual frame-check output over the source CAD layout.
8. Generate Process Simulate Native Frame creation data/scripts where practical.
9. Convert static factory environment geometry (fence, floor, wall, column, platform) from 2D CAD to 3D STEP.
10. Validate CAD coordinates against Process Simulate frame coordinates.
11. When explicitly configured by the user, batch-place a known articulated COJT robot package on verified robot Frames and align its actual `Robot.Baseframe` to the target Frame. Do not infer the robot model automatically.

## Explicitly out of scope
Do NOT make these core project goals:
- Automatic guessing/mapping of CAD equipment to COJT/JT/STEP resources without explicit user configuration.
- Automatic selection of robot models based only on CAD appearance/color.
- Automatic complete 3D factory assembly.
- One-click DXF-to-complete-Process-Simulate-factory generation.
- Robot kinematics authoring, joint limits, TCP/tool setup.
- Fixture mechanism/kinematics.
- Collision checking.
- Robot paths, operations, weld points, cycle-time simulation.

## Design principle
CAD is the master layout.
Process Simulate is the master simulation environment.

Automate repetitive CAD interpretation, frame preparation, and explicitly approved repetitive placement. Keep engineering decisions and Process-native simulation work in Process Simulate.

## Coordinate rules
- Preserve original factory CAD coordinates. Do not move the whole factory to (0,0) merely for convenience.
- Robot frame origin should correspond to the actual robot base center defined by the CAD layout, not an arbitrary bounding-box center.
- X/Y should come from CAD whenever possible.
- Rz should come from block/entity rotation or another deterministic CAD rule whenever possible.
- Do not guess Z/Rx/Ry from 2D CAD. These require explicit configuration or Process/engineering reference data.
- Always provide a visual validation layer/output so detected frames can be checked before use.
- For batch robot placement, move the COJT root as one unit and preserve the package's internal links/joints. Align the package's real `Robot.Baseframe` to the target Frame and validate residual position/angle error.

## Development priorities
P0: DXF -> robot/base frames -> visual frame check -> CSV
P1: Improve fixture/TTR/conveyor frame detection
P1: Batch Process Simulate Native Frame creation workflow
P1: Explicit batch placement of a user-approved robot COJT package on verified robot Frames
P1: Fence/environment -> STEP
P2: CAD <-> Process frame validation report
P3: GUI only after the core pipeline is reliable

## Acceptance principle
A feature is worth adding only if it clearly reduces manual factory-building time without reducing engineering accuracy in Process Simulate.

For explicit resource placement, the script must be deterministic, repeatable, idempotent where practical, and must report failures instead of silently placing a resource at a suspicious coordinate.
