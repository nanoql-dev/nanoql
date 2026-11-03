# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdSimpleBSSwingEngine (ql/pricingengines/vanilla/fdsimplebsswingengine.hpp, .cpp) and its
instrument, VanillaSwingOption, SwingExercise and VanillaForwardPayoff (ql/instruments/vanillaswingoption.hpp, .cpp),
on the evaluation date 15 October 2026: the NPV, delta, gamma and theta the engine reports; the forward payoff's values;
and the option's expiry.

Swing options on forward and plain-vanilla calls and puts at the money and away from it, on monthly exercise dates over
a year and on weekly ones over a quarter, with every number of rights from none to all and minimums of none, some and
all, on three processes (tools/vectors/gen/processes/blackscholesprocess.py's), on small grids (25 x 60, Douglas) and
testFdBSSwingOption's (50 x 200); then another scheme; the refusals of a minimum above the maximum and a maximum above
the dates; an expired option; and the forward payoff's values at prices around its strike. Exercises within a day, whose
seconds QuantLib's engine drops and nanoql's times (tests/NanoQL.Tests/Divergences/SwingExerciseSecondsTests.cs), are
left out: the wheel's SwingExercise takes dates alone. Every structure is on the risk-free curve's day counter and
reference date (tests/NanoQL.Tests/Divergences/FdShoutSwingCirMismatchedTimesTests.cs).

The specs are a pricing {"process", "payoff" (plain vanilla, or {"type": "Forward", "optionType", "strike"}), "dates",
"minExerciseRights", "maxExerciseRights", "tGrid", "xGrid", "scheme"}. The wheel's calls, by QuantLib-SWIG 1.44's
options.i and exercise.i: SwingExercise(dates), VanillaForwardPayoff(type, strike) and its call, VanillaSwingOption(
payoff, exercise, minExerciseRights, maxExerciseRights) and its isExpired, FdSimpleBSSwingEngine(process, tGrid, xGrid,
schemeDesc); NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdSimpleBSSwingEngineVectorTests.cs."""
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


fbv = _load("fdblackscholesvanillaengine", "pricingengines/vanilla/fdblackscholesvanillaengine.py")
fbs, bsp, bc = fbv.fbs, fbv.bsp, fbv.bc
R = fbv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = fbv.RESULTS


def forward_spec(option_type, strike):
    return {"type": "Forward", "optionType": option_type, "strike": strike}


def payoff(spec):
    if spec["type"] == "Forward":
        return ql.VanillaForwardPayoff(spec["optionType"], spec["strike"])
    return bc.payoff(spec)


def pricing_spec(process, pay, offsets, minimum, maximum, t_grid=25, x_grid=60, scheme=None):
    return {"process": process, "payoff": pay, "dates": [(R + d).serialNumber() for d in offsets],
            "minExerciseRights": minimum, "maxExerciseRights": maximum, "tGrid": t_grid, "xGrid": x_grid,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Douglas())}


def results(spec):
    process = bsp.process(spec["process"])
    option = ql.VanillaSwingOption(payoff(spec["payoff"]), ql.SwingExercise([ql.Date(d) for d in spec["dates"]]),
                                   spec["minExerciseRights"], spec["maxExerciseRights"])
    option.setPricingEngine(ql.FdSimpleBSSwingEngine(process, spec["tGrid"], spec["xGrid"], fbs.scheme(spec["scheme"])))
    out = {"isExpired": option.isExpired()}
    try:
        option.NPV()
    except RuntimeError:
        out.update({r: dict(v.THROWS) for r in RESULTS})
        return out
    out.update({r: v.result(lambda r=r: getattr(option, r)()) for r in RESULTS})
    return out


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec}, "expected": results(spec)})

    monthly = [30 * i for i in range(1, 13)]
    weekly = [7 * i for i in range(1, 14)]
    for i in (0, 1, 5):
        pname, p = bsp.PROCESSES[i]
        for option_type in (CALL, PUT):
            for strike in (90.0, 100.0, 115.0):
                pay = forward_spec(option_type, strike)
                kind = "call" if option_type == CALL else "put"
                for maximum in (1, 4, 12):
                    add(f"{pname}, monthly forward {kind} {strike}, at most {maximum}",
                        pricing_spec(p, pay, monthly, 0, maximum))
                add(f"{pname}, monthly forward {kind} {strike}, 3 to 6", pricing_spec(p, pay, monthly, 3, 6))
                add(f"{pname}, monthly forward {kind} {strike}, all forced", pricing_spec(p, pay, monthly, 12, 12))
                add(f"{pname}, monthly plain {kind} {strike}, at most 4",
                    pricing_spec(p, bc.payoff_spec("PlainVanilla", option_type, strike), monthly, 0, 4))
    p = bsp.PROCESSES[0][1]
    put = forward_spec(PUT, 100.0)
    add("weekly forward put, at most 5", pricing_spec(p, put, weekly, 0, 5))
    add("none of the rights", pricing_spec(p, put, monthly, 0, 0))
    add("testFdBSSwingOption's grid, at most 6", pricing_spec(p, put, monthly, 0, 6, 50, 200))
    add("Crank-Nicolson, at most 4", pricing_spec(p, put, monthly, 0, 4, scheme=ql.FdmSchemeDesc.CrankNicolson()))
    add("minimum above the maximum", pricing_spec(p, put, monthly, 5, 4))
    add("maximum above the dates", pricing_spec(p, put, monthly, 0, 13))
    add("expired", pricing_spec(p, put, [-60, -30, -1], 0, 2))
    payoffs = []
    for option_type in (CALL, PUT):
        pay = forward_spec(option_type, 100.0)
        payoffs.append({"name": f"forward {'call' if option_type == CALL else 'put'} at 100",
                        "inputs": {"settings": st, "payoff": pay, "prices": [0.0, 50.0, 99.5, 100.0, 100.5, 150.0]},
                        "expected": {"values": [payoff(pay)(x) for x in (0.0, 50.0, 99.5, 100.0, 100.5, 150.0)]}})
    return out + payoffs


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdsimplebsswingengine.hpp", cases)
