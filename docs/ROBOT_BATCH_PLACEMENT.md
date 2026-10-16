# Batch Robot Placement in Process Simulate

## Purpose

The verified frame-generation script already creates factory/line Frames and robot base Frames, then demonstrates importing one complete articulated COJT robot and aligning its real `Robot.Baseframe` to one target Frame.

The next step is repetitive placement: reuse that same approved COJT package on every generated robot Frame.

## Script

`process_simulate/BatchPlaceRobotOnFrames.cs`

## Current behavior

1. Reads all `TxFrame` objects in the active Process Simulate study.
2. Selects Frames whose names contain `_ROBOT_`.
3. Includes `_TRACK_ROBOT_` Frames when `IncludeTrackRobotFrames = true`.
4. Inserts a new instance from the configured complete COJT package.
5. Moves the COJT root as one unit.
6. Aligns the imported robot's actual `Robot.Baseframe` to the target Frame.
7. Checks residual base position and angle error.
8. Checks that at least six joints/driving joints are present.
9. Skips an instance if that generated instance name already exists.
10. Rejects suspicious target Z values instead of placing a robot far away.
11. Reports success / skip / fail counts.

## Current resource configuration

```csharp
private const string RobotPackagePath =
    @"D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt";
```

The script currently uses the **same KR210_R2700 COJT package for every matching robot Frame**. This is intentional for the first batch-placement test. It does not infer different robot models from CAD.

## Track robot behavior

When `IncludeTrackRobotFrames = true`, the script also puts the robot arm on `*_TRACK_ROBOT_*` Frames. It does **not** create the rail / tracker / seventh-axis resource. That requires a separate approved track package or mapping rule.

## Safe fence

Safe-fence resource placement is intentionally pending. The user will provide the Safe Fence resource/file next. Do not guess its package path, origin, or placement rule before that file is supplied.

When the Safe Fence file is supplied, add it as a separate deterministic import/placement stage rather than mixing fence logic into robot Baseframe alignment.

## Test sequence

Recommended first test:

1. Open a copy of the Process Simulate study.
2. Ensure the generated Frames already exist.
3. Set `DryRun = true` and run the script; review target Frame names/count.
4. Set `DryRun = false`.
5. Test a reduced study or one line first if memory/load time is a concern.
6. Verify several robot Baseframes manually against their target Frames.
7. Only then run the full factory batch.

## Known data guard

The uploaded frame-generation source contains one obviously suspicious robot Z value (`MID_MAIN_ROBOT_014` is many kilometers from the normal factory Z range). The batch script therefore rejects robot target Frames with `abs(Z) > 10000 mm` and reports them instead of silently placing a robot there.
