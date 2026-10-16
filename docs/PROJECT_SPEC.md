# Project Specification

## Current source of truth

The current baseline is the user-verified Process Simulate 2512 `.NET Script`:

`process_simulate/Create_FACTORY_RobotBase_Frames_PS2512_BatchRobot.cs`

Do not replace this working workflow with the previous direct-DXF / Fence all-in-one experiment unless the user explicitly requests that change again.

## Current workflow

1. Create the approved factory/line base Frames.
2. Create the approved Robot Frames using the embedded coordinate/orientation table.
3. Find target Frames by the naming token `_ROBOT_`.
4. Reuse the explicitly approved complete articulated Robot COJT package.
5. Insert one Robot resource for every Robot Frame.
6. Align the real `Robot.Baseframe` to the target Frame by moving the complete COJT root as one unit.
7. Verify residual position and angular error.
8. Skip duplicate generated instances.
9. Skip suspicious target Z coordinates instead of placing a Robot at a corrupted position.

## Naming

Robot target Frames include forms such as:

- `<AREA>_ROBOT_<NNN>`
- `<AREA>_TRACK_ROBOT_<NNN>`
- existing approved special-area names already embedded in the script

Generated Robot instances use:

`AUTO_<FRAME_NAME>_KR210_R2700`

## Approved Robot resource

`D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt`

The COJT must remain a complete articulated resource. Do not substitute only the inner JT because Robot joints/Baseframe would be lost.

## Placement rule

For each Robot Frame:

- insert the complete COJT resource;
- obtain the inserted `TxRobot`;
- obtain `robot.Baseframe`;
- calculate the Baseframe transform relative to the inserted COJT root;
- move the inserted root so the Robot Baseframe coincides with the target Frame;
- reject placement if residual error exceeds the configured tolerance.

Current validation tolerance:

- position: `0.1 mm`
- rotation: `0.01 deg`

## Track Robot scope

`TRACK_ROBOT` Frames currently receive the Robot arm only. Automatic rail / seventh-axis creation is not part of the current verified baseline.

## Coordinate guard

Do not place a Robot when the target Frame has a non-finite Z or `|Z| > 10000 mm`. This protects against known corrupted source values such as the abnormal MID_MAIN Z record.

## Development rule

New work must extend this verified script incrementally. Preserve existing Frame creation and Robot placement unless a change is specifically requested and revalidated in Process Simulate.

Likely next stages may include Safe Fence placement or additional resource types, but they should be added on top of this working baseline rather than replacing it.
