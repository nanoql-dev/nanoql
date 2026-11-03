# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdBlackScholesBarrierEngine (ql/pricingengines/barrier/fdblackscholesbarrierengine.hpp,
.cpp), on the evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports.

The four barrier types (at 85% of the spot below it, 115% above), with and without a rebate (3% of the spot), calls and
puts at two strikes (90 and 110% of the spot), a year out, on five processes of tools/vectors/gen/processes/
blackscholesprocess.py (flat and zero curves, constant and surface volatilities; the Merton, Black-Scholes and
Garman-Kohlhagen processes), on the default grid (100 × 100, Douglas); then on the flat process: other grids and
damping steps, every named scheme description, the local volatility (with an overwrite), cash dividends (a knock-in's
vanilla part priced by the finite-difference vanilla engine with them), an expired option, whose results are zero, and
the refusals: an American exercise, a spot beyond the barrier and a cash-or-nothing payoff.

Every structure is on one day counter and the risk-free curve's reference date: on others QuantLib's engine takes its
times from the risk-free curve and the operator reads the structures at them, where nanoql's takes them from the
engine's clock and reads each structure at their instants
(tests/NanoQL.Tests/Divergences/FdBarrierMismatchedTimesTests.cs and FdmMismatchedTimesTests.cs); no rebate is negative
(nanoql's fix there moves only the analytic engine).

A pricing spec is {"process", "barrier", "payoff", "exercise", "dividends", "tGrid", "xGrid", "dampingSteps",
"scheme", "localVol", "illegalLocalVolOverwrite"}: the process, payoff, exercise and barrier specs
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's, the dividends and scheme
tools/vectors/gen/pricingengines/vanilla/fdblackscholesvanillaengine.py's. The rebate engine's generator shares it.
The wheel's calls, by QuantLib-SWIG 1.44's options.i: FdBlackScholesBarrierEngine's two constructors (with and without
dividends) on a BarrierOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdBlackScholesBarrierEngineVectorTests.cs."""
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


ab = _load("analyticbarrierengine", "pricingengines/barrier/analyticbarrierengine.py")
fv = _load("fdblackscholesvanillaengine", "pricingengines/vanilla/fdblackscholesvanillaengine.py")
fbs, bsm, bsp, bc = fv.fbs, fv.bsm, ab.bsp, ab.bc
R = ab.R
CALL, PUT = ab.CALL, ab.PUT
RESULTS = ["NPV", "delta", "gamma", "theta"]
PROCESSES = [bsp.PROCESSES[i] for i in (0, 1, 3, 5, 9)]


def pricing_spec(process, barrier, payoff, ex, dividends=(), t_grid=100, x_grid=100, damping=0, scheme=None,
                 local_vol=False, overwrite=None):
    return {"process": process, "barrier": barrier, "payoff": payoff, "exercise": ex, "dividends": list(dividends),
            "tGrid": t_grid, "xGrid": x_grid, "dampingSteps": damping,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Douglas()),
            "localVol": local_vol, "illegalLocalVolOverwrite": overwrite}


def engine(spec, kind):
    """The engine of the given class (FdBlackScholesBarrierEngine or FdBlackScholesRebateEngine) for a spec."""
    process = bsp.process(spec["process"])
    overwrite = -bsm.NULL if spec["illegalLocalVolOverwrite"] is None else spec["illegalLocalVolOverwrite"]
    head = [process]
    if spec["dividends"]:
        head.append(bsm.dividends(spec["dividends"]))
    return kind(*head, spec["tGrid"], spec["xGrid"], spec["dampingSteps"], fbs.scheme(spec["scheme"]),
                spec["localVol"], overwrite)


def results(spec, kind=ql.FdBlackScholesBarrierEngine):
    """The engine's results for a spec, each null where QuantLib doesn't provide it, or the refusal."""
    option = ab.option(spec["barrier"], spec["payoff"], spec["exercise"])
    option.setPricingEngine(engine(spec, kind))
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

    year = ab.exercise_spec("European", 365)
    for pname, p in PROCESSES:
        x0 = p["x0"]
        for tname, t in ab.TYPES:
            level = x0 * (0.85 if t in (ab.DOWN_IN, ab.DOWN_OUT) else 1.15)
            for rebate in (0.0, 0.03 * x0):
                for option_type in (CALL, PUT):
                    for f in (0.9, 1.1):
                        payoff = bc.payoff_spec("PlainVanilla", option_type, x0 * f)
                        add(f"{pname}, {tname} at {level:g}, rebate {rebate:g}, "
                            f"{'call' if option_type == CALL else 'put'} {x0 * f:g}",
                            pricing_spec(p, ab.barrier_spec(t, level, rebate), payoff, year))
    p = PROCESSES[0][1]
    put = bc.payoff_spec("PlainVanilla", PUT, 100.0)
    down_out, up_in = ab.barrier_spec(ab.DOWN_OUT, 85.0, 3.0), ab.barrier_spec(ab.UP_IN, 115.0, 3.0)
    add("grid 50 × 200, damped", pricing_spec(p, down_out, put, year, t_grid=50, x_grid=200, damping=2))
    add("grid 200 × 51", pricing_spec(p, down_out, put, year, t_grid=200, x_grid=51))
    add("knock-in, grid 50 × 200, damped", pricing_spec(p, up_in, put, year, t_grid=50, x_grid=200, damping=2))
    for dname in fbs.NAMED:
        add(f"down-and-out put, {dname}", pricing_spec(p, down_out, put, year, t_grid=60, x_grid=60,
                                                       scheme=getattr(ql.FdmSchemeDesc, dname)()))
    surface = PROCESSES[2][1]
    add("local volatility, down-and-out put", pricing_spec(surface, down_out, put, year, local_vol=True))
    add("local volatility, up-and-in put", pricing_spec(surface, up_in, put, year, local_vol=True))
    add("local volatility, overwrite 0.2", pricing_spec(surface, down_out, put, year, local_vol=True, overwrite=0.2))
    divs = [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]
    for tname, t in ab.TYPES:
        level = 85.0 if t in (ab.DOWN_IN, ab.DOWN_OUT) else 115.0
        add(f"dividends, {tname} put", pricing_spec(p, ab.barrier_spec(t, level, 3.0), put, year, divs))
    add("dividends past maturity, down-and-out put",
        pricing_spec(p, down_out, put, year, [bsm.fixed(2.0, 400)]))
    add("expired", pricing_spec(p, down_out, put, ab.exercise_spec("European", -1)))
    add("an American exercise", pricing_spec(p, down_out, put, ab.exercise_spec("American", 365)))
    add("the spot beyond the barrier", pricing_spec(p, ab.barrier_spec(ab.DOWN_OUT, 105.0, 3.0), put, year))
    add("a cash-or-nothing payoff", pricing_spec(p, down_out, bc.payoff_spec("CashOrNothing", PUT, 100.0), year))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/fdblackscholesbarrierengine.hpp", cases)
