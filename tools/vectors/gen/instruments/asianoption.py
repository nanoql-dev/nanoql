# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscreteAveragingAsianOption and ContinuousAveragingAsianOption
(ql/instruments/asianoption.hpp, .cpp), on the evaluation date 15 October 2026: the options' arguments as their engines
read them.

Arithmetic options priced by the Turnbull-Wakeman engine and geometric ones by the analytic discrete geometric engine,
on the flat Merton process of tools/vectors/gen/processes/blackscholesprocess.py, built both ways: from a running
accumulator and a count of past fixings, and from every fixing date with the past fixings' values, which the option
splits at the evaluation date (a fixing on it is to come). Then the refusals: too few past fixings given, a negative
running sum, a running product of 0, and an expired option. The fixing dates are sorted, as both constructors' outputs
are left out where they aren't (the S-bar plan's §2.10: QuantLib sorts the first constructor's only, BR5 (xl)).

An option spec is {"averageType" (QuantLib's Average::Type, Arithmetic 0, Geometric 1), "fixingDates" (serials),
"payoff", "exercise", and either "runningAccumulator" and "pastFixings" or "allPastFixings"}; the payoff spec is
tools/vectors/gen/pricingengines/blackcalculator.py's, the exercise tools/vectors/gen/pricingengines/barrier/
analyticbarrierengine.py's. The specs, the schedules and the option are shared by the Asian engines' generators, which
import this file. The wheel's calls, by QuantLib-SWIG 1.44's options.i: DiscreteAveragingAsianOption(averageType,
runningAccumulator, pastFixings, fixingDates, payoff, exercise) and (averageType, fixingDates, payoff, exercise,
allPastFixings), TurnbullWakemanAsianEngine(process), AnalyticDiscreteGeometricAveragePriceAsianEngine(process) and the
option's NPV. Read back by tests/NanoQL.Tests/Vectors/Instruments/AsianOptionVectorTests.cs."""
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


abe = _load("analyticbarrierengine", "pricingengines/barrier/analyticbarrierengine.py")
bsp, bc = abe.bsp, abe.bc
R = abe.R
CALL, PUT = abe.CALL, abe.PUT
ARITHMETIC, GEOMETRIC = int(ql.Average.Arithmetic), int(ql.Average.Geometric)

# the flat, constant-volatility processes, where the geometric engines' and Levy's forms are QuantLib's (§2.10)
FLAT = [(n, s) for n, s in bsp.PROCESSES if n.endswith("flat, constant")]
MERTON = FLAT[0][1]


def dates(days, n, first=None):
    """n fixing dates from `first` days (one period by default) to `days` days, evenly, as serials."""
    first = days / n if first is None else first
    if n == 1:
        return [(R + days).serialNumber()]
    return [(R + int(round(first + i * (days - first) / (n - 1)))).serialNumber() for i in range(n)]


def option_spec(average, fixing_dates, payoff, ex, running=None, past=0, all_past=None):
    spec = {"averageType": average, "fixingDates": fixing_dates, "payoff": payoff, "exercise": ex}
    if all_past is None:
        spec["runningAccumulator"] = (0.0 if average == ARITHMETIC else 1.0) if running is None else running
        spec["pastFixings"] = past
    else:
        spec["allPastFixings"] = all_past
    return spec


def option(spec):
    fixings = [ql.Date(d) for d in spec["fixingDates"]]
    payoff, ex = bc.payoff(spec["payoff"]), abe.exercise(spec["exercise"])
    if "allPastFixings" in spec:
        return ql.DiscreteAveragingAsianOption(spec["averageType"], fixings, payoff, ex, spec["allPastFixings"])
    return ql.DiscreteAveragingAsianOption(spec["averageType"], spec["runningAccumulator"], spec["pastFixings"], fixings,
                                           payoff, ex)


def npv(opt, engine):
    opt.setPricingEngine(engine)
    return v.attempt(opt.NPV)


def cases():
    st = v.settings(R)
    p = bsp.process(MERTON)
    tw = ql.TurnbullWakemanAsianEngine(p)
    geo = ql.AnalyticDiscreteGeometricAveragePriceAsianEngine(p)
    out = []
    past_dates = [(R - 60).serialNumber(), (R - 30).serialNumber()]
    for average, aname, engine in ((ARITHMETIC, "arithmetic", tw), (GEOMETRIC, "geometric", geo)):
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            future = dates(days, 6)
            for tname, t in (("call", CALL), ("put", PUT)):
                for k in (90.0, 100.0, 110.0):
                    pay = bc.payoff_spec("PlainVanilla", t, k)
                    specs = [("unseasoned", option_spec(average, future, pay, ex)),
                             ("two past fixings at 95 and 105",
                              option_spec(average, future, pay, ex,
                                          running=200.0 if average == ARITHMETIC else 95.0 * 105.0, past=2)),
                             ("every date, two past at 95 and 105",
                              option_spec(average, past_dates + future, pay, ex, all_past=[95.0, 105.0])),
                             ("every date, a fixing today",
                              option_spec(average, [R.serialNumber()] + future, pay, ex, all_past=[]))]
                    for sname, spec in specs:
                        out.append({"name": f"{aname}, {days} days, a {tname} at {k:g}, {sname}",
                                    "inputs": {"settings": st, "process": MERTON, "option": spec},
                                    "expected": {"NPV": npv(option(spec), engine)}})
    ex = abe.exercise_spec("European", 365)
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    future = dates(365, 6)
    for name, spec, engine in (
            ("too few past fixings given", option_spec(ARITHMETIC, past_dates + future, pay, ex, all_past=[95.0]), tw),
            ("a negative running sum", option_spec(ARITHMETIC, future, pay, ex, running=-1.0, past=1), tw),
            ("a running product of 0", option_spec(GEOMETRIC, future, pay, ex, running=0.0, past=1), geo),
            ("expired", option_spec(ARITHMETIC, dates(-1, 1), pay, abe.exercise_spec("European", -1)), tw)):
        out.append({"name": name, "inputs": {"settings": st, "process": MERTON, "option": spec},
                    "expected": {"NPV": npv(option(spec), engine)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/asianoption.hpp", cases)
