# Batch Robot Placement in Process Simulate

## Purpose

The verified frame-generation workflow creates robot base Frames and then reuses one approved articulated COJT robot package on those verified Frames.

The current placement script is:

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

## Corrected MID_MAIN Frames

Before placement, run:

`python scripts/apply_mid_main_station_corrections.py`

This applies the verified BM/FM station rules and unique nearby bare `Rxx` labels, writes `FACTORY_RobotFrames_Corrected.csv`, and generates `process_simulate/FactoryRobotFrames_Corrected.cs`.

Corrected names deliberately keep the `_ROBOT_` token, for example:

- `MID_MAIN_BM02_ROBOT_R01`
- `MID_MAIN_FM05_ROBOT_R03`

Therefore the existing batch placement script discovers corrected Frames without another naming change.

## Current resource configuration

```csharp
private const string RobotPackagePath =
    @"D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt";
```

The script currently uses the same **KR210_R2700 COJT package** for every matching robot Frame. This is intentional for the first batch-placement test. It does not infer different robot models from CAD.

## Track robot behavior

When `IncludeTrackRobotFrames = true`, the script also puts the robot arm on `*_TRACK_ROBOT_*` Frames. It does **not** create the rail / tracker / seventh-axis resource. That requires a separate approved track package or mapping rule.

## Safe fence

Safe-fence resource placement is intentionally pending. The user will provide the Safe Fence resource/file next. Do not guess its package path, origin, or placement rule before that file is supplied.

When the Safe Fence file is supplied, add it as a separate deterministic import/placement stage rather than mixing fence logic into robot Baseframe alignment.

## Test sequence

1. Generate the DXF audit CSVs.
2. Run `scripts/apply_mid_main_station_corrections.py`.
3. Review the corrected CSV, especially BM/FM station and `RobotLabel` columns.
4. Open a copy/clean Process Simulate study.
5. Run generated `FactoryRobotFrames_Corrected.cs`.
6. Set `DryRun = true` in `BatchPlaceRobotOnFrames.cs` and review target count/names.
7. Set `DryRun = false`.
8. Verify several robot Baseframes manually against their target Frames.
9. Only then run the full factory batch.

## Known data guard

The old uploaded frame-generation source contained one obviously suspicious robot Z value (`MID_MAIN_ROBOT_014` was many kilometers from the normal factory Z range). The correction pipeline preserves that raw value as `OriginalZ`, repairs the generated Process frame to the configured 2D robot-base default Z=0 mm, and records the repair. The batch placement script independently keeps its `abs(Z) > 10000 mm` guard as a second layer of protection.
