# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdBlackScholesVanillaEngine (ql/pricingengines/vanilla/fdblackscholesvanillaengine.hpp,
.cpp), on the evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports.

European, American and Bermudan plain-vanilla calls and puts, at the money and away from it, on five processes (flat
and zero curves, constant and surface volatilities; the Merton, Black-Scholes and Black processes, each on Act/365
throughout), on the default grid (100 × 100, Douglas); then on the flat process: a cash-or-nothing put, other grids and
damping steps, every named scheme description, the local volatility, a quanto helper, and cash dividends under the spot
model; on the surface of QuantLib's testLocalVolatility (europeanoption.cpp), whose local variance turns negative on
the grid, the local volatility refused, and taken with an overwrite; and an expired option, whose results are zero. The
escrowed dividend model is tools/vectors/gen/methods/finitedifferences/utilities/escroweddividendadjustment.py's.
Structures on different day counters or reference dates are nanoql's refusal
(tests/NanoQL.Tests/Divergences/FdmMismatchedTimesTests.cs).

The specs, which the escrowed adjustment's generator shares, are a pricing {"process", "payoff", "exercise",
"dividends", "quanto", "tGrid", "xGrid", "dampingSteps", "scheme", "localVol", "illegalLocalVolOverwrite",
"cashDividendModel" (QuantLib's Spot 0, Escrowed 1)}, the exercise {"type": "European" | "American" | "Bermudan",
"dates"}, the others as the finite-difference generators write them. The wheel's calls, by QuantLib-SWIG 1.44's
options.i: FdBlackScholesVanillaEngine's four constructors (with and without dividends and a quanto helper), on a
VanillaOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdBlackScholesVanillaEngineVectorTests.cs."""
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


fbs = _load("fdmbackwardsolver", "methods/finitedifferences/solvers/fdmbackwardsolver.py")
bsm, bsp, bc = fbs.bsm, fbs.bsp, fbs.bc
R = fbs.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = ["NPV", "delta", "gamma", "theta"]

# QuantLib's testLocalVolatility (europeanoption.cpp): a zero curve, a zero dividend yield and a bicubic Black
# variance surface on the DAX, dated from the evaluation date
_LV_DAYS = [13, 41, 75, 165, 256, 345, 524, 703]
_LV_RATES = [0.0357, 0.0349, 0.0341, 0.0355, 0.0359, 0.0368, 0.0386, 0.0401]
_LV_STRIKES = [100, 500, 2000, 3400, 3600, 3800, 4000, 4200, 4400, 4500,
               4600, 4800, 5000, 5200, 5400, 5600, 7500, 10000, 20000, 30000]
_LV_VOLS = [
    [1.015873, 1.015873, 1.015873, 0.89729, 0.796493, 0.730914, 0.631335, 0.568895],
    [0.711309, 0.711309, 0.711309, 0.641309, 0.635593, 0.583653, 0.508045, 0.463182],
    [0.516034, 0.500534, 0.500534, 0.500534, 0.448706, 0.416661, 0.375470, 0.353442],
    [0.516034, 0.482263, 0.447713, 0.387703, 0.355064, 0.337438, 0.316966, 0.306859],
    [0.497587, 0.464373, 0.430764, 0.374052, 0.344336, 0.328607, 0.310619, 0.301865],
    [0.479511, 0.446815, 0.414194, 0.361010, 0.334204, 0.320301, 0.304664, 0.297180],
    [0.461866, 0.429645, 0.398092, 0.348638, 0.324680, 0.312512, 0.299082, 0.292785],
    [0.444801, 0.413014, 0.382634, 0.337026, 0.315788, 0.305239, 0.293855, 0.288660],
    [0.428604, 0.397219, 0.368109, 0.326282, 0.307555, 0.298483, 0.288972, 0.284791],
    [0.420971, 0.389782, 0.361317, 0.321274, 0.303697, 0.295302, 0.286655, 0.282948],
    [0.413749, 0.382754, 0.354917, 0.316532, 0.300016, 0.292251, 0.284420, 0.281164],
    [0.400889, 0.370272, 0.343525, 0.307904, 0.293204, 0.286549, 0.280189, 0.277767],
    [0.390685, 0.360399, 0.334344, 0.300507, 0.287149, 0.281380, 0.276271, 0.274588],
    [0.383477, 0.353434, 0.327580, 0.294408, 0.281867, 0.276746, 0.272655, 0.271617],
    [0.379106, 0.349214, 0.323160, 0.289618, 0.277362, 0.272641, 0.269332, 0.268846],
    [0.377073, 0.347258, 0.320776, 0.286077, 0.273617, 0.269057, 0.266293, 0.266265],
    [0.399925, 0.369232, 0.338895, 0.289042, 0.265509, 0.255589, 0.249308, 0.249665],
    [0.423432, 0.406891, 0.373720, 0.314667, 0.281009, 0.263281, 0.246451, 0.242166],
    [0.453704, 0.453704, 0.453704, 0.381255, 0.334578, 0.305527, 0.268909, 0.251367],
    [0.517748, 0.517748, 0.517748, 0.416577, 0.364770, 0.331595, 0.287423, 0.264285]]
LOCAL_VOL_PROCESS = bsp.process_spec(
    "BlackScholesMertonProcess", 4500.0, bsp.zero_spec(R, [0] + _LV_DAYS, [0.0357] + _LV_RATES),
    bsp.bvts.vol_spec_surface(R, _LV_DAYS, _LV_STRIKES, _LV_VOLS, interpolation="bicubic"), bsp.flat_spec(R, 0.0))


def exercise_spec(kind, offsets):
    return {"type": kind, "dates": [(R + d).serialNumber() for d in offsets]}


def exercise(spec):
    dates = [ql.Date(d) for d in spec["dates"]]
    if spec["type"] == "European":
        return ql.EuropeanExercise(dates[-1])
    if spec["type"] == "American":
        return ql.AmericanExercise(R, dates[-1])
    return ql.BermudanExercise(dates)


def pricing_spec(process, payoff, ex, dividends=(), quanto=None, t_grid=100, x_grid=100, damping=0,
                 scheme=None, local_vol=False, overwrite=None, model=0):
    return {"process": process, "payoff": payoff, "exercise": ex, "dividends": list(dividends), "quanto": quanto,
            "tGrid": t_grid, "xGrid": x_grid, "dampingSteps": damping,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Douglas()),
            "localVol": local_vol, "illegalLocalVolOverwrite": overwrite, "cashDividendModel": model}


def results(spec):
    """The engine's results for a spec, each null where QuantLib doesn't provide it, or the refusal."""
    process = bsp.process(spec["process"])
    helper = bsm.quanto(spec["quanto"])
    overwrite = -bsm.NULL if spec["illegalLocalVolOverwrite"] is None else spec["illegalLocalVolOverwrite"]
    model = ql.FdBlackScholesVanillaEngine.Spot if spec["cashDividendModel"] == 0 else ql.FdBlackScholesVanillaEngine.Escrowed
    tail = [spec["tGrid"], spec["xGrid"], spec["dampingSteps"], fbs.scheme(spec["scheme"]), spec["localVol"], overwrite,
            model]
    head = [process]
    if spec["dividends"]:
        head.append(bsm.dividends(spec["dividends"]))
    if helper is not None:
        head.append(helper)
    option = ql.VanillaOption(bc.payoff(spec["payoff"]), exercise(spec["exercise"]))
    option.setPricingEngine(ql.FdBlackScholesVanillaEngine(*head, *tail))
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

    for i in (0, 1, 3, 5, 7):
        pname, p = bsp.PROCESSES[i]
        for kind, offsets in (("European", [365]), ("American", [365]), ("Bermudan", [91, 182, 273, 365])):
            for option_type in (CALL, PUT):
                for strike in (100.0, 120.0):
                    payoff = bc.payoff_spec("PlainVanilla", option_type, strike)
                    add(f"{pname}, {kind} {'call' if option_type == CALL else 'put'} {strike}",
                        pricing_spec(p, payoff, exercise_spec(kind, offsets)))
    p = bsp.PROCESSES[0][1]
    put = bc.payoff_spec("PlainVanilla", PUT, 100.0)
    european, american = exercise_spec("European", [365]), exercise_spec("American", [365])
    add("cash-or-nothing put", pricing_spec(p, bc.payoff_spec("CashOrNothing", PUT, 105.0), european))
    add("grid 50 × 200, damped", pricing_spec(p, put, american, t_grid=50, x_grid=200, damping=2))
    add("grid 200 × 51", pricing_spec(p, put, european, t_grid=200, x_grid=51))
    for dname in fbs.NAMED:
        add(f"American put, {dname}", pricing_spec(p, put, american, t_grid=60, x_grid=60,
                                                   scheme=getattr(ql.FdmSchemeDesc, dname)()))
    surface = bsp.PROCESSES[3][1]
    add("local volatility, European put", pricing_spec(surface, put, european, local_vol=True))
    add("local volatility, American put", pricing_spec(surface, put, american, local_vol=True))
    add("local volatility, overwrite 0.2", pricing_spec(surface, put, american, local_vol=True, overwrite=0.2))
    # the local variance turns negative on this grid: refused by default, and the overwrite enters the price
    call_3400, put_3400 = bc.payoff_spec("PlainVanilla", CALL, 3400.0), bc.payoff_spec("PlainVanilla", PUT, 3400.0)
    for name, payoff, kind, overwrite in (("European call, refused", call_3400, "European", None),
                                          ("European call, overwrite 0.2", call_3400, "European", 0.2),
                                          ("American put, overwrite 0.4", put_3400, "American", 0.4)):
        add(f"illegal local volatility, {name}",
            pricing_spec(LOCAL_VOL_PROCESS, payoff, exercise_spec(kind, [524]), t_grid=200, x_grid=400,
                         local_vol=True, overwrite=overwrite))
    add("quanto, European put", pricing_spec(p, put, european, quanto=bsm.FLAT_QUANTO))
    add("quanto, American put, surface", pricing_spec(surface, put, american, quanto=bsm.FLAT_QUANTO))
    divs = [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]
    add("dividends, European put", pricing_spec(p, put, european, divs))
    add("dividends, American put", pricing_spec(p, put, american, divs))
    add("dividends, American call", pricing_spec(p, bc.payoff_spec("PlainVanilla", CALL, 100.0), american, divs))
    add("dividends outside, Bermudan put", pricing_spec(p, put, exercise_spec("Bermudan", [91, 182, 273, 365]),
                                                        [bsm.fixed(1.0, -10), bsm.fixed(2.0, 120), bsm.fixed(4.0, 500)]))
    add("quanto with dividends, American put", pricing_spec(p, put, american, divs, quanto=bsm.FLAT_QUANTO))
    add("expired", pricing_spec(p, put, exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdblackscholesvanillaengine.hpp", cases)
