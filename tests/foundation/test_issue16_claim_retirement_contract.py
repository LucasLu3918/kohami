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

    def test_issue16_marks_claim_acceptance_as_retired_history(self):
        issue = (ROOT / "docs/issues/kohami/16-member-checkout-private-orders.md").read_text()
        self.assertIn("退役前的歷史實作", issue)
        self.assertIn("退役前實作紀錄", issue)
        self.assertIn("不代表現行功能", issue)
        acceptance = "\n".join(line for line in issue.splitlines() if line.startswith("- [x]"))
        self.assertNotIn("claim capability", acceptance)
        self.assertNotIn("member-claims", acceptance)


if __name__ == "__main__":
    unittest.main()
