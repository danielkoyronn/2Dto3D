# 3DRbotbuild

Verified Process Simulate 2512 project for station-based Robot Frame naming and batch Robot placement.

## Current baseline

The verified source is `Create_FACTORY_StationNamed_Robots_PS2512.cs`.

Naming convention:

- Robot Frame: `<LINE>_<STATION>_R<NN>`
- Robot Resource: `<LINE>_<STATION>_R<NN>_<MODEL>`

Example:

- Frame: `EC_EC40_R01`
- Robot: `EC_EC40_R01_KR210_R2700`

The script keeps the verified embedded Frame coordinates, reads DXF TEXT/MTEXT only for station/Rxx naming, and aligns the complete approved COJT Robot by `Robot.Baseframe` to each target Frame.

## Restore the source after cloning

From the repository root:

```powershell
python .\3DRbotbuild\tools\restore_source.py
```

This creates:

```text
3DRbotbuild/src/Create_FACTORY_StationNamed_Robots_PS2512.cs
```

You can then open that file in VSCode and continue development.

## Process Simulate inputs

Default paths in the current verified script:

```text
DXF:
D:\WEDING\MODEL\1013.dxf

Robot COJT:
D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt
```

If the DXF is not at the default path, the script opens a file picker for the DXF naming source.

## Current scope

1. Keep the verified factory/line/Robot Frame coordinates.
2. Read DXF station labels and nearby `R01/R02/...` labels.
3. Rename Robot Frames to engineering names such as `EC_EC40_R01`.
4. Rename/reuse existing generated Robots when possible.
5. Otherwise insert one complete articulated Robot on each Robot Frame.
6. Align the actual Robot Baseframe to the target Frame.
7. Reject suspicious Robot Z values and duplicate engineering names.

This project is the new development baseline. New Robot/fence/layout functions should be added here incrementally without replacing the verified Frame + Robot placement behavior.
