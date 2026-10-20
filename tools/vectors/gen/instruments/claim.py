# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's claims (ql/instruments/claim.hpp), through the wheel's FaceValueClaim and
FaceValueAccrualClaim.

- "face" cases: the face-value claim's amount, notional (1 - recovery), at a default date or none, for notionals of
  1,000,000 and 100 and recoveries of 0, 25% and 40%.
- "accrual" cases: the face-value accrual claim on tools/vectors/gen/instruments/bond.py's semiannual fixed-rate bond
  and its amortizing bond: at default dates through the bond's life, before its first coupon and after its last, and
  with none, the bond's settlement date; the claim deducts the bond's accrued amount over its notional at the date.

A case's inputs hold its settings, the claim's kind, the bond spec (null for a face-value claim), the default date
(null for none), the notional and the recovery rate. Read back by
tests/NanoQL.Tests/Vectors/Instruments/ClaimVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_discountingbondengine",
                                               os.path.join(v.GEN_ROOT, "pricingengines", "bond",
                                                            "discountingbondengine.py"))
dbe = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dbe)
bd = dbe.bd

TODAY = ql.Date(17, ql.June, 2026)
DEFAULT_DATES = [None, ql.Date(20, ql.January, 2025), ql.Date(16, ql.March, 2026), ql.Date(17, ql.June, 2026),
                 ql.Date(15, ql.September, 2026), ql.Date(31, ql.December, 2027), ql.Date(14, ql.March, 2028),
                 ql.Date(20, ql.June, 2030)]


def make_cases():
    out = []
    k = 0
    for notional in (1e6, 100.0):
        for recovery in (0.0, 0.25, 0.4):
            for d in (None, ql.Date(15, ql.September, 2026)):
                with v.case_settings(TODAY) as st:
                    claim = ql.FaceValueClaim()
                    out.append({"name": f"face {notional} {recovery} {d} / {k}",
                                "inputs": {"settings": st, "kind": "face", "bond": None, "defaultDate": v.enc(d),
                                           "notional": notional, "recoveryRate": recovery},
                                "expected": {"amount": claim.amount(d or ql.Date(), notional, recovery)}})
                k += 1
    bonds = [b for b in dbe.bonds() if b[0] in ("fixed 6M", "amortizing")]
    for name, spec, _, _ in bonds:
        for d in DEFAULT_DATES:
            for recovery in (0.25, 0.4):
                with v.case_settings(TODAY) as st:
                    bond = bd.make_bond(spec)
                    claim = ql.FaceValueAccrualClaim(bond)
                    out.append({"name": f"accrual {name} {d} {recovery} / {k}",
                                "inputs": {"settings": st, "kind": "accrual", "bond": spec, "defaultDate": v.enc(d),
                                           "notional": 1e6, "recoveryRate": recovery},
                                "expected": {"amount": v.attempt(lambda: claim.amount(d or ql.Date(), 1e6, recovery))}})
                k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/claim.hpp", make_cases)
