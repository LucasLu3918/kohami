#!/usr/bin/env python3
"""Create a fresh disposable SQL fixture for wallet HTTP integration work."""
from fixture_provisioning import run_cli


if __name__ == "__main__":
    run_cli("wallet03-http")
