#!/usr/bin/env python3
"""Provision a dedicated, synthetic KOHAMI demo SQL fixture on an unused loopback port."""
from fixture_provisioning import run_cli


if __name__ == "__main__":
    run_cli("local-demo")
