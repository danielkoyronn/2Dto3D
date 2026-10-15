"""Equipment detection and base-frame extraction.

Never use an arbitrary bounding-box center for a robot when a deterministic
robot-base reference exists in the CAD definition.
"""


def detect_frames(document):
    raise NotImplementedError("Implement equipment/base-frame detection rules.")
