from pathlib import Path
import base64
import gzip

ROOT = Path(__file__).resolve().parents[1]
SRC_DIR = ROOT / "src"
ARCHIVE = ROOT / "source" / "Create_FACTORY_StationNamed_Robots_PS2512.cs.gz.b64"
OUTPUT = SRC_DIR / "Create_FACTORY_StationNamed_Robots_PS2512.cs"


def main() -> None:
    SRC_DIR.mkdir(parents=True, exist_ok=True)
    encoded = ARCHIVE.read_text(encoding="ascii").strip()
    data = gzip.decompress(base64.b64decode(encoded))
    OUTPUT.write_bytes(data)
    print(f"Restored: {OUTPUT}")


if __name__ == "__main__":
    main()
