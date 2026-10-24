# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's equity cash flow and its quanto pricer (ql/cashflows/equitycashflow.hpp), through the
wheel's EquityCashFlow, EquityQuantoCashFlowPricer and setCouponPricer, on an EUR equity index with flat Actual/365
(Fixed) curves and stored past fixings, on the evaluation date 27 January 2023 (indexes/equityindex.py's).

- "flow" cases: a flow paying the index's growth or its ratio between a base and a fixing date, the base past (a
  stored fixing), today or future, the fixing past or future, with no pricer (IndexedCashFlow's amount) or the quanto
  pricer on constant or variance-curve equity and FX volatilities, a quanto curve and a correlation, an index without
  a dividend curve (QuantLib's flat zero curve): baseFixing, indexFixing, notional, growthOnly and amount. QuantLib's
  pricer refusals are throws: a fixing date before the base date, structures with different reference dates, and an
  empty handle (each text checked by tests/NanoQL.Tests/Conventions/EquityPricerRefusalTests.cs).
- "leg" cases: a leg of two equity flows and a simple cash flow, the pricer set by setCouponPricer, which skips the
  simple flow: each flow's amount.

A case's inputs hold its settings with the stored fixings, the index ({"interest", "dividend", "spot"}, each curve
{"rate", "referenceDate"} or null, as indexes/equityindex.py's), the flow's arguments and the pricer (null for none,
or {"quantoCurve", "equityVolatility", "fxVolatility", "correlation"}, a curve as the index's, a volatility a spec of
termstructures/volatility/equityfx/blackvoltermstructure.py, each null for an empty handle). Read back by
tests/NanoQL.Tests/Vectors/Coupons/EquityCashFlowVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


eqi = _load("wheel_equityindex", "indexes/equityindex.py")
bvt = _load("wheel_blackvoltermstructure", "termstructures/volatility/equityfx/blackvoltermstructure.py")

TODAY = eqi.TODAY
T = TODAY.serialNumber()
D = ql.Date
PAST = [(D(d, ql.January, 2023), 8600.0 + 3.5 * d) for d in (2, 3, 4, 5, 6, 9, 10, 11, 12, 13, 16, 26)] + \
    [(D(15, ql.December, 2022), 8450.0), (D(30, ql.November, 2022), 8390.0), (TODAY, 8690.0)]


def curve(rate, reference=T):
    return {"rate": rate, "referenceDate": reference}


FULL = {"interest": curve(0.03), "dividend": curve(0.01), "spot": 8700.0}
NO_DIVIDEND = {"interest": curve(0.025), "dividend": None, "spot": 8700.0}


def make_index(spec):
    spot = spec["spot"]
    return ql.EquityIndex("eqIndex", ql.TARGET(), ql.EURCurrency(), eqi.handle(spec["interest"]),
                          eqi.handle(spec["dividend"]),
                          ql.QuoteHandle(ql.SimpleQuote(spot)) if spot is not None else ql.QuoteHandle())


def pricer_spec(quanto, equity, fx, correlation):
    return {"quantoCurve": quanto, "equityVolatility": equity, "fxVolatility": fx, "correlation": correlation}


EQ_VOL = bvt.vol_spec_constant(TODAY, 0.25)
FX_VOL = bvt.vol_spec_constant(TODAY, 0.12)
EQ_CURVE_VOL = bvt.vol_spec_curve(TODAY, [91, 365, 730, 1826], [0.3, 0.27, 0.24, 0.22])
FX_CURVE_VOL = bvt.vol_spec_curve(TODAY, [91, 365, 730, 1826], [0.1, 0.11, 0.12, 0.125])
QUANTO = pricer_spec(curve(0.02), EQ_VOL, FX_VOL, 0.6)


def make_pricer(spec):
    if spec is None:
        return None
    vol = (lambda s: ql.BlackVolTermStructureHandle(bvt.black_vol(s)) if s is not None
           else ql.BlackVolTermStructureHandle())
    correlation = spec["correlation"]
    return ql.EquityQuantoCashFlowPricer(eqi.handle(spec["quantoCurve"]), vol(spec["equityVolatility"]),
                                         vol(spec["fxVolatility"]),
                                         ql.QuoteHandle(ql.SimpleQuote(correlation)) if correlation is not None
                                         else ql.QuoteHandle())


# (label, index, notional, base, fixing, payment, growth only, pricer)
FLOWS = [
    ("past base, future fixing", FULL, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024), D(30, ql.January, 2024),
     True, None),
    ("past base, future fixing, the ratio", FULL, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024),
     D(30, ql.January, 2024), False, None),
    ("both past", FULL, 2.5e6, D(30, ql.November, 2022), D(13, ql.January, 2023), D(17, ql.January, 2023), True, None),
    ("base today", FULL, 1e6, TODAY, D(27, ql.July, 2023), D(31, ql.July, 2023), True, None),
    ("both future", FULL, 1e6, D(27, ql.April, 2023), D(29, ql.January, 2024), D(31, ql.January, 2024), True, None),
    ("quanto, past base", FULL, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024), D(30, ql.January, 2024), True,
     QUANTO),
    ("quanto, past base, the ratio", FULL, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024),
     D(30, ql.January, 2024), False, QUANTO),
    ("quanto, negative correlation, five years", FULL, 1e6, D(16, ql.January, 2023), D(27, ql.January, 2028),
     D(31, ql.January, 2028), True, pricer_spec(curve(0.035), EQ_VOL, FX_VOL, -0.4)),
    ("quanto, zero correlation", FULL, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024), D(30, ql.January, 2024),
     True, pricer_spec(curve(0.02), EQ_VOL, FX_VOL, 0.0)),
    ("quanto, future base", FULL, 1e6, D(27, ql.April, 2023), D(29, ql.January, 2024), D(31, ql.January, 2024), True,
     QUANTO),
    ("quanto, variance curves", FULL, 1e6, D(26, ql.January, 2023), D(28, ql.January, 2025), D(30, ql.January, 2025),
     True, pricer_spec(curve(-0.005), EQ_CURVE_VOL, FX_CURVE_VOL, 0.3)),
    ("quanto, no dividend curve", NO_DIVIDEND, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024),
     D(30, ql.January, 2024), True, QUANTO),
    ("quanto, fixing before base", FULL, 1e6, D(26, ql.January, 2024), D(26, ql.January, 2023),
     D(30, ql.January, 2024), True, QUANTO),
    ("quanto, the quanto curve dated later", FULL, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024),
     D(30, ql.January, 2024), True, pricer_spec(curve(0.02, T + 1), EQ_VOL, FX_VOL, 0.6)),
    ("quanto, no FX volatility", FULL, 1e6, D(26, ql.January, 2023), D(26, ql.January, 2024), D(30, ql.January, 2024),
     True, pricer_spec(curve(0.02), EQ_VOL, None, 0.6)),
]


def flow_cases():
    out = []
    for k, (label, index_spec, notional, base, fixing, payment, growth_only, pricer) in enumerate(FLOWS):
        index = make_index(index_spec)
        with v.case_settings(TODAY, fixings=[(index, PAST)]) as st:
            f = ql.EquityCashFlow(notional, index, base, fixing, payment, growth_only)
            p = make_pricer(pricer)
            if p is not None:
                f.setPricer(p)
            out.append({"name": f"flow {label} / {k}",
                        "inputs": {"settings": st, "kind": "flow", "index": index_spec, "notional": notional,
                                   "baseDate": v.enc(base), "fixingDate": v.enc(fixing),
                                   "paymentDate": v.enc(payment), "growthOnly": growth_only, "pricer": pricer},
                        "expected": {"baseFixing": v.attempt(f.baseFixing), "indexFixing": v.attempt(f.indexFixing),
                                     "notional": f.notional(), "growthOnly": f.growthOnly(),
                                     "amount": v.attempt(f.amount)}})
    return out


def leg_cases():
    out = []
    flows = [(1e6, D(26, ql.January, 2023), D(26, ql.July, 2023), D(28, ql.July, 2023)),
             (1e6, D(26, ql.July, 2023), D(26, ql.January, 2024), D(30, ql.January, 2024))]
    simple = (25000.0, D(30, ql.January, 2024))
    for k, pricer in enumerate([QUANTO, pricer_spec(curve(0.01), EQ_CURVE_VOL, FX_CURVE_VOL, -0.2)]):
        index = make_index(FULL)
        with v.case_settings(TODAY, fixings=[(index, PAST)]) as st:
            leg = [ql.EquityCashFlow(n, index, b, f, p, True) for n, b, f, p in flows]
            leg.append(ql.SimpleCashFlow(*simple))
            ql.setCouponPricer(leg, make_pricer(pricer))
            out.append({"name": f"leg / {k}",
                        "inputs": {"settings": st, "kind": "leg", "index": FULL,
                                   "flows": [{"notional": n, "baseDate": v.enc(b), "fixingDate": v.enc(f),
                                              "paymentDate": v.enc(p)} for n, b, f, p in flows],
                                   "simple": {"amount": simple[0], "date": v.enc(simple[1])}, "pricer": pricer},
                        "expected": {"amounts": [v.attempt(x.amount) for x in leg]}})
    return out


def make_cases():
    return flow_cases() + leg_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/equitycashflow.hpp", make_cases)
