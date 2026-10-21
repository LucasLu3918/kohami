#!/usr/bin/env python3
"""Create a fresh disposable SQL fixture for Issue 06 fault/replay evidence."""
from fixture_provisioning import run_cli


if __name__ == "__main__":
    run_cli("issue06")
