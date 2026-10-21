"""No-op telemetry context for the KOHAMI-local Gate.

The project profile does not write AIPS run telemetry. Explicit attempts to
request telemetry are rejected by integration_gate.py.
"""

from contextlib import contextmanager
from pathlib import Path
from typing import Iterator


@contextmanager
def observed_stage(project: Path, run_id: str | None, name: str) -> Iterator[dict[str, str]]:
    del project, run_id, name
    yield {"status": "DISABLED"}
