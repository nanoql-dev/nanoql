# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBlackScholesMesher (ql/methods/finitedifferences/meshers/fdmblackscholesmesher.hpp, .cpp),
on the evaluation date 15 October 2026: the log-spot mesher the finite-difference Black-Scholes engines build, its
locations, dplus and dminus (the last dplus and the first dminus QuantLib's Null<Real>, written as null).

Five processes (flat and zero curves, constant and surface volatilities, the Merton, Black-Scholes and Black processes),
at three maturities and three strikes, then on the first and fourth: edge constraints (both, the lower alone, the upper
alone), a wide and a narrow tail (eps, scaleFactor), critical points inside and outside the edges, cash dividends
(inside the maturity, before the evaluation date and after the maturity, which the mesher skips, and a large one),
quanto helpers (flat and zero curves, a constant and a curve exchange-rate volatility, two correlations), spot
adjustments, QuantLib's high-interest-rate and low-volatility, high-dividend cases (fdmlinearop.cpp), and sizes 2 and 3.
Then the refusal of a null spot ("negative or null underlying given").

The specs, which the other finite-difference generators share, are written and built below: a 1-D mesher is {"type":
"Uniform", "start", "end", "size"}, {"type": "Concentrating", "start", "end", "size", "cPoint": [point, density] (either
null), "requireCPoint"}, {"type": "ConcentratingPoints", "start", "end", "size", "points": [[point, density, required],
...], "tol"} or {"type": "BlackScholes", "size", "process", "maturity", "strike", "xMin", "xMax" (null for none), "eps",
"scaleFactor", "cPoint" ([point, density] or null), "dividends", "quanto" (null for none), "spotAdjustment"}; a quanto
helper {"riskFree", "foreign", "fxVolatility", "correlation", "atmLevel"}; a process, curve and volatility are
tools/vectors/gen/processes/blackscholesprocess.py's, a dividend {"type": "Fixed", "amount", "date"}. The wheel's calls,
by QuantLib-SWIG 1.44's fdm.i: the C++ constructors, the dividends a DividendSchedule of FixedDividend, the quanto
helper's curves and volatility the structures themselves; locations, dplus and dminus the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/FdmBlackScholesMesherVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bvts = bsp.bvts
R = bsp.R
NULL = ql.nullDouble()


def fixed(amount, offset):
    return {"type": "Fixed", "amount": amount, "date": (R + offset).serialNumber()}


def dividends(specs):
    return [ql.FixedDividend(d["amount"], ql.Date(d["date"])) for d in specs]


def quanto_spec(risk_free, foreign, fx_vol, correlation, atm_level=1.0):
    return {"riskFree": risk_free, "foreign": foreign, "fxVolatility": fx_vol, "correlation": correlation,
            "atmLevel": atm_level}


def quanto(spec):
    if spec is None:
        return None
    return ql.FdmQuantoHelper(bsp.curve(spec["riskFree"]), bsp.curve(spec["foreign"]),
                              bvts.black_vol(spec["fxVolatility"]), spec["correlation"], spec["atmLevel"])


def bs_spec(size, process, maturity, strike, x_min=None, x_max=None, eps=0.0001, scale=1.5, c_point=None,
            divs=(), quanto_helper=None, spot_adjustment=0.0):
    return {"type": "BlackScholes", "size": size, "process": process, "maturity": maturity, "strike": strike,
            "xMin": x_min, "xMax": x_max, "eps": eps, "scaleFactor": scale,
            "cPoint": None if c_point is None else list(c_point), "dividends": list(divs), "quanto": quanto_helper,
            "spotAdjustment": spot_adjustment}


def _null(x):
    return NULL if x is None else x


def mesher_1d(spec):
    """The wheel's 1-D mesher for a spec."""
    t = spec["type"]
    if t == "Uniform":
        return ql.Uniform1dMesher(spec["start"], spec["end"], spec["size"])
    if t == "Concentrating":
        p, d = spec["cPoint"]
        return ql.Concentrating1dMesher(spec["start"], spec["end"], spec["size"], (_null(p), _null(d)),
                                        spec["requireCPoint"])
    if t == "ConcentratingPoints":
        points = ql.Concentrating1dMesherPointVector()
        for p, d, r in spec["points"]:
            points.push_back(ql.Concentrating1dMesherPoint(p, d, r))
        return ql.Concentrating1dMesher(spec["start"], spec["end"], spec["size"], points, spec["tol"])
    if t == "BlackScholes":
        c = spec["cPoint"]
        return ql.FdmBlackScholesMesher(
            spec["size"], bsp.process(spec["process"]), spec["maturity"], spec["strike"], spec["xMin"], spec["xMax"],
            spec["eps"], spec["scaleFactor"], (NULL, NULL) if c is None else (c[0], c[1]),
            dividends(spec["dividends"]), quanto(spec["quanto"]), spec["spotAdjustment"])
    raise ValueError(f"unknown mesher {t}")


def outputs(m):
    n = m.size()
    return {"locations": v.enc(list(m.locations())), "dplus": v.enc([m.dplus(i) for i in range(n)]),
            "dminus": v.enc([m.dminus(i) for i in range(n)])}


MERTON_FLAT = bsp.PROCESSES[0][1]
MERTON_ZERO_SURFACE = bsp.PROCESSES[3][1]
PROCESSES = [bsp.PROCESSES[i] for i in (0, 1, 3, 5, 7)]
FLAT_QUANTO = quanto_spec(bsp.flat_spec(R, 0.04), bsp.flat_spec(R, 0.06), bvts.vol_spec_constant(R, 0.12), -0.75)
ZERO_QUANTO = quanto_spec(bsp.ZERO_R, bsp.zero_spec(R, [0, 365, 1825], [0.05, 0.055, 0.06]),
                          bvts.vol_spec_curve(R, [182, 365, 730], [0.10, 0.11, 0.12]), 0.5, 1.2)


def variants():
    """(name, spec) on the Merton process with flat curves and on the one with zero curves and a surface."""
    out = []
    for pname, p in (("flat", MERTON_FLAT), ("surface", MERTON_ZERO_SURFACE)):
        log50, log200 = math.log(50.0), math.log(200.0)
        out += [
            (f"{pname}, both edges constrained", bs_spec(30, p, 1.0, 100.0, log50, log200)),
            (f"{pname}, lower edge constrained", bs_spec(30, p, 1.0, 100.0, log50)),
            (f"{pname}, upper edge constrained", bs_spec(30, p, 1.0, 100.0, None, log200)),
            (f"{pname}, wide tail", bs_spec(30, p, 1.0, 100.0, eps=0.05, scale=2.5)),
            (f"{pname}, narrow tail", bs_spec(30, p, 1.0, 100.0, eps=1e-6, scale=1.0)),
            (f"{pname}, critical point at the strike", bs_spec(41, p, 1.0, 100.0, c_point=(100.0, 0.1))),
            (f"{pname}, dense critical point", bs_spec(41, p, 2.0, 90.0, c_point=(90.0, 0.01))),
            (f"{pname}, critical point below the edge", bs_spec(41, p, 1.0, 100.0, c_point=(1e-5, 0.1))),
            (f"{pname}, dividends", bs_spec(30, p, 1.0, 100.0, divs=[fixed(2.0, 91), fixed(3.0, 273)])),
            (f"{pname}, dividends outside the maturity",
             bs_spec(30, p, 1.0, 100.0, divs=[fixed(2.0, -30), fixed(1.0, 120), fixed(4.0, 500)])),
            (f"{pname}, a large dividend", bs_spec(30, p, 0.5, 100.0, divs=[fixed(40.0, 60)])),
            (f"{pname}, flat quanto", bs_spec(30, p, 1.0, 100.0, quanto_helper=FLAT_QUANTO)),
            (f"{pname}, zero-curve quanto", bs_spec(30, p, 1.5, 110.0, quanto_helper=ZERO_QUANTO)),
            (f"{pname}, quanto with dividends",
             bs_spec(30, p, 1.0, 100.0, divs=[fixed(2.0, 91)], quanto_helper=FLAT_QUANTO)),
            (f"{pname}, spot adjusted up", bs_spec(30, p, 1.0, 100.0, spot_adjustment=5.0)),
            (f"{pname}, spot adjusted down", bs_spec(30, p, 1.0, 100.0, spot_adjustment=-10.0)),
            (f"{pname}, size 2", bs_spec(2, p, 1.0, 100.0)),
            (f"{pname}, size 3", bs_spec(3, p, 0.5, 100.0)),
        ]
    high = bsp.process_spec("GeneralizedBlackScholesProcess", 100.0, bsp.flat_spec(R, 0.21), bvts.vol_spec_constant(
        R, 0.25), bsp.flat_spec(R, 0.02))
    low = bsp.process_spec("GeneralizedBlackScholesProcess", 100.0, bsp.flat_spec(R, 0.16), bvts.vol_spec_constant(
        R, 0.0), bsp.flat_spec(R, 0.07))
    out += [("high interest rate", bs_spec(10, high, 2.0, 100.0, eps=0.05, scale=2.5)),
            ("low volatility, high dividends", bs_spec(5, low, 1.0, 100.0, divs=[fixed(10.0, 212), fixed(5.0, 334)]))]
    return out


def cases():
    st = v.settings(R)
    out = []
    for pname, p in PROCESSES:
        for maturity in (0.25, 1.0, 3.0):
            for strike in (80.0, 100.0, 125.0):
                spec = bs_spec(25, p, maturity, strike)
                out.append({"name": f"{pname}, {maturity} years, strike {strike}",
                            "inputs": {"settings": st, "mesher": spec}, "expected": outputs(mesher_1d(spec))})
    for name, spec in variants():
        out.append({"name": name, "inputs": {"settings": st, "mesher": spec}, "expected": outputs(mesher_1d(spec))})
    null_spot = bsp.process_spec("BlackScholesMertonProcess", 0.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q)
    spec = bs_spec(25, null_spot, 1.0, 100.0)
    out.append({"name": "null spot", "inputs": {"settings": st, "mesher": spec},
                "expected": {"mesher": v.attempt(lambda: outputs(mesher_1d(spec)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/fdmblackscholesmesher.hpp", cases)
