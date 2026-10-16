# CAD to Process Layout Assistant

Current final workflow: **one Process Simulate 2512 .NET Script**.

Canonical file:

`process_simulate/AllInOneDxfRobotFence.cs`

The script performs the reduced factory-building workflow in one run:

1. Read the configured ASCII DXF directly.
2. Detect Robot / Track-Robot / Safe-Fence placement data.
3. Apply the verified 1013 MID_MAIN BM/FM + nearby `Rxx` naming rules.
4. Create Process Simulate Native Frames.
5. Insert the approved Robot COJT once at `(0,0,0)` and instantiate/alignment-place it on every Robot Frame.
6. Insert the supplied `BSL_SF-0044.jt` once at `(0,0,0)` and instantiate it on every Fence Frame.
7. Report create/skip/fail results and reject suspicious target coordinates.

The final user workflow does **not** require a separate Python preprocessing command, corrected CSV handoff, or separate Robot/Fence placement scripts. Older utilities remain in the repository as development/reference tools.

Current naming standard:

- `<AREA>_ROBOT_<NNN>`
- `<AREA>_TRACK_ROBOT_<NNN>`
- `MID_MAIN_<STATION>_ROBOT_R<NN>` when a verified nearby CAD robot label is matched
- `<AREA>_FENCE_<NNN>`

Current approved resources:

- `BSR_OP10_R01_KR210_R2700.cojt`
- `BSL_SF-0044.jt`

See `AGENTS.md` for the project rules and safeguards.
