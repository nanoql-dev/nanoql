# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NthToDefault priced by its IntegralNtdEngine (ql/experimental/credit/nthtodefault.hpp, .cpp,
integralntdengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness
(tools/cxx/src/experimental/credit/integralntdengine.cpp) on tools/vectors/gen/cxx/experimental/credit/defaultevent.py's
specs and termstructures/credit/creditspec.hpp's schedules and discount curves.

QuantLib's test market (ten names at hazard 0.01, recovery 0.4, a 5% continuous discount curve, a five-year quarterly
schedule from 1 September 2006) and a smaller heterogeneous one, on constant-loss models (Gaussian at correlations 0,
0.3 and 0.6; Student t of orders 5, 5 at 0.3; Gaussian by factor weights): swaps of every order, both sides, with and
without the premium accrued at default, at weekly and monthly integration steps; upfronts only where the swap's nominal
is the basket's notional (QuantLib's upfront is the basket's remaining notional times the rate, the S-xcr plan's XR8
(xi), held in Divergences/NthToDefaultUpfrontTests.cs); a swap that expired; and the inspectors. No swap started before
the discount curve's date and before its first payment, whose upfront QuantLib discounts before the curve (XR8 (x),
held in Divergences/NthToDefaultReferenceDateTests.cs). And the refused swaps (an order above the basket's size, a
basket born after the schedule's start). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/IntegralNtdEngineCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pf = _load("cxx_defaultevent", "cxx/experimental/credit/defaultevent.py")
dts = pf.dts

TODAY = pf.TODAY
D = ql.Date
TARGET = {"type": "TARGET"}
A360 = {"type": "Actual360"}
SELLER, BUYER = int(ql.Protection.Seller), int(ql.Protection.Buyer)
DISCOUNT = dts.discount_spec(TODAY, 0.05)


def schedule(start, end, months=3):
    return {"effectiveDate": start.serialNumber(), "terminationDate": end.serialNumber(), "tenor": [months, pf.MONTHS],
            "calendar": TARGET, "convention": int(ql.Following), "terminationDateConvention": int(ql.Following),
            "rule": int(ql.DateGeneration.Backward)}


SCHEDULE = schedule(D(1, 9, 2006), D(1, 9, 2011))
SHORT = schedule(D(1, 9, 2006), D(1, 9, 2008), 6)
HW = pf.basket(TODAY, [10.0] * 10, pf.flat_pool([0.01] * 10))
SMALL = pf.basket(TODAY, [10, 30, 60], pf.flat_pool([0.02, 0.01, 0.04]))


def ntd(n, side, sched, upfront, premium, nominal, accrual):
    return {"n": n, "side": side, "schedule": sched, "upfrontRate": upfront, "premiumRate": premium,
            "dayCounter": A360, "nominal": nominal, "settlePremiumAccrual": accrual}


MARKETS = [
    ("Hull-White, Gaussian at 0", HW, pf.constant_loss("Gaussian", [0.4] * 10, correlation=0.0)),
    ("Hull-White, Gaussian at 0.3", HW, pf.constant_loss("Gaussian", [0.4] * 10, correlation=0.3)),
    ("Hull-White, Gaussian at 0.6", HW, pf.constant_loss("Gaussian", [0.4] * 10, correlation=0.6)),
    ("Hull-White, Student t at 0.3", HW, pf.constant_loss("T", [0.4] * 10, correlation=0.3, t_orders=[5, 5])),
    ("three names, Gaussian by weights", SMALL, pf.constant_loss("Gaussian", [0.3, 0.5, 0.4],
                                                                  factor_weights=[[0.4], [0.6], [0.2]])),
]


def swaps(size, notional):
    out = [ntd(n, SELLER, SCHEDULE, 0.0, 0.02, notional * 10, True) for n in range(1, size + 1)]
    out += [ntd(1, BUYER, SCHEDULE, 0.0, 0.02, notional * 10, True),
            ntd(2, SELLER, SCHEDULE, 0.0, 0.015, notional, False),
            ntd(1, BUYER, SHORT, 0.03, 0.01, notional, True),
            ntd(2, SELLER, SHORT, 0.01, 0.02, notional, False)]
    return out


EXPIRED = ntd(1, SELLER, schedule(D(1, 9, 2001), D(1, 6, 2006)), 0.0, 0.02, 100.0, True)
OLD = pf.basket(D(1, 1, 2001), [10.0] * 10, pf.flat_pool([0.01] * 10))  # born before the expired swap's start


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, b, m in MARKETS:
            with H.case_settings(TODAY.serialNumber()) as st:
                notional = sum(b["notionals"])
                ss = swaps(len(b["notionals"]), notional)
                steps = [[1, pf.WEEKS]] + ([[1, pf.MONTHS]] if b is SMALL else [])
                expected = {"inspect": [call("NthToDefault::inspect", b, m, s) for s in ss],
                            "calculate": [call("IntegralNtdEngine::calculate", b, m, s, step, DISCOUNT)
                                          for step in steps for s in ss]}
                out.append({"name": name,
                            "inputs": {"settings": st, "basket": b, "model": m, "swaps": ss, "steps": steps,
                                       "discount": DISCOUNT},
                            "expected": expected})
        with H.case_settings(TODAY.serialNumber()) as st:
            b, m = MARKETS[1][1], MARKETS[1][2]
            refused = [ntd(11, SELLER, SCHEDULE, 0.0, 0.02, 100.0, True),
                       ntd(1, SELLER, schedule(D(1, 6, 2006), D(1, 6, 2009)), 0.0, 0.02, 100.0, True)]
            out.append({"name": "expired and refused",
                        "inputs": {"settings": st, "basket": b, "model": m, "old": OLD, "expired": EXPIRED,
                                   "refused": refused, "discount": DISCOUNT},
                        "expected": {"isExpired": call("NthToDefault::isExpired", OLD, m, EXPIRED),
                                     "expired": call("IntegralNtdEngine::calculate", OLD, m, EXPIRED, [1, pf.WEEKS],
                                                     DISCOUNT),
                                     "refused": [call("NthToDefault::inspect", b, m, s) for s in refused]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/integralntdengine.hpp", cases)
