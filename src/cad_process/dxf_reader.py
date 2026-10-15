"""DXF loading and normalized entity extraction.

Initial responsibilities:
- preserve factory/global CAD coordinates
- recursively resolve INSERT/BLOCK transforms
- expose layer, block, text, geometry, insertion point, and rotation metadata
"""


def load_dxf(path):
    raise NotImplementedError("Implement DXF parser using ezdxf.")
