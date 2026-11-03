# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dSmileSection (ql/termstructures/volatility/gaussian1dsmilesection.hpp, .cpp), which
the wheel doesn't bind, each output computed by the C++ harness on sections built inside the call, on the evaluation
date 15 January 2025: on a EuriborSwapIsdaFixA swap index, sections of
pricingengines/swaption/gaussian1dswaptionengine.py's three GSR models on its 2.7% curve, fixing in a week, one, two and
five years, on 5- and 10-year swaps, with curves attached to the index (forecast on a flat 3.1%, discounted on the 2.7%)
or none, the model's curve then; the default engine and a Gaussian1dSwaptionEngine of 32 points and 5 standard
deviations without payoff extrapolation; and Actual/365 (Fixed) or Actual/360 for the exercise time. Each gives the
at-the-money level, the exercise date and time, the reference date and the strike range, and at eight strikes around the
level the call and put prices, with a discount of 1 and of 0.97, and the volatility.

**The stability filter** runs on the volatilities, each implied from a price by blackFormulaImpliedStdDev's solve: each
section is rebuilt with every model volatility and reversion and every curve's rate moved one ulp up, then down, by
math.nextafter, together; then each alone, up and down; a volatility is written only where every rebuild leaves it
within 1e-12 x max(|base|, |moved|, 1), its strike's other outputs kept, and the generator prints "dropped n of m
outputs" per case. The prices go through the C runtime's erf, so their last bits differ, within the tolerance.

Left out, each a documented divergence: the Ibor index's sections, whose constructor QuantLib refuses for every index
(its MakeCapFloor without a strike asks for a Black or Bachelier engine) and whose caplet starts on the fixing date
(tests/NanoQL.Tests/Divergences/Gaussian1dSmileSectionCapletTests.cs). A refused value is {"throws": true}. Each case's
input "section" is the spec tools/cxx/src/termstructures/volatility/gaussian1dsmilesection.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/Gaussian1dSmileSectionCxxVectorTests.cs reads it back."""
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
RELATIVE = 1e-12
MONEYNESS = [0.5, 0.8, 0.95, 1.0, 1.05, 1.25, 1.6, 2.0]
DISCOUNTS = [1.0, 0.97]
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
A365, A360 = {"type": "Actual365Fixed"}, {"type": "Actual360"}


def swap_index(years, curves=True):
    return {"type": "EuriborSwapIsdaFixA", "tenor": [years, int(ql.Years)],
            "forwarding": fvf.curve(0.031) if curves else None, "discounting": g1d.DISCOUNT if curves else None}


def section(fixing, model, index, engine=None, dc=A365):
    return {"fixingDate": v.enc(fixing), "model": copy.deepcopy(model), "dayCounter": dc, "swapIndex": index,
            "engine": engine}


def specs():
    m0, m1, m2 = g1d.MODELS
    in_1y = CAL.advance(TODAY, 1, ql.Years)
    return [
        ("1Y into 5Y, curves attached", section(in_1y, m0, swap_index(5))),
        ("5Y into 10Y, curves attached, stepped volatilities", section(CAL.advance(TODAY, 5, ql.Years), m1,
                                                                       swap_index(10))),
        ("1Y into 5Y, the model's curve", section(in_1y, m2, swap_index(5, curves=False))),
        ("2Y into 5Y, an engine of 32 points", section(CAL.advance(TODAY, 2, ql.Years), m0, swap_index(5), engine={
            "integrationPoints": 32, "stddevs": 5.0, "extrapolatePayoff": False, "discountCurve": None})),
        ("1Y into 10Y, Actual/360", section(in_1y, m1, swap_index(10), dc=A360)),
        ("a week into 5Y", section(CAL.advance(TODAY, 1, ql.Weeks), m0, swap_index(5))),
    ]


def moves(spec):
    """The spec with every model volatility and reversion and every curve's rate moved one ulp: all up, all down, then
    each alone up and down."""
    def paths(s):
        out = [("model", "volatilities", i) for i in range(len(s["model"]["volatilities"]))]
        out += [("model", "reversions", i) for i in range(len(s["model"]["reversions"]))]
        out.append(("model", "curve", "forward"))
        for name in ("forwarding", "discounting"):
            if s["swapIndex"][name] is not None:
                out.append(("swapIndex", name, "forward"))
        return out

    def moved(s, chosen, d):
        m = copy.deepcopy(s)
        for a, b, c in chosen:
            m[a][b][c] = math.nextafter(m[a][b][c], d)
        return m

    ps = paths(spec)
    out = [moved(spec, ps, d) for d in (math.inf, -math.inf)]
    for p in ps:
        out += [moved(spec, [p], d) for d in (math.inf, -math.inf)]
    return out


def within(a, b):
    if isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return a == b or (math.isnan(a) and math.isnan(b)) or abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def cases():
    with cxx.connect() as H:
        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        dropped_all = total_all = 0
        with H.case_settings(v.enc(TODAY)) as st:
            for name, spec in specs():
                inspectors = H.call("Gaussian1dSmileSection::inspectors", spec)
                ks = [inspectors["atmLevel"] * m for m in MONEYNESS]
                vols = [call("Gaussian1dSmileSection::volatility", spec, k) for k in ks]
                keep = list(range(len(ks)))
                for m in moves(spec):
                    if not keep:
                        break
                    keep = [i for i in keep if within(vols[i], call("Gaussian1dSmileSection::volatility", m, ks[i]))]
                dropped = len(ks) - len(keep)
                dropped_all += dropped
                total_all += len(ks)
                print(f"{name}: dropped {dropped} of {len(ks)} outputs")
                kept = [ks[i] for i in keep]
                expected = dict(inspectors)
                for label, t in (("callPrice", CALL), ("putPrice", PUT)):
                    expected[label] = [[call("Gaussian1dSmileSection::optionPrice", spec, k, t, d) for k in kept]
                                       for d in DISCOUNTS]
                expected["volatility"] = [vols[i] for i in keep]
                out.append({"name": name,
                            "inputs": {"settings": st, "section": spec, "strikes": kept, "discounts": DISCOUNTS},
                            "expected": expected})
        print(f"file: dropped {dropped_all} of {total_all} outputs")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/gaussian1dsmilesection.hpp", cases)
