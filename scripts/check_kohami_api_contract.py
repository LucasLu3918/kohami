"""Validate proposed KOHAMI API documents and synthetic invariants, never run a product.
Requires PyYAML, jsonschema and openapi-spec-validator in the caller's environment.
"""
from copy import deepcopy
from pathlib import Path
import hashlib
import json
import re
import yaml
from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'docs/planning/kohami'
api = yaml.safe_load((PLAN / 'contracts/API_M1_M3.openapi.yaml').read_text())
schemas = api['components']['schemas']


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def validator(name):
    # Keep local #/components references in the document resource, with no network refs.
    return Draft202012Validator({**api, '$ref': '#/components/schemas/' + name},
                               format_checker=FormatChecker())


def rejects(name, value):
    check(not validator(name).is_valid(value), 'expected rejection: ' + name)


def walk(value):
    if isinstance(value, dict):
        yield value
        for item in value.values():
            yield from walk(item)
    elif isinstance(value, list):
        for item in value:
            yield from walk(item)


validate(api)
check(api['x-authority'] == 'PROPOSED', 'authority')
check(api['x-product-tests'] == 'OUT_OF_SCOPE_OPTION', 'no product PASS')
check(hashlib.sha256((PLAN / 'contracts/API_M0.openapi.yaml').read_bytes()).hexdigest()
      == api['x-m0-baseline-sha256'], 'M0 baseline changed; review required')
for node in walk(api):
    if '$ref' in node:
        check(node['$ref'].startswith('#/'), 'external refs not allowed')
        target = api
        for part in node['$ref'][2:].split('/'):
            target = target[part.replace('~1', '/').replace('~0', '~')]

example_count = 0
for name, schema in schemas.items():
    check(bool(schema.get('examples')), 'missing schema example: ' + name)
    for example in schema['examples']:
        validator(name).validate(example)
        example_count += 1

operation_ids = set()
covered = set()
for path, item in api['paths'].items():
    for method, op in item.items():
        if not isinstance(op, dict) or 'operationId' not in op:
            continue
        check(op['operationId'] not in operation_ids, 'duplicate operationId')
        operation_ids.add(op['operationId'])
        covered.add(op['x-trace-op'])
        params = op.get('parameters', [])
        keys = [(p['in'], p['name'].lower() if p['in'] == 'header' else p['name']) for p in params]
        check(len(set(keys)) == len(keys), 'duplicate parameter: ' + path)
        path_params = {p['name'] for p in params if p['in'] == 'path' and p['required']}
        check(path_params == set(re.findall(r'{([^}]+)}', path)), 'path parameter mismatch')
        if method != 'get':
            check(('header', 'x-csrf-token') in keys and op.get('x-origin-check'), 'CSRF: ' + path)
            if not op.get('x-proof-consumption') and not path.startswith('/auth/'):
                check(('header', 'idempotency-key') in keys, 'missing idem: ' + path)
        if op.get('x-version-target'):
            check(('header', 'if-match') in keys and '412' in op['responses']
                  and '428' in op['responses'], 'version errors: ' + path)
        if path.startswith('/admin/'):
            check(op['security'] == [{'SessionCookie': []}], 'admin authentication')
            check(op['security'] == [{'SessionCookie': []}], 'admin authentication')
        if not path.startswith('/products'):
            for code, response in op['responses'].items():
                if str(code).startswith('2'):
                    header = response.get('headers', {}).get('Cache-Control', {})
                    check(header.get('schema', {}).get('const') == 'no-store'
                          or 'no-store' in header.get('description', ''), 'private cache: ' + path)

required_groups = {'OP-%02d' % n for n in list(range(1, 15)) + [21, 22] + list(range(24, 39)) + [40, 42, 45, 46]}
check(required_groups <= covered, 'M1-M3 groups missing: ' + str(required_groups - covered))
check(api['paths']['/orders']['get']['security'] == [{'SessionCookie': []}], 'guest cannot list orders')
check(api['paths']['/orders/{id}/member-claims']['post']['security']
      == [{'SessionCookie': [], 'GuestActionCookie': []}], 'claim requires AND')
for path in ('/orders/{id}/cancellations', '/orders/{id}/return-requests'):
    sec = api['paths'][path]['post']['security']
    check({'GuestReadCookie': []} not in sec and {'CheckoutPayCookie': []} not in sec,
          'read/pay may not perform order actions')

negative_count = 0
for name, field, value in [
    ('CartItemRequest', 'quantity', 0), ('CartItemRequest', 'quantity', 1.5),
    ('PaymentAttemptRequest', 'state', 'Succeeded'),
    ('CheckoutRequest', 'mode', 'live'), ('CheckoutRequest', 'memberId', 'other_member'),
    ('CheckoutRequest', 'contact', {'email': 'real@example.com'}),
    ('RefundRequest', 'lines', []), ('RefundRequest', 'amountTwd', -1),
    ('RefundRequest', 'amountTwd', 9007199254740992), ('RefundRequest', 'amountTwd', 425.5),
    ('BoundRefundRequest', 'amountTwd', 60), ('BoundRefundRequest', 'bank', 'arbitrary'),
    ('BoundRefundRequest', 'paymentId', 'arbitrary'), ('BoundRefundRequest', 'lines', []),
    ('ReasonCommand', 'mfa', True), ('ShipmentTransition', 'command', 'Succeeded')
]:
    value_dict = deepcopy(schemas[name]['examples'][0])
    value_dict[field] = value
    rejects(name, value_dict)
    negative_count += 1
value = deepcopy(schemas['ReturnTransition']['examples'][0])
value['command'] = 'Receive'
value.pop('receivedLines', None)
rejects('ReturnTransition', value)
negative_count += 1
value = deepcopy(schemas['ReturnTransition']['examples'][-1])
value['exceptions'] = []
rejects('ReturnTransition', value)
negative_count += 1
value = deepcopy(schemas['RefundOperation']['examples'][0])
value['units'] = []
value['shippingTwd'] = 0
rejects('RefundOperation', value)
negative_count += 1
value = deepcopy(schemas['RefundOperation']['examples'][0])
value['kind'] = 'PaymentCompensation'
rejects('RefundOperation', value)  # compensation cannot consume product units
negative_count += 1
public_forbidden = {'reserved', 'password', 'passwordHash', 'sourceRightsRef', 'costTwd',
                    'contactEmail', 'proof', 'requestToken'}
for name in ('Product', 'ProductSummary', 'ProductSku', 'ProductMedia', 'CheckoutStatus', 'Order'):
    seen = set()
    def properties(schema):
        if '$ref' in schema:
            n = schema['$ref'].split('/')[-1]
            if n in seen:
                return
            seen.add(n)
            yield from properties(schemas[n])
        elif isinstance(schema, dict):
            yield from schema.get('properties', {})
            for child in schema.get('properties', {}).values():
                yield from properties(child)
            if 'items' in schema:
                yield from properties(schema['items'])
    check(not public_forbidden.intersection(properties(schemas[name])), 'private data in ' + name)

# These checks validate synthetic contract examples, not runtime authorization/SQL behavior.
m1 = json.loads((PLAN / 'fixtures/M1_CATALOG.json').read_text())['quoteExample']
m2 = json.loads((PLAN / 'fixtures/M2_ORDER_CASES.json').read_text())
m3 = json.loads((PLAN / 'fixtures/M3_REFUND_CASES.json').read_text())
snapshot = schemas['PriceSnapshot']['examples'][0]
check(snapshot['totalTwd'] == snapshot['merchandiseTwd'] - snapshot['discountTwd']
      + snapshot['shippingTwd'] == m1['totalTwd'] == 1590, 'snapshot total')
check(m3['snapshotFixture'] == m1, 'M1/M3 snapshot')
# M2 fixture names may evolve, compare its explicit snapshot structurally.
check(m2['snapshotFixture'] == m1, 'M1/M2 snapshot')
for line, original in zip(snapshot['lines'], m1['lines'], strict=True):
    check(line['quantity'] == len(line['units']), 'unit quantity')
    paid = [u['paidTwd'] for u in line['units']]
    check(paid == original['unitPaidAllocationsTwd'] and sum(paid) == line['paidTwd'], 'allocation')
    check(line['unitPriceTwd'] * line['quantity'] - line['discountTwd'] == line['paidTwd'], 'line total')
for line in schemas['SnapshotLine']['examples']:
    check(line['quantity'] == len(line['units']), 'standalone line qty')
    check(line['unitPriceTwd'] * line['quantity'] - line['discountTwd'] == line['paidTwd']
          == sum(u['paidTwd'] for u in line['units']), 'standalone line allocation')
for balance in [schemas['CollectionBalance']['examples'][0], schemas['RefundCase']['examples'][0]['source']]:
    check(balance['collectedTwd'] - balance['succeededRefundTwd'] - balance['pendingTwd']
          == balance['availableTwd'] >= 0, 'source balance')
for operation in schemas['RefundOperation']['examples']:
    if operation['kind'] == 'OrderRefund':
        check(sum(u['paidTwd'] for u in operation['units']) + operation['shippingTwd']
              == operation['amountTwd'], 'refund allocated total')
stock = schemas['StockBalance']['examples'][0]
check(stock['onHand'] - stock['reserved'] == stock['sellable'] >= 0, 'stock example')
report = schemas['ReportAggregate']['examples'][0]
check(report['collectedTwd'] - report['succeededRefundTwd'] == report['netConfirmedTwd'], 'report net')
check(report['netConfirmedTwd'] - report['pendingTwd'] == report['availableTwd'] >= 0, 'report available')
for c in m3['collections']:
    check(c['collectedTwd'] - c['succeededRefundTwd'] - c['pendingUnknownTwd'] == c['availableTwd'] >= 0,
          'fixture source amount')
check(m3['nextOrdinaryRefund']['requestAmountTwd'] == 425, 'partial eligible amount')
print(json.dumps({'result': 'PASS', 'scope': 'proposal_document_contract_only',
                  'operations': len(operation_ids), 'schemas': len(schemas),
                  'schema_examples': example_count, 'negative_schema_cases': negative_count,
                  'm0_baseline': 'UNCHANGED', 'product_tests': 'NOT_EXECUTED'}, ensure_ascii=False))
