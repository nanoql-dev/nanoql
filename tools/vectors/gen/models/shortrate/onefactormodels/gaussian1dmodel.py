# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dModel (ql/models/shortrate/onefactormodels/gaussian1dmodel.hpp, .cpp), through
the GSR models of pricingengines/swaption/gaussian1dswaptionengine.py, on the evaluation date 15 January 2025.

For each of the three models: the zero bond at six (T, t, y) times and six (maturity, reference date, y) dates, each
also on a given curve; the numeraire at four times and four dates, each also on a given curve; the forward rate of
Euribor 6M at five (fixing, reference date, y) points; the swap rate and the annuity of four swap indexes (Euribor 5Y
on its index's curve, Euribor 10Y with an exogenous discounting curve, an Euribor 2Y on the model's curve, and an
ESTR overnight-indexed 3Y swap index) at four (fixing, tenor, reference date, y) points; the compounded rate of three
ESTR coupons of a swap (a plain one, one with a 5-day lookback under the observation shift, one starting before the
reference date) at three (reference date, y) points, also on a given curve; and the zero-bond option at six
(type, expiry, value date, maturity, strike, reference date, y, grid) points, also on a given curve. The given curve is
a flat 2.5%; every curve is on Actual/365 (Fixed) from the evaluation date, the model's clock, and every fixing is
after it.

Left out, each a documented divergence: a curve on another clock (the S-g1d plan's G1R6 (xvi),
tests/NanoQL.Tests/Divergences/Gaussian1dMismatchedTimesTests.cs); flat payoff extrapolation (G1R6 (xxi)); an index
that overrides its maturity date, whose period QuantLib 1.44 ends by the plain advance (#2848,
Gaussian1dForwardRateEndDateTests.cs); a fixing on or before the evaluation date, which reads the context's fixings,
Gaussian1dModelContextTests.cs's.

The wheel's calls, by QuantLib-SWIG's gaussian1dmodel.i: zerobond, numeraire, forwardRate, swapRate, swapAnnuity,
compoundedRate and zerobondOption, each forwarding to the C++ member of that name and overload. Each case's inputs
hold its settings, the model (the engine generator's spec), the given curve "yts", the indexes and the points. Read
back by tests/NanoQL.Tests/Vectors/Models/Gaussian1dModelVectorTests.cs."""
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


g1d = _load("wheel_gaussian1dswaptionengine", "pricingengines/swaption/gaussian1dswaptionengine.py")
six = _load("wheel_swapindex", "indexes/swapindex.py")
swp, fvf, irx = g1d.swp, g1d.fvf, g1d.irx

TODAY = g1d.TODAY
CAL = g1d.TARGET_CAL
D, M, Y = ql.Days, ql.Months, ql.Years
YTS = g1d.OTHER_DISCOUNT
E6M = swp.E6M
ESTR = swp.ESTR


def d(n, unit):
    return CAL.advance(TODAY, n, unit)


SWAP_INDEXES = [
    six.swap_index("EuriborSwap", (5, Y), 2, "EUR", irx.TARGET, (1, Y), ql.ModifiedFollowing, swp.THIRTY360_BB, E6M),
    six.swap_index("EuriborSwap", (10, Y), 2, "EUR", irx.TARGET, (1, Y), ql.ModifiedFollowing, swp.THIRTY360_BB, E6M,
                   True, swp.DISCOUNT),
    six.swap_index("EuriborSwap", (2, Y), 2, "EUR", irx.TARGET, (1, Y), ql.ModifiedFollowing, swp.THIRTY360_BB,
                   fvf.ibc.euribor((6, M), None)),
    six.ois_index("EstrSwap", (3, Y), 2, "EUR", ESTR),
]

ZB_TIMES = [(1.0, 0.0, 0.0), (5.0, 1.0, 0.5), (10.0, 2.0, -1.0), (30.0, 5.0, 2.0), (2.0, 2.0, 0.3), (15.0, 10.0, -2.5)]
ZB_DATES = [(d(1, Y), None, 0.0), (d(5, Y), d(1, Y), 0.5), (d(10, Y), d(2, Y), -1.0), (d(30, Y), d(5, Y), 2.0),
            (d(3, Y), d(3, Y), 0.3), (d(6, M), d(1, M), -1.7)]
NUM_TIMES = [(0.0, 0.0), (1.0, 0.5), (5.0, -1.0), (20.0, 1.5)]
NUM_DATES = [(TODAY, 0.0), (d(1, Y), 0.5), (d(5, Y), -1.0), (d(20, Y), 1.5)]
FWD_POINTS = [(d(1, Y), None, 0.0), (d(2, Y), d(1, Y), 0.8), (d(5, Y), d(5, Y), -1.2), (d(3, M), d(1, M), 2.0),
              (d(10, Y), d(4, Y), -0.4)]
SWAP_POINTS = [(d(1, Y), (5, Y), None, 0.0), (d(2, Y), (10, Y), d(1, Y), 0.7), (d(5, Y), (2, Y), d(5, Y), -1.1),
               (d(3, Y), (3, Y), d(2, Y), 1.6)]
COMPOUNDED_POINTS = [(None, 0.0), (d(1, Y), 0.9), (d(2, Y), -1.3)]
ZBO_POINTS = [(ql.Option.Call, d(1, Y), d(1, Y) + 2, d(5, Y), 0.88, None, 0.0, 7.0, 64, True),
              (ql.Option.Put, d(2, Y), d(2, Y) + 2, d(7, Y), 0.86, None, 0.0, 7.0, 64, True),
              (ql.Option.Call, d(5, Y), d(5, Y), d(10, Y), 0.9, d(1, Y), 0.6, 5.0, 32, True),
              (ql.Option.Put, d(3, Y), d(3, Y), d(4, Y), 0.97, d(1, Y), -1.0, 7.0, 64, False),
              (ql.Option.Call, d(1, Y), d(1, Y), d(2, Y), 0.95, None, 0.0, 6.0, 48, False),
              (ql.Option.Put, d(10, Y), d(10, Y) + 2, d(20, Y), 0.7, d(5, Y), 1.2, 7.0, 64, True)]


def coupon_swap():
    """An ESTR swap from a year before the evaluation date's second year: its coupons are plain, with a lookback under
    the shift, and starting before the reference dates."""
    start = d(1, Y)
    s = fvf.sched(start, start + ql.Period(3, Y), ql.Period(1, Y), irx.TARGET, ql.ModifiedFollowing)
    return [fvf.ois("nominal", swp.PAYER, [1.0], s, 0.03, swp.A360, ESTR),
            fvf.ois("nominal", swp.PAYER, [1.0], s, 0.03, swp.A360, ESTR, lookback=5, shift=True,
                    payment_calendar=irx.TARGET)]


COUPONS = [(0, 0), (1, 1), (0, 2)]  # (swap, coupon)


def optional(x):
    return ql.Date() if x is None else x


def outputs(model, swaps):
    yts = irx.handle(YTS)
    index = fvf.make_index(E6M)
    swap_indexes = [six.make_swap_index(s) for s in SWAP_INDEXES]
    coupons = [ql.as_overnight_indexed_coupon(swaps[i].overnightLeg()[k]) for i, k in COUPONS]
    out = {"zerobondTime": [model.zerobond(*p) for p in ZB_TIMES],
           "zerobondTimeOnCurve": [model.zerobond(*p, yts) for p in ZB_TIMES],
           "zerobondDate": [model.zerobond(m, optional(r), y) for m, r, y in ZB_DATES],
           "zerobondDateOnCurve": [model.zerobond(m, optional(r), y, yts) for m, r, y in ZB_DATES],
           "numeraireTime": [model.numeraire(*p) for p in NUM_TIMES],
           "numeraireTimeOnCurve": [model.numeraire(*p, yts) for p in NUM_TIMES],
           "numeraireDate": [model.numeraire(*p) for p in NUM_DATES],
           "numeraireDateOnCurve": [model.numeraire(*p, yts) for p in NUM_DATES],
           "forwardRate": [model.forwardRate(f, optional(r), y, index) for f, r, y in FWD_POINTS]}
    out["swapRate"] = [[model.swapRate(f, ql.Period(*t), optional(r), y, s) for f, t, r, y in SWAP_POINTS]
                       for s in swap_indexes]
    out["swapAnnuity"] = [[model.swapAnnuity(f, ql.Period(*t), optional(r), y, s) for f, t, r, y in SWAP_POINTS]
                          for s in swap_indexes]
    out["compoundedRate"] = [[model.compoundedRate(c, optional(r), y) for r, y in COMPOUNDED_POINTS] for c in coupons]
    out["compoundedRateOnCurve"] = [[model.compoundedRate(c, optional(r), y, yts) for r, y in COMPOUNDED_POINTS]
                                    for c in coupons]
    out["zerobondOption"] = [model.zerobondOption(t, e, vd, m, k, optional(r), y, ql.YieldTermStructureHandle(),
                                                  sd, n, x, False) for t, e, vd, m, k, r, y, sd, n, x in ZBO_POINTS]
    out["zerobondOptionOnCurve"] = [model.zerobondOption(t, e, vd, m, k, optional(r), y, yts, sd, n, x, False)
                                    for t, e, vd, m, k, r, y, sd, n, x in ZBO_POINTS]
    return out


def cases():
    out = []
    swap_specs = coupon_swap()
    for i, spec in enumerate(g1d.MODELS):
        with v.case_settings(TODAY) as st:
            swaps = [fvf.make_swap(s) for s in swap_specs]
            expected = outputs(g1d.make_gsr(spec), swaps)
            out.append({"name": f"model {i}",
                        "inputs": {"settings": st, "model": spec, "yts": YTS, "iborIndex": E6M,
                                   "swapIndexes": SWAP_INDEXES, "couponSwaps": swap_specs, "coupons": COUPONS,
                                   "zerobondTimes": ZB_TIMES, "zerobondDates": v.enc(ZB_DATES),
                                   "numeraireTimes": NUM_TIMES, "numeraireDates": v.enc(NUM_DATES),
                                   "forwardPoints": v.enc(FWD_POINTS),
                                   "swapPoints": v.enc([[f, list(t), r, y] for f, t, r, y in SWAP_POINTS]),
                                   "compoundedPoints": v.enc(COMPOUNDED_POINTS),
                                   "zerobondOptionPoints": v.enc([[int(t), e, vd, m, k, r, y, sd, n, x]
                                                                  for t, e, vd, m, k, r, y, sd, n, x in ZBO_POINTS])},
                        "expected": v.enc(expected)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/gaussian1dmodel.hpp", cases)
