# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdSimpleExtOUJumpSwingEngine and FdOrnsteinUhlenbeckVanillaEngine
(ql/experimental/finitedifferences/fdsimpleextoujumpswingengine.hpp, .cpp, fdornsteinuhlenbeckvanillaengine.hpp, .cpp),
on the evaluation date 18 December 2011.

- "swing" cases: swing options on Kluge's spot (QuantLib's swing case's process, and one with frequent small jumps) on
  monthly exercises over six months, the forward put and call payoffs at 30, rights from 1 to 6 (none obliged, and 2
  obliged), on small grids and a seasonal shape: each value.
- "ou" cases: vanilla options on an Ornstein-Uhlenbeck process, European and American calls and puts at strikes around
  its level, on a flat curve and with a cash dividend: each value, delta, gamma and theta.

No output is left out (the S-xeq plan's §2.8): the engines read their structures as QuantLib's, and every shape's last
time is the maturity's, so no lookup passes it (XER5 (xxxi)): the wheel's engine always takes a shape, and an empty one,
which QuantLib reads past its end (an access violation in the wheel), is a zero shape at the maturity here. Nothing is
fitted, so no output is filtered.

A swing spec is {"process" (an extended OU spec of tools/vectors/gen/experimental/processes/
extendedornsteinuhlenbeckprocess.py, a constant level), "jumps", "type", "strike", "minRights", "maxRights", "grids"
[t, x, y], "shape"} with shape pairs (time, value); an OU spec {"speed", "vol", "x0", "level", "type", "strike",
"exercise" ("European" or "American"), "dividend" (null, or [days, amount]), "grids" [t, x]}. The wheel's calls, by
QuantLib-SWIG 1.44: FdSimpleExtOUJumpSwingEngine(process, rTS, tGrid, xGrid, yGrid, shape), VanillaSwingOption(payoff,
exercise, minRights, maxRights), FdOrnsteinUhlenbeckVanillaEngine(process, rTS, [dividends,] tGrid, xGrid),
VanillaOption. Read back by tests/NanoQL.Tests/Vectors/Experimental/FiniteDifferences/
FdSimpleExtOUJumpSwingEngineVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(18, ql.December, 2011)
DC = ql.ActualActual(ql.ActualActual.ISDA)


def exercise_dates():
    dates = [R + ql.Period(1, ql.Months)]
    while dates[-1] < R + ql.Period(6, ql.Months):
        dates.append(dates[-1] + ql.Period(1, ql.Months))
    return dates


def kluge(spec, jumps):
    b = spec["level"]["b"]
    ou = ql.ExtendedOrnsteinUhlenbeckProcess(spec["speed"], spec["sigma"], spec["x0"], lambda t: b)
    return ql.ExtOUWithJumpsProcess(ou, jumps["y0"], jumps["beta"], jumps["jumpIntensity"], jumps["eta"])


def swing_case(name, st, spec, jumps, type_, strike, min_rights, max_rights, grids, shape):
    rts = ql.FlatForward(R, 0.1, DC)
    process = kluge(spec, jumps)
    engine = ql.FdSimpleExtOUJumpSwingEngine(process, rts, *grids, shape)
    payoff = ql.VanillaForwardPayoff(type_, strike)
    option = ql.VanillaSwingOption(payoff, ql.SwingExercise(exercise_dates()), min_rights, max_rights)
    option.setPricingEngine(engine)
    return {"name": name, "inputs": {"settings": st, "kind": "swing",
                                     "swing": {"process": spec, "jumps": jumps, "type": int(type_), "strike": strike,
                                               "minRights": min_rights, "maxRights": max_rights, "grids": grids,
                                               "shape": [list(p) for p in shape]}},
            "expected": {"npv": option.NPV()}}


def ou_case(name, st, spec):
    rts = ql.FlatForward(R, 0.05, DC)
    process = ql.OrnsteinUhlenbeckProcess(spec["speed"], spec["vol"], spec["x0"], spec["level"])
    maturity = R + ql.Period(1, ql.Years)
    exercise = ql.EuropeanExercise(maturity) if spec["exercise"] == "European" else ql.AmericanExercise(R, maturity)
    option = ql.VanillaOption(ql.PlainVanillaPayoff(spec["type"], spec["strike"]), exercise)
    t, x = spec["grids"]
    if spec["dividend"] is None:
        engine = ql.FdOrnsteinUhlenbeckVanillaEngine(process, rts, t, x)
    else:
        days, amount = spec["dividend"]
        engine = ql.FdOrnsteinUhlenbeckVanillaEngine(process, rts, ql.DividendSchedule([ql.FixedDividend(amount, R + days)]),
                                                     t, x)
    option.setPricingEngine(engine)
    return {"name": name, "inputs": {"settings": st, "kind": "ou", "ou": spec},
            "expected": {"npv": option.NPV(), "delta": option.delta(), "gamma": option.gamma(),
                         "theta": option.theta()}}


def cases():
    st = v.settings(R)
    out = []
    swing = {"speed": 1.0, "sigma": 2.0, "x0": 3.0, "level": {"type": "constant", "b": 3.0}}
    small = {"speed": 4.0, "sigma": 0.8, "x0": 3.3, "level": {"type": "constant", "b": 3.4}}
    kj = {"y0": 0.0, "beta": 5.0, "jumpIntensity": 1.0, "eta": 2.0}
    fj = {"y0": 0.1, "beta": 50.0, "jumpIntensity": 6.0, "eta": 8.0}
    put, call = ql.Option.Put, ql.Option.Call
    maturity_t = DC.yearFraction(R, exercise_dates()[-1])
    flat = [(maturity_t, 0.0)]
    for rights in (1, 3, 6):
        out.append(swing_case(f"swing: Kluge's case, a put, {rights} rights", st, swing, kj, put, 30.0, 0, rights,
                              [25, 50, 25], flat))
    out.append(swing_case("swing: Kluge's case, a call, 2 obliged of 4", st, swing, kj, call, 30.0, 2, 4,
                          [25, 50, 25], flat))
    out.append(swing_case("swing: frequent small jumps, a put, 3 rights", st, small, fj, put, 30.0, 0, 3,
                          [20, 40, 20], flat))
    out.append(swing_case("swing: a seasonal shape, a call, 2 rights", st, small, fj, call, 30.0, 0, 2,
                          [20, 40, 20], [(maturity_t / 2, 0.05), (maturity_t, -0.05)]))
    for name, spec in [
            ("ou: a European call", {"speed": 1.0, "vol": 0.3, "x0": 1.0, "level": 1.2, "type": int(call), "strike": 1.1,
                                     "exercise": "European", "dividend": None, "grids": [100, 100]}),
            ("ou: a European put below the level", {"speed": 2.0, "vol": 0.5, "x0": 0.5, "level": 1.0, "type": int(put),
                                                    "strike": 0.8, "exercise": "European", "dividend": None,
                                                    "grids": [100, 100]}),
            ("ou: an American put", {"speed": 1.5, "vol": 0.4, "x0": 1.0, "level": 0.6, "type": int(put), "strike": 1.0,
                                     "exercise": "American", "dividend": None, "grids": [100, 100]}),
            ("ou: an American call with a dividend", {"speed": 0.8, "vol": 0.3, "x0": 1.0, "level": 1.1,
                                                      "type": int(call), "strike": 0.9, "exercise": "American",
                                                      "dividend": [120, 0.05], "grids": [80, 120]})]:
        out.append(ou_case(name, st, spec))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/finitedifferences/fdsimpleextoujumpswingengine.hpp", cases)
