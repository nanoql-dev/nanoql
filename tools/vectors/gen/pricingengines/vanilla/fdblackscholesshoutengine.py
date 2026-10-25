# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdBlackScholesShoutEngine (ql/pricingengines/vanilla/fdblackscholesshoutengine.hpp, .cpp), on
the evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports.

Shout calls and puts at the money and away from it, their windows from today and from a quarter on, on four processes
(flat and zero curves, constant and surface volatilities: tools/vectors/gen/processes/blackscholesprocess.py's), on a
small grid (40 x 80, Douglas) and on testFDShoutNPV's market and grid (400 x 200); then other damping steps and
schemes, a dividend before the window, and an expired option. A dividend between a shout and expiry, where QuantLib
strikes the residual option at the escrowed level and nanoql at the spot (the S-van plan's VR4 (xxx),
tests/NanoQL.Tests/Divergences/ShoutDividendStrikeTests.cs), is left out: the one dividend is paid before the window
opens, where the two levels are one. Every structure is on the risk-free curve's day counter and reference date
(tests/NanoQL.Tests/Divergences/FdShoutSwingCirMismatchedTimesTests.cs).

The specs are a pricing {"process", "payoff", "exercise": {"earliest", "latest"}, "dividends", "tGrid", "xGrid",
"dampingSteps", "scheme"}, the process, payoff, dividends and scheme tools/vectors/gen/pricingengines/vanilla/
fdblackscholesvanillaengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i: FdBlackScholesShoutEngine's
two constructors (with and without dividends) on a VanillaOption with an AmericanExercise; NPV, delta, gamma and theta
the option's accessors. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/FdBlackScholesShoutEngineVectorTests.cs."""
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
fbs, bsm, bsp, bc = fbv.fbs, fbv.bsm, fbv.bsp, fbv.bc
R = fbv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = fbv.RESULTS


def window(earliest, latest):
    return {"earliest": (R + earliest).serialNumber(), "latest": (R + latest).serialNumber()}


def pricing_spec(process, payoff, ex, dividends=(), t_grid=40, x_grid=80, damping=0, scheme=None):
    return {"process": process, "payoff": payoff, "exercise": ex, "dividends": list(dividends), "tGrid": t_grid,
            "xGrid": x_grid, "dampingSteps": damping,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Douglas())}


def results(spec):
    process = bsp.process(spec["process"])
    tail = [spec["tGrid"], spec["xGrid"], spec["dampingSteps"], fbs.scheme(spec["scheme"])]
    head = [process] + ([bsm.dividends(spec["dividends"])] if spec["dividends"] else [])
    ex = spec["exercise"]
    option = ql.VanillaOption(bc.payoff(spec["payoff"]),
                              ql.AmericanExercise(ql.Date(ex["earliest"]), ql.Date(ex["latest"])))
    option.setPricingEngine(ql.FdBlackScholesShoutEngine(*head, *tail))
    try:
        option.NPV()
    except RuntimeError:
        return {r: dict(v.THROWS) for r in RESULTS}
    return {r: v.result(lambda r=r: getattr(option, r)()) for r in RESULTS}


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec}, "expected": results(spec)})

    for i in (0, 1, 3, 5):
        pname, p = bsp.PROCESSES[i]
        for wname, ex in (("from today", window(0, 365)), ("from a quarter", window(91, 365))):
            for option_type in (CALL, PUT):
                for strike in (90.0, 100.0, 120.0):
                    payoff = bc.payoff_spec("PlainVanilla", option_type, strike)
                    add(f"{pname}, {wname}, {'call' if option_type == CALL else 'put'} {strike}",
                        pricing_spec(p, payoff, ex))
    market = bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.flat_spec(R, 0.06),
                              bsp.bvts.vol_spec_constant(R, 0.25), bsp.flat_spec(R, 0.03))
    for strike, option_type in ((105, PUT), (105, CALL), (120, PUT), (80, CALL)):
        add(f"testFDShoutNPV's {'call' if option_type == CALL else 'put'} {strike}",
            pricing_spec(market, bc.payoff_spec("PlainVanilla", option_type, float(strike)), window(0, 1826), (), 400, 200))
    p = bsp.PROCESSES[0][1]
    put = bc.payoff_spec("PlainVanilla", PUT, 100.0)
    add("damped", pricing_spec(p, put, window(0, 365), damping=2))
    for dname in ("CrankNicolson", "Hundsdorfer", "ImplicitEuler"):
        add(f"put, {dname}", pricing_spec(p, put, window(0, 365), scheme=getattr(ql.FdmSchemeDesc, dname)()))
    add("a dividend before the window", pricing_spec(p, put, window(120, 365), [bsm.fixed(2.0, 60)]))
    add("expired", pricing_spec(p, put, window(-30, -1)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdblackscholesshoutengine.hpp", cases)
