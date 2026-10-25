# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonBlackVolSurface (ql/termstructures/volatility/equityfx/hestonblackvolsurface.hpp,
.cpp), on the evaluation date 15 October 2026: the Black volatility of a Heston model's out-of-the-money option prices.

Two Heston parameter sets (tools/vectors/gen/processes/hestonprocess.py's equity and far-from-Feller sets) on flat and
linear zero curves, by Gatheral's form, the angled contour and Andersen and Piterbarg's form, each on Gauss-Laguerre
of order 164 (the wheel's surface takes the form alone, its integration QuantLib-SWIG's default; QuantLib's C++
defaults, the angled contour at order 160, are a convention fact's): the Black volatility and variance
at three times and five strikes, the at-the-money level at those times, and the reference date. The curves share their
reference date and day counter, so the surface's clock is QuantLib's (the S-hes plan's HR7 (xi)).

A case holds one model {"process" (hestonprocess.py's spec, no discretization)} and a surface {"cpxLog"
(AnalyticHestonEngine's ComplexLogFormula integer), "order" (164)}. The wheel's calls, by QuantLib-SWIG 1.44's
volatilities.i: HestonBlackVolSurface(HestonModelHandle(HestonModel(HestonProcess(...))), cpxLog), blackVol,
blackVariance, atmLevel and referenceDate. Read back
by tests/NanoQL.Tests/Vectors/TermStructures/HestonBlackVolSurfaceVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
hp = _load("hestonprocess", "processes/hestonprocess.py")
R = bsp.R
TIMES = [0.25, 1.0, 3.0]
STRIKES = [0.7, 0.9, 1.0, 1.1, 1.4]
H = ql.AnalyticHestonEngine
SURFACES = [("Gatheral", {"cpxLog": int(H.Gatheral), "order": 164}),
            ("angled contour", {"cpxLog": int(H.AngledContour), "order": 164}),
            ("Andersen-Piterbarg", {"cpxLog": int(H.AndersenPiterbarg), "order": 164})]


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in (hp.PARAMETERS[0], hp.PARAMETERS[2]):
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho}
            model = ql.HestonModel(hp.process({**spec, "discretization": 0}))
            for sname, sspec in SURFACES:
                surface = ql.HestonBlackVolSurface(ql.HestonModelHandle(model), sspec["cpxLog"])
                strikes = [k * s0 for k in STRIKES]
                out.append({"name": f"{pname}, {cname} curves, {sname}",
                            "inputs": {"settings": st, "process": spec, "surface": sspec, "times": TIMES,
                                       "strikes": strikes},
                            "expected": {
                                "blackVol": [v.attempt(lambda: surface.blackVol(t, k)) for t in TIMES for k in strikes],
                                "blackVariance": [v.attempt(lambda: surface.blackVariance(t, k)) for t in TIMES
                                                  for k in strikes],
                                "atmLevel": [surface.atmLevel(t) for t in TIMES],
                                "referenceDate": v.enc(surface.referenceDate())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/hestonblackvolsurface.hpp", cases)
