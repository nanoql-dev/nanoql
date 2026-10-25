# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeFdHestonVanillaEngine and FdHestonVanillaEngine's multiple-strikes caching
(ql/pricingengines/vanilla/fdhestonvanillaengine.hpp, .cpp), which the wheel doesn't bind, each output computed by the
C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

- The builder: the NPV, delta, gamma and theta of a plain vanilla option priced on the engine MakeFdHestonVanillaEngine
  makes on tools/vectors/gen/cxx/models/equity/batesmodel.py's first Heston process, at QuantLib's defaults and after
  each setter in turn (the grids, damping steps, a scheme description, a constant leverage function, a quanto helper,
  cash dividends), all of them together, and a setter called twice.
- The multiple-strikes cache: an engine with enableMultipleStrikesCaching(strikes), a reference option priced on it,
  then an option at each strike, of the reference's type and exercise, priced from the cache: each one's NPV, delta,
  gamma and theta. European calls and puts in turn, the reference strike at the spot and among the strikes, and away
  from the spot and not among them; and an American put.
- The widened grid (the S-hes C1 review's B1-1, tests/NanoQL.Tests/Divergences/FdHestonHighCarryTests.cs): calls and
  puts at 0.8, 1 and 1.25 times the forward on five markets of volatility 0.1 and carry from 0.05, where QuantLib's
  grid holds the true forwards' band, to 0.30, where it doesn't; an American put and a call with cash dividends at
  carry 0.30; each against QuantLib's solve assembled from its own components under the rule (the harness's
  widenedResults: QuantLib's mesher, given its constraints at the band where its range falls short). Calls and puts
  at 0.8, 1 and 1.25 times the dividend forward where five cash dividends take QuantLib's swapped walk below zero and
  its bound isn't a number (the review's B2-2). And the multi-strike solve where its grid falls short, at carry 0.30
  and with the reference strike at twice the spot (B2-1), against each strike's widened solve. And each strike's quanto
  solve where the foreign rate sits far from the domestic one (B3-1), whose strikes the multi-strike solve takes alone
  (tests/NanoQL.Tests/Divergences/FdHestonHighCarryTests.cs), against QuantLib's engine.

Left out (the S-hes plan's §2.10): the multiple-strikes cache with a leverage function, whose strikes nanoql solves one
by one (HR7 (i), tests/NanoQL.Tests/Divergences/FdHestonMultipleStrikesLeverageTests.cs), and on a non-plain reference
(HR7 (xxiv), tests/NanoQL.Tests/Divergences/FdHestonStrikesReferenceTests.cs), and where its grid falls short of the
true forwards' band, whose strikes nanoql solves one by one too, each on its widened grid; every structure on the
process's reference date and day counter.

A builder case holds the "process" (batesmodel.py's harness spec), the "optionType", "strike" and "exercise"
(["European" | "American", date]) and the "setters" ([name, ...] as tools/cxx/src/pricingengines/vanilla/
fdhestonvanillaengine.cpp takes them); a multiple-strikes case the "process", "optionType", "referenceStrike",
"exercise", "strikes", "tGrid", "xGrid", "vGrid", "dampingSteps" and "scheme" ([type, theta, mu]); a widened case
the "process", "optionType", "strike", "exercise", "dividendDates", "dividendAmounts", the grids, "dampingSteps" and
"scheme". The harness's calls: MakeFdHestonVanillaEngine::results, FdHestonVanillaEngine::multipleStrikes and
FdHestonVanillaEngine::widenedResults. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/FdHestonVanillaEngineCxxVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bm = _load("cxx_batesmodel", "cxx/models/equity/batesmodel.py")
R = bm.R
KEYS = ["NPV", "delta", "gamma", "theta"]
CALL, PUT = 1, -1
# QuantLib's FdmSchemeDesc types: Hundsdorfer 0, Douglas 1 (theta 0.5, mu 0)
HUNDSDORFER = [0, 0.5 + 3.0 ** 0.5 / 6.0, 0.5]
DOUGLAS = [1, 0.5, 0.0]
QUANTO = {"foreign": 0.06, "fxVolatility": 0.12, "correlation": -0.75, "atmLevel": 1.0}
GRIDS = [["withTGrid", 40], ["withXGrid", 50], ["withVGrid", 25]]
# B1-1's markets: v0 = theta 0.01, kappa 1.5, sigma 0.1, rho -0.5; carry from 0.05 (QuantLib's grid holds) to 0.30
LOW_VOL = ("low volatility", 100.0, 0.01, 1.5, 0.01, 0.1, -0.5)
HIGH_CARRY = [(5, 0.31, 0.01), (10, 0.21, 0.01), (10, 0.01, 0.21), (2, 0.31, 0.01), (5, 0.06, 0.01)]
DIVIDENDS = ["withCashDividends", [R + 91, R + 273], [2.0, 3.0]]


def cases():
    out = []
    process = bm.process_spec(bm.HESTON[0])
    european, american = ["European", R + 365], ["American", R + 365]
    with cxx.connect() as H:
        st = H.settings(R)

        def builder(name, option_type, strike, exercise, setters):
            r = v.attempt(lambda: H.call("MakeFdHestonVanillaEngine::results", process, option_type, strike,
                                         exercise, setters))
            out.append({"name": f"builder, {name}",
                        "inputs": {"settings": st, "kind": "builder", "process": process, "optionType": option_type,
                                   "strike": strike, "exercise": exercise, "setters": setters},
                        "expected": {k: r if "throws" in r else v.enc(r[k]) for k in KEYS}})

        def strikes_case(name, option_type, reference, exercise, strikes):
            r = H.call("FdHestonVanillaEngine::multipleStrikes", process, option_type, reference, exercise, strikes,
                       40, 50, 25, 0, HUNDSDORFER)
            out.append({"name": f"multiple strikes, {name}",
                        "inputs": {"settings": st, "kind": "multipleStrikes", "process": process,
                                   "optionType": option_type, "referenceStrike": reference, "exercise": exercise,
                                   "strikes": strikes, "tGrid": 40, "xGrid": 50, "vGrid": 25, "dampingSteps": 0,
                                   "scheme": HUNDSDORFER},
                        "expected": {k: [v.enc(s[k]) for s in r["strikes"]] for k in KEYS}})

        builder("defaults, European put", PUT, 100.0, european, [])
        builder("grids, American put", PUT, 100.0, american, GRIDS)
        builder("damping steps, American put", PUT, 100.0, american, GRIDS + [["withDampingSteps", 2]])
        builder("Douglas, American put", PUT, 100.0, american, GRIDS + [["withFdmSchemeDesc"] + DOUGLAS])
        builder("leverage, European call", CALL, 110.0, european, GRIDS + [["withLeverageFunction", 0.9]])
        builder("quanto, American put", PUT, 100.0, american, GRIDS + [["withQuantoHelper", QUANTO]])
        builder("dividends, American call", CALL, 100.0, american, GRIDS + [DIVIDENDS])
        builder("every setter, American put", PUT, 105.0, american,
                GRIDS + [["withDampingSteps", 1], ["withFdmSchemeDesc"] + DOUGLAS, ["withLeverageFunction", 1.1],
                         ["withQuantoHelper", QUANTO], DIVIDENDS])
        builder("a grid set twice, European put", PUT, 100.0, european,
                [["withTGrid", 20]] + GRIDS[1:] + [["withTGrid", 40]])
        listed, away = [80.0, 90.0, 100.0, 110.0, 120.0], [80.0, 90.0, 100.0, 110.0]
        for option_type, tname in ((CALL, "calls"), (PUT, "puts")):
            strikes_case(f"{tname}, reference at the spot", option_type, 100.0, european, listed)
            strikes_case(f"{tname}, reference away from the spot", option_type, 120.0, european, away)
        strikes_case("American puts, reference at the spot", PUT, 100.0, american, listed)

        def widened(name, process, option_type, strike, exercise, dividends=((), ())):
            r = H.call("FdHestonVanillaEngine::widenedResults", process, option_type, strike, exercise,
                       list(dividends[0]), list(dividends[1]), 100, 100, 50, 0, HUNDSDORFER)
            out.append({"name": f"widened grid, {name}" + (", widened" if r["widened"] else ", QuantLib's grid"),
                        "inputs": {"settings": st, "kind": "widened", "process": process, "optionType": option_type,
                                   "strike": strike, "exercise": exercise, "dividendDates": list(dividends[0]),
                                   "dividendAmounts": list(dividends[1]), "tGrid": 100, "xGrid": 100, "vGrid": 50,
                                   "dampingSteps": 0, "scheme": HUNDSDORFER},
                        "expected": {k: v.enc(r[k]) for k in KEYS}})
            return r

        for years, rf, q in HIGH_CARRY:
            process = bm.process_spec(LOW_VOL, rf=rf, q=q)
            fwd = 100.0 * math.exp((rf - q) * years)
            for k in (0.8, 1.0, 1.25):
                strike = round(k * fwd, 6)
                for option_type, tname in ((CALL, "call"), (PUT, "put")):
                    widened(f"{years} y, r {rf}, q {q}, {tname} at {k}F", process, option_type, strike,
                            ["European", R + 365 * years])
        process = bm.process_spec(LOW_VOL, rf=0.31, q=0.01)
        fwd = 100.0 * math.exp(0.30 * 5)
        widened("5 y, r 0.31, q 0.01, American put at 1.25F", process, PUT, round(1.25 * fwd, 6),
                ["American", R + 365 * 5])
        widened("5 y, r 0.31, q 0.01, call at 1.25F with dividends", process, CALL, round(1.25 * fwd, 6),
                ["European", R + 365 * 5], ([R + 365, R + 730], [1.0, 1.0]))

        # B2-2: five cash dividends of 8 at carry 0.30 take QuantLib's swapped walk below zero, its bound not a number
        dates, amounts = [R + 365 * y for y in range(1, 6)], [8.0] * 5
        div_fwd = (100.0 - sum(8.0 * math.exp(-0.30 * y) for y in range(1, 6))) * math.exp(0.30 * 6)
        for k in (0.8, 1.0, 1.25):
            for option_type, tname in ((CALL, "call"), (PUT, "put")):
                widened(f"6 y, r 0.31, q 0.01, five dividends of 8, {tname} at {k}F", process, option_type,
                        round(k * div_fwd, 6), ["European", R + 365 * 6], (dates, amounts))

        # the multi-strike solve where its grid falls short: each strike solved alone on the widened grid
        strikes = [round(k * fwd, 6) for k in (0.8, 1.0, 1.25)]
        per_strike = [H.call("FdHestonVanillaEngine::widenedResults", process, CALL, k, european5, [], [], 40, 50, 25,
                             0, HUNDSDORFER) for k in strikes for european5 in [["European", R + 365 * 5]]]
        out.append({"name": "multiple strikes, 5 y, r 0.31, q 0.01, calls, each strike on its own widened grid",
                    "inputs": {"settings": st, "kind": "multipleStrikes", "process": process, "optionType": CALL,
                               "referenceStrike": strikes[1], "exercise": ["European", R + 365 * 5],
                               "strikes": strikes, "tGrid": 40, "xGrid": 50, "vGrid": 25, "dampingSteps": 0,
                               "scheme": HUNDSDORFER},
                    "expected": {k: [v.enc(s[k]) for s in per_strike] for k in KEYS}})

        # B2-1: the reference strike at twice the spot, where the solve is read at S K/K_i, away from the forwards
        # QuantLib's multi-strike mesher is sized on
        process = bm.process_spec(LOW_VOL, rf=0.06, q=0.04)
        strikes = [160.0, 200.0, 250.0]
        per_strike = [H.call("FdHestonVanillaEngine::widenedResults", process, CALL, k, ["European", R + 365 * 5], [],
                             [], 100, 100, 50, 0, HUNDSDORFER) for k in strikes]
        out.append({"name": "multiple strikes, 5 y, r 0.06, q 0.04, calls, reference 200, each strike on its own grid",
                    "inputs": {"settings": st, "kind": "multipleStrikes", "process": process, "optionType": CALL,
                               "referenceStrike": 200.0, "exercise": ["European", R + 365 * 5],
                               "strikes": strikes, "tGrid": 100, "xGrid": 100, "vGrid": 50, "dampingSteps": 0,
                               "scheme": HUNDSDORFER},
                    "expected": {k: [v.enc(s[k]) for s in per_strike] for k in KEYS}})

        # B3-1: a foreign rate of 0.30 against a domestic 0.05, the quanto forward far from the curves' own; each strike
        # QuantLib's engine with the quanto helper
        process = bm.process_spec(LOW_VOL, rf=0.05, q=0.01)
        far_quanto = {"foreign": 0.30, "fxVolatility": 0.1, "correlation": 0.0, "atmLevel": 1.0}
        for strike in (80.0, 100.0, 125.0):
            setters = [["withQuantoHelper", far_quanto]]
            exercise = ["European", R + 365 * 5]
            r = H.call("MakeFdHestonVanillaEngine::results", process, CALL, strike, exercise, setters)
            out.append({"name": f"builder, quanto at a foreign rate of 0.30, 5 y, call at {strike}",
                        "inputs": {"settings": st, "kind": "builder", "process": process, "optionType": CALL,
                                   "strike": strike, "exercise": exercise, "setters": setters},
                        "expected": {k: v.enc(r[k]) for k in KEYS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdhestonvanillaengine.hpp", cases)
