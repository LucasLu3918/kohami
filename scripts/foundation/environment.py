"""Load one explicit private KOHAMI environment profile without logging values."""
from pathlib import Path
import os
import stat


def parse_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for number, raw in enumerate(path.read_text().splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            raise ValueError(f"ENV_FILE_LINE_INVALID:{number}")
        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip()
        if not key.replace("_", "").isalnum() or not key[0].isalpha():
            raise ValueError(f"ENV_FILE_KEY_INVALID:{number}")
        if value[:1] in {"'", '"'}:
            quote = value[0]
            if len(value) < 2 or value[-1] != quote:
                raise ValueError(f"ENV_FILE_QUOTE_INVALID:{number}")
            value = value[1:-1]
        if key in values:
            raise ValueError(f"ENV_FILE_DUPLICATE_KEY:{key}")
        values[key] = value
    return values


def load_profile(root: Path, profile: str | None = None) -> dict[str, str]:
    selected = profile or os.environ.get("KOHAMI_ENVIRONMENT", "local")
    if selected not in {"local", "dev"}:
        raise ValueError("KOHAMI_ENVIRONMENT_INVALID")
    path = root / ".local" / "environments" / f"{selected}.env"
    if path.is_symlink() or not path.is_file():
        raise RuntimeError(f"ENVIRONMENT_PROFILE_REQUIRED:{selected}")
    if selected == "local" and stat.S_IMODE(path.stat().st_mode) & 0o077:
        raise RuntimeError("LOCAL_ENVIRONMENT_FILE_PERMISSIONS_INVALID")
    values = parse_env_file(path)
    if values.get("KOHAMI_ENVIRONMENT") != selected:
        raise RuntimeError("ENVIRONMENT_PROFILE_MISMATCH")
    return values
