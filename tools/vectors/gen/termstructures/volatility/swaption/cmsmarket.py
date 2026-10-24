# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CmsMarket (ql/termstructures/volatility/swaption/cmsmarket.hpp, .cpp): a set of CMS quotes on
swap lengths of 1, 5, 10 and 20 years over the 2- and 10-year EUR swap indexes, on the evaluation date and curve of
tools/vectors/gen/cashflows/conundrumpricer.py (16 June 2025, a flat 5%), the Ibor legs on the 2-year index's Euribor 6M
and discounted on its curve.

Each case is a pricer per index on a volatility: the analytic Hagan pricer on the standard model and the at-the-money
matrix, the numeric one on the non-parallel shifts at a reversion of 1%, and the linear TSR pricer at 1%; then the first
market repriced on the normal matrix keeping its pricers' reversion, and the second on the lognormal matrix at a
reversion of 2%. The outputs are browse(), impliedCmsSpreads() and spreadErrors(), each flattened row by row, and the
weighted spread, spot and forward NPV errors and their arrays on fixed weights.

Each case's inputs hold its settings, the swap lengths, the indexes (swaptionvolcube.py's specs), the bid and ask
spreads (a row per length, a bid and an ask per index), the pricers ({"type": "analytic" | "numeric" | "tsr", "model",
"meanReversion"}), the volatility (a matrix spec of swaptionvolcube.py's), the weights, and "reprice": null or
{"volatility", "meanReversion"} (null for QuantLib's Null<Real>()). The wheel's market is a SWIG wrapper of the C++
class. Read back by tests/NanoQL.Tests/Vectors/TermStructures/CmsMarketVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_conundrumpricer", os.path.join(v.GEN_ROOT, "cashflows", "conundrumpricer.py"))
cp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cp)
svc, mat = cp.svc, cp.mat

LENGTHS = [[1, svc.Y], [5, svc.Y], [10, svc.Y], [20, svc.Y]]
INDEXES = [svc.euribor([2, svc.Y], cp.CURVE), svc.euribor([10, svc.Y], cp.CURVE)]
SPREADS = [[0.0010, 0.0012, 0.0020, 0.0024],
           [0.0012, 0.0014, 0.0022, 0.0026],
           [0.0014, 0.0016, 0.0024, 0.0028],
           [0.0016, 0.0018, 0.0026, 0.0030]]
WEIGHTS = [[1.0, 2.0], [1.0, 0.5], [2.0, 1.0], [1.0, 1.0]]
LOGNORMAL = svc.atm_floating()
NORMAL = svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N)


def make_pricer(spec, vol):
    if spec["type"] == "tsr":
        return ql.LinearTsrPricer(ql.SwaptionVolatilityStructureHandle(vol),
                                  ql.QuoteHandle(ql.SimpleQuote(spec["meanReversion"])))
    return cp.make_pricer(spec, vol)


def flat(m):
    return [m[i][j] for i in range(m.rows()) for j in range(m.columns())]


def market_outputs(market):
    w = ql.Matrix(len(WEIGHTS), len(WEIGHTS[0]))
    for i, row in enumerate(WEIGHTS):
        for j, x in enumerate(row):
            w[i][j] = x
    return {"browse": flat(market.browse()), "impliedCmsSpreads": flat(market.impliedCmsSpreads()),
            "spreadErrors": flat(market.spreadErrors()),
            "weightedSpreadError": market.weightedSpreadError(w),
            "weightedSpotNpvError": market.weightedSpotNpvError(w),
            "weightedFwdNpvError": market.weightedFwdNpvError(w),
            "weightedSpreadErrors": list(market.weightedSpreadErrors(w)),
            "weightedSpotNpvErrors": list(market.weightedSpotNpvErrors(w)),
            "weightedFwdNpvErrors": list(market.weightedFwdNpvErrors(w))}


def case(name, pricer_spec, vol_spec, reprice):
    st = v.settings(cp.TODAY)
    vol = cp.make_vol(vol_spec)
    indexes = [svc.make_index(s) for s in INDEXES]
    pricers = ql.CmsCouponPricerVector()
    for _ in indexes:
        pricers.append(make_pricer(pricer_spec, vol))
    quotes = [[ql.QuoteHandle(ql.SimpleQuote(x)) for x in row] for row in SPREADS]
    market = ql.CmsMarket([svc.period(p) for p in LENGTHS], indexes, indexes[0].iborIndex(), quotes, pricers,
                          indexes[0].forwardingTermStructure())
    if reprice is not None:
        m = ql.nullDouble() if reprice["meanReversion"] is None else reprice["meanReversion"]
        market.reprice(ql.SwaptionVolatilityStructureHandle(cp.make_vol(reprice["volatility"])), m)
    return {"name": name,
            "inputs": {"settings": st, "swapLengths": LENGTHS, "indexes": INDEXES, "bidAskSpreads": SPREADS,
                       "pricer": pricer_spec, "volatility": vol_spec, "weights": WEIGHTS, "reprice": reprice},
            "expected": market_outputs(market)}


def cases():
    analytic = {"type": "analytic", "model": "Standard", "meanReversion": 0.0}
    numeric = {"type": "numeric", "model": "NonParallelShifts", "meanReversion": 0.01}
    tsr = {"type": "tsr", "model": None, "meanReversion": 0.01}
    return [
        case("analytic Hagan, Standard", analytic, LOGNORMAL, None),
        case("numeric Hagan, NonParallelShifts at 1%", numeric, LOGNORMAL, None),
        case("linear TSR at 1%", tsr, LOGNORMAL, None),
        case("analytic Hagan, repriced on the normal matrix", analytic, LOGNORMAL,
             {"volatility": NORMAL, "meanReversion": None}),
        case("numeric Hagan, repriced at a reversion of 2%", numeric, LOGNORMAL,
             {"volatility": LOGNORMAL, "meanReversion": 0.02}),
    ]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/cmsmarket.hpp", cases)
