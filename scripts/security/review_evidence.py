"""Fail-closed review-evidence adapter for the KOHAMI-local Gate.

Independent reviewer attestations are not configured for this demo profile.
When a test matrix requires them, the Gate reports UNVERIFIED and fails closed.
"""

from pathlib import Path
from typing import Any


def validate_report(
    root: Path,
    report_path: Path,
    expected_candidate: dict[str, str],
    *,
    allow_external: bool = False,
    trust_store: Path | None = None,
) -> dict[str, Any]:
    del root, report_path, expected_candidate, allow_external, trust_store
    return {
        "status": "UNVERIFIED",
        "reason_codes": ["project_gate_review_attestation_not_configured"],
    }
