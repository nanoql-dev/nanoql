# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dSwaptionVolatility (ql/termstructures/volatility/swaption/
gaussian1dswaptionvolatility.hpp, .cpp), which the wheel doesn't bind, each output computed by the C++ harness on
structures built inside the call, on the evaluation date 15 January 2025: on TARGET and Modified Following, a
EuriborSwapIsdaFixA 10-year base with curves attached (forecast on a flat 3.1%, discounted on the 2.7%) or none,
pricingengines/swaption/gaussian1dswaptionengine.py's three GSR models on its 2.7% curve, the default engine or a
Gaussian1dSwaptionEngine of 32 points, and Actual/365 (Fixed) or Actual/360. Each gives the reference and maximum dates,
the strike range and the maximum swap tenor; the volatility by option tenor and swap tenor, by option date and swap
tenor, and by option time and swap length, at two strikes; and the smile sections by date and tenor and by time and
length (the latter's date found by the structure's Newton solve), their exercise date and time and their level.

**The stability filter** (the S7 plan's S7R6) runs on the volatilities, each implied from a price by
blackFormulaImpliedStdDev's solve: each structure is rebuilt with every model volatility and reversion and every curve's
rate moved one ulp up, then down, by math.nextafter, together; then each alone, up and down; a volatility is written
only where every rebuild leaves it within 1e-12 x max(|base|, |moved|, 1), a query's whole array otherwise left out, and
the generator prints "dropped n of m outputs" per case. The prices go through the C runtime's erf (the S-g1d plan's
G1R3), so the levels' last bits differ, within R7. A refused value is {"throws": true}. Each case's input "volatility"
is the spec tools/cxx/src/termstructures/volatility/swaption/gaussian1dswaptionvolatility.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/Gaussian1dSwaptionVolatilityCxxVectorTests.cs reads it back."""
import copy
import importlib.util
import math
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


g1d = _load("wheel_gaussian1dswaptionengine", "pricingengines/swaption/gaussian1dswaptionengine.py")
fvf = g1d.fvf
TODAY = g1d.TODAY
CAL = ql.TARGET()
Y, M = int(ql.Years), int(ql.Months)
RELATIVE = 1e-12
STRIKES = [0.025, 0.035]
OPTION_TENORS = [[1, Y], [2, Y], [5, Y]]
SWAP_TENORS = [[5, Y], [10, Y]]
OPTION_DATES = [CAL.advance(TODAY, 18, ql.Months), CAL.advance(TODAY, 3, ql.Years)]
OPTION_TIMES = [1.0, 2.5, 4.75]
SWAP_LENGTHS = [5.0, 7.5]
A365, A360 = {"type": "Actual365Fixed"}, {"type": "Actual360"}


def structure(model, curves=True, engine=None, dc=A365):
    return {"calendar": {"type": "TARGET"}, "convention": int(ql.ModifiedFollowing),
            "swapIndex": {"type": "EuriborSwapIsdaFixA", "tenor": [10, Y],
                          "forwarding": fvf.curve(0.031) if curves else None,
                          "discounting": g1d.DISCOUNT if curves else None},
            "model": copy.deepcopy(model), "dayCounter": dc, "engine": engine}


def specs():
    m0, m1, m2 = g1d.MODELS
    return [
        ("curves attached, a constant model", structure(m0)),
        ("curves attached, stepped volatilities, an engine of 32 points", structure(m1, engine={
            "integrationPoints": 32, "stddevs": 5.0, "extrapolatePayoff": True, "discountCurve": None})),
        ("the model's curve, Actual/360", structure(m2, curves=False, dc=A360)),
    ]


def moves(spec):
    """The spec with every model volatility and reversion and every curve's rate moved one ulp: all up, all down, then
    each alone up and down."""
    paths = [("model", "volatilities", i) for i in range(len(spec["model"]["volatilities"]))]
    paths += [("model", "reversions", i) for i in range(len(spec["model"]["reversions"]))]
    paths.append(("model", "curve", "forward"))
    paths += [("swapIndex", name, "forward") for name in ("forwarding", "discounting")
              if spec["swapIndex"][name] is not None]

    def moved(chosen, d):
        m = copy.deepcopy(spec)
        for a, b, c in chosen:
            m[a][b][c] = math.nextafter(m[a][b][c], d)
        return m

    out = [moved(paths, d) for d in (math.inf, -math.inf)]
    for p in paths:
        out += [moved([p], d) for d in (math.inf, -math.inf)]
    return out


def within(a, b):
    if isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return a == b or (math.isnan(a) and math.isnan(b)) or abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def cases():
    with cxx.connect() as H:
        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        queries = [("volatility(Period,Period)", [(o, s) for o in OPTION_TENORS for s in SWAP_TENORS]),
                   ("volatility(Date,Period)", [(v.enc(d), s) for d in OPTION_DATES for s in SWAP_TENORS]),
                   ("volatility(Time,Time)", [(t, l) for t in OPTION_TIMES for l in SWAP_LENGTHS])]
        out = []
        dropped_all = total_all = 0
        with H.case_settings(v.enc(TODAY)) as st:
            for name, spec in specs():
                expected = H.call("Gaussian1dSwaptionVolatility::inspectors", spec)
                specs_moved = moves(spec)
                dropped = total = 0
                for method, points in queries:
                    values = [[call("Gaussian1dSwaptionVolatility::" + method, spec, o, s, k) for k in STRIKES]
                              for o, s in points]
                    stable = all(within(values[i][j], call("Gaussian1dSwaptionVolatility::" + method, m, o, s, k))
                                 for m in specs_moved for i, (o, s) in enumerate(points)
                                 for j, k in enumerate(STRIKES))
                    total += len(points) * len(STRIKES)
                    if stable:
                        expected[method] = values
                    else:
                        dropped += len(points) * len(STRIKES)
                for method, points in (("smileSection(Date,Period)", [(v.enc(d), s) for d in OPTION_DATES
                                                                      for s in SWAP_TENORS]),
                                       ("smileSection(Time,Time)", [(t, l) for t in OPTION_TIMES
                                                                    for l in SWAP_LENGTHS])):
                    sections = [H.call("Gaussian1dSwaptionVolatility::" + method, spec, o, s) for o, s in points]
                    for field in ("exerciseDate", "exerciseTime", "atmLevel"):
                        expected[f"{method}.{field}"] = [x[field] for x in sections]
                dropped_all += dropped
                total_all += total
                print(f"{name}: dropped {dropped} of {total} outputs")
                out.append({"name": name,
                            "inputs": {"settings": st, "volatility": spec, "strikes": STRIKES,
                                       "optionTenors": OPTION_TENORS, "swapTenors": SWAP_TENORS,
                                       "optionDates": v.enc(OPTION_DATES), "optionTimes": OPTION_TIMES,
                                       "swapLengths": SWAP_LENGTHS},
                            "expected": expected})
        print(f"file: dropped {dropped_all} of {total_all} outputs")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/gaussian1dswaptionvolatility.hpp", cases)
