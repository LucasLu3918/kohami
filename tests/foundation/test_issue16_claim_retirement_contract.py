import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class Issue16ClaimRetirementContractTests(unittest.TestCase):
    def test_runtime_contract_excludes_retired_claim_capabilities(self):
        contract = json.loads(
            (ROOT / "docs/contracts/runtime/Core.openapi.json").read_text()
        )
        schemas = contract["components"]["schemas"]

        self.assertNotIn("Claim", schemas["CommerceActionAvailability"]["properties"]["action"]["enum"])
        for name in ("CommerceGuestVerificationRequest", "CommerceGuestVerificationConfirm"):
            self.assertEqual(
                ["read", "cancel", "return"],
                schemas[name]["properties"]["intent"]["enum"],
            )
        self.assertNotIn(
            "guest.action.claim",
            schemas["CommerceCapabilityConfirmed"]["properties"]["scope"]["enum"],
        )

        paths = contract["paths"]
        self.assertEqual(27, sum(len(operations) for operations in paths.values()))
        self.assertNotIn("/orders/{id}/member-claims", paths)
        self.assertIn("/guest-order-verifications", paths)
        self.assertIn("/guest-order-verifications/confirm", paths)

    def test_current_docs_keep_member_checkout_and_guest_legacy_separate(self):
        conflicts = (ROOT / "docs/todo/confuse.md").read_text()
        architecture = (ROOT / "docs/architecture.md").read_text()
        self.assertIn("CF-01", conflicts)
        self.assertIn("會員才可建立新訂單", conflicts)
        self.assertIn("訪客歷史訂單", architecture)
        self.assertNotIn("member-claims", json.dumps(json.loads(
            (ROOT / "docs/contracts/runtime/Core.openapi.json").read_text()
        )["paths"]))


if __name__ == "__main__":
    unittest.main()
