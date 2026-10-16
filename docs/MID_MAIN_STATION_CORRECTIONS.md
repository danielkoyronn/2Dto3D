# MID_MAIN BM/FM station correction pipeline

## Purpose

This applies the verified CAD-text rules from the MID_MAIN audit instead of leaving them as `print()`-only diagnostics.

The correction script is:

`python scripts/apply_mid_main_station_corrections.py`

It reads:

- `output/1013/DXF_Text_Audit.csv`
- `output/1013/FACTORY_RobotDetection_Audit.csv`

and writes:

- `output/1013/FACTORY_RobotFrames_Corrected.csv`
- `process_simulate/FactoryRobotFrames_Corrected.cs`

## Verified station split

Upper group (`Y > -1975000`):

- BM02: X=957496
- BM01: X=972315
- FM07: X=982662
- FM05: X=995953
- FM04: X=1001809
- FM03: X=1008036
- FM02: X=1015611
- FM01: X=1025683

Lower group (`Y <= -1975000`):

- BM05: X=950331
- BM06: X=958850
- BM07: X=965237
- BM08: X=971295
- BM09: X=977535
- BM10: X=984581
- BM12: X=996952

Each MID_MAIN robot is assigned to the nearest X reference within its upper/lower group.

## Bare robot labels

CAD text matching `R01`, `R02`, etc. is collected in the MID_MAIN window. Within each assigned station, robot Frames and bare R labels are matched one-to-one by nearest XY distance, with a 3000 mm maximum distance.

A label is never reused. If no unique nearby label can be matched, the original frame numeric suffix is retained rather than guessed.

Example corrected names:

- `MID_MAIN_BM02_ROBOT_R01`
- `MID_MAIN_FM05_ROBOT_R03`
- fallback: `MID_MAIN_BM12_ROBOT_052`

The `_ROBOT_` token is deliberately preserved so `BatchPlaceRobotOnFrames.cs` continues to discover these Frames automatically.

## Track robots

`MID_MAIN_TRACK_ROBOT_*` names are not renamed. They may receive station metadata in the CSV, but their frame names stay intact because rail/7th-axis handling is a separate workflow.

## Suspicious Z repair

The old generated frame source contained an obviously corrupt value for `MID_MAIN_ROBOT_014` (`Z=-18686004.694183 mm`).

The correction stage now treats `|Z| > 10000 mm` as invalid 2D-derived robot-base data, preserves the value in `OriginalZ`, and replaces the Process-frame Z with the explicit project default `0 mm`. The correction is recorded in `CorrectionStatus` and `CorrectionRemark`.

## Process Simulate order

Recommended order:

1. Generate/update the two audit CSVs from DXF.
2. Run `scripts/apply_mid_main_station_corrections.py`.
3. Review `FACTORY_RobotFrames_Corrected.csv`.
4. Run generated `process_simulate/FactoryRobotFrames_Corrected.cs` in a clean/copy study.
5. Run `process_simulate/BatchPlaceRobotOnFrames.cs` to place the approved COJT robot package on the corrected robot Frames.
6. Verify several BM/FM Frames and Robot Baseframes manually before scaling to the whole factory.

## Important

The Python correction script is now the source of truth for these MID_MAIN corrections. Do not manually edit dozens of hard-coded `CreateOne(...)` calls after every CAD-analysis change; regenerate the corrected CSV and C# instead.
