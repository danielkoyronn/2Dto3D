# 2Dto3D — Process Simulate Factory Placement

Current project baseline is the **verified Process Simulate 2512 .NET Script workflow** that the user has tested successfully.

## Canonical script

`process_simulate/Create_FACTORY_RobotBase_Frames_PS2512_BatchRobot.cs`

## Current behavior

1. Create the known factory/line base Frames and Robot Frames from the approved coordinate table embedded in the script.
2. Find every Process Simulate Frame whose name contains `_ROBOT_`.
3. Reuse the approved complete articulated Robot package:
   `BSR_OP10_R01_KR210_R2700.cojt`.
4. Insert one Robot instance for every Robot Frame.
5. Move the complete COJT root as one unit and align the actual `Robot.Baseframe` to the target Frame.
6. Validate the final Baseframe residual position/angle error.
7. Skip duplicate generated Robot instances.
8. Skip obviously corrupted target Z coordinates (`|Z| > 10000 mm`).
9. `TRACK_ROBOT` Frames currently receive the Robot arm only; no seventh-axis rail is generated automatically.

Generated Robot instance naming:

`AUTO_<FRAME_NAME>_KR210_R2700`

Examples:

- `AUTO_BSL_ROBOT_001_KR210_R2700`
- `AUTO_BSR_ROBOT_005_KR210_R2700`
- `AUTO_EC_ROBOT_001_KR210_R2700`
- `AUTO_BSL_TRACK_ROBOT_001_KR210_R2700`

## Current project decision

The previous direct-DXF + automatic Fence all-in-one experiment is **not the current baseline**. The working Frame-generation script supplied by the user is now the source of truth, and further development should extend this script incrementally.

Next additions should preserve the verified Frame and Robot-placement behavior instead of replacing it with a new pipeline.
