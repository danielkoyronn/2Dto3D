# CAD to Process Layout Assistant

A DXF preprocessing and factory-layout assistant for Siemens Tecnomatix Process Simulate.

The project focuses on the repetitive work that is inefficient to do manually:
- locating equipment in 2D factory CAD
- generating base-frame coordinates
- checking frame placement visually
- converting static factory layout geometry to STEP

It intentionally leaves Process-native work such as COJT/JT resource insertion, kinematics,
tool/TCP setup, collision, operations and path planning inside Process Simulate.

See `AGENTS.md` for the instructions Codex should follow and `docs/PROJECT_SPEC.md` for the agreed project scope.
