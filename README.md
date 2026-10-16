# CAD to Process Layout Assistant

A DXF preprocessing and factory-layout assistant for Siemens Tecnomatix Process Simulate.

The project focuses on the repetitive work that is inefficient to do manually:
- locating equipment in 2D factory CAD
- generating base-frame coordinates
- checking frame placement visually
- converting static factory layout geometry to STEP
- applying verified station/robot-label corrections to robot frame data
- optionally batch-placing an explicitly approved robot COJT on verified robot Frames

It intentionally leaves engineering decisions such as automatic robot-model guessing, kinematics authoring,
tool/TCP setup, collision, operations and path planning inside Process Simulate.

For the current 1013 factory workflow, `scripts/apply_mid_main_station_corrections.py` turns the BM/FM CAD-text audit rules into `FACTORY_RobotFrames_Corrected.csv` and a regenerated Process Simulate robot-frame C# script. See `docs/MID_MAIN_STATION_CORRECTIONS.md`.

See `AGENTS.md` for the instructions Codex should follow and `docs/PROJECT_SPEC.md` for the agreed project scope.
