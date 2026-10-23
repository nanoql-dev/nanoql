# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's KahaleSmileSection (ql/termstructures/volatility/kahalesmilesection.hpp, .cpp), each output
computed by the wheel on sections built under an evaluation date of 15 January 2025. The sources are QuantLib's own
testKahaleSmileSection smiles, linearly interpolated over ten strikes at a 5% level (arbitrage free, its leftmost
point arbitrageable, its second-to-last point arbitrageable), with the test's settings and moneyness grid (each
strike over the level), and an unshifted SABR smile on the default grid. On each, the inspectors, and at twelve
strikes (within the grid, between its points and in both wings) the call and put prices, the volatility, the digital
call price and the density; and on the arbitrage-free smile and the SABR smile, the core forced to start and end at
given indices at the least and the most, the indices QuantLib's Markov-functional model forces.

**The stability filter** (the S7 plan's S7R6) runs on every output: each section is fitted again with every nonzero
real of the source's spec moved one ulp up, then down (models/model.py's moved); an output is written only where both
fits leave it within models/model.py's measure, and the generator prints the outputs dropped per case.

Left out, where nanoql's section differs (the S-g1d plan's G1R6 (viii), (xv), (xvii), and the run's at-the-money
level): shifted sources, and interpolated fits on which QuantLib's erase runs past its functions' end. The sources'
specs are tools/vectors/gen/termstructures/volatility/interpolatedsmilesection.py's and sabrsmilesection.py's; each
case's input "kahale" is {"source", "atm", "interpolate", "exponentialExtrapolation", "deleteArbitragePoints",
"moneynessGrid", "gap", "forcedLeftIndex", "forcedRightIndex"}. tests/NanoQL.Tests/Vectors/TermStructures/
KahaleSmileSectionVectorTests.cs reads it back."""
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

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


mdl = _load("wheel_model", "models/model.py")
iss = _load("wheel_interpolatedsmilesection", "termstructures/volatility/interpolatedsmilesection.py")
sss = _load("wheel_sabrsmilesection", "termstructures/volatility/sabrsmilesection.py")

TODAY = ql.Date(15, ql.January, 2025)
ATM, T = 0.05, 1.0
STRIKES = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09, 0.10]
MONEY = [k / ATM for k in STRIKES]
A365 = iss.A365
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)


def implied(calls):
    return [ql.blackFormulaImpliedStdDev(ql.Option.Call, k, ATM, c, 1.0, 0.0, 0.2, 1E-8, 1000)
            for k, c in zip(STRIKES, calls)]


def sources():
    calls0 = [ql.blackFormula(ql.Option.Call, k, ATM, 0.50 * math.sqrt(T), 1.0, 0.0) for k in STRIKES]
    calls1 = list(calls0)
    calls1[0] = (ATM - STRIKES[0]) + 0.0010
    calls2 = list(calls0)
    calls2[8] = 0.9 * calls2[9] + 0.1 * calls2[8]

    def linear(calls):
        return iss.section("LinearInterpolatedSmileSection", "time", T, STRIKES, implied(calls), ATM, A365)

    sabr = sss.time_spec(5.0, 0.03, [0.03, 0.6, 0.4, -0.3])
    # (name, spec, grid, settings as (interpolate, exponentialExtrapolation, deleteArbitragePoints[, forced indices]))
    return [
        ("arbitrage free", linear(calls0), MONEY, [(False, False, False), (True, False, False), (False, True, False),
                                                   (False, False, False, (3, 7)), (True, False, False, (3, 7))]),
        ("leftmost point arbitrageable", linear(calls1), MONEY, [(False, False, False), (True, False, True)]),
        ("second-to-last point arbitrageable", linear(calls2), MONEY, [(False, False, False), (True, False, True)]),
        ("SABR", sabr, [], [(False, False, False), (False, True, False), (False, False, False, (5, 15))]),
    ]


def make_source(spec):
    if spec["type"] == "SabrSmileSection":
        return sss.make_section(spec)
    return iss.make_section(spec)


def strikes(spec):
    level = spec.get("atmLevel") or spec.get("forward")
    return [level * m for m in (0.05, 0.15, 0.2, 0.35, 0.6, 0.9, 1.0, 1.1, 1.5, 2.0, 2.5, 6.0)]


def outputs(kahale, ks):
    with v.case_settings(TODAY) as st:
        source = make_source(kahale["source"])
        s = ql.KahaleSmileSection(source, ql.nullDouble() if kahale["atm"] is None else kahale["atm"],
                                  kahale["interpolate"], kahale["exponentialExtrapolation"],
                                  kahale["deleteArbitragePoints"], kahale["moneynessGrid"], kahale["gap"],
                                  kahale["forcedLeftIndex"], kahale["forcedRightIndex"])
        out = {"minStrike": s.minStrike(), "maxStrike": s.maxStrike(), "atmLevel": s.atmLevel(),
               "exerciseTime": s.exerciseTime()}
        for i, k in enumerate(ks):
            out[f"call[{i}]"] = v.attempt(lambda k=k: s.optionPrice(k, ql.Option.Call, 1.0))
            out[f"put[{i}]"] = v.attempt(lambda k=k: s.optionPrice(k, ql.Option.Put, 1.0))
            out[f"volatility[{i}]"] = v.attempt(lambda k=k: s.volatility(k))
            out[f"digital[{i}]"] = v.attempt(lambda k=k: s.digitalOptionPrice(k, ql.Option.Call, 1.0, 1.0e-5))
            out[f"density[{i}]"] = v.attempt(lambda k=k: s.density(k, 1.0, 1.0e-4))
        return st, out


def stable(a, b):
    if isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return mdl.stable(a, b)


def cases():
    out = []
    for name, source, grid, settings in sources():
        ks = strikes(source)
        for interpolate, exponential, delete, *forced in settings:
            left, right = forced[0] if forced else (-1, 2147483647)
            kahale = {"source": source, "atm": None, "interpolate": interpolate,
                      "exponentialExtrapolation": exponential, "deleteArbitragePoints": delete,
                      "moneynessGrid": grid, "gap": 1.0e-5, "forcedLeftIndex": left, "forcedRightIndex": right}
            st, base = outputs(kahale, ks)
            refits = [outputs({**kahale, "source": mdl.moved(copy.deepcopy(source), d)}, ks)[1]
                      for d in (math.inf, -math.inf)]
            kept = {k: x for k, x in base.items() if all(stable(x, r[k]) for r in refits)}
            label = f"{name}, {'interpolated' if interpolate else 'exponential' if exponential else 'plain'}" + (
                ", deleting" if delete else "") + (f", forced to [{left}, {right}]" if forced else "")
            print(f"{label}: dropped {len(base) - len(kept)} of {len(base)} outputs")
            out.append({"name": label, "inputs": {"settings": st, "kahale": kahale, "strikes": ks},
                        "expected": v.enc(kept)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/kahalesmilesection.hpp", cases)
