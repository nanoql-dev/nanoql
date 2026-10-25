# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SmileSectionRNDCalculator (ql/methods/finitedifferences/utilities/smilesectionrndcalculator.hpp,
.cpp): the Breeden-Litzenberger density, distribution and inverse of the log spot at a smile section's expiry, on flat
sections (20% and 45%, a year and a quarter), two SVI sections (one with fat wings on a low at-the-money volatility),
a SABR section and a linearly interpolated section, at table sizes of 50 and 200 and half-widths of 2, 5 and 8 normal
quantiles: the density and distribution at log-moneyness from -1.5 to 1.5, the inverse at probabilities from 1e-6 to
1 - 1e-6. A refusal: the inverse of a section without an at-the-money level (QuantLib's, raised at the inverse); the
wheel binds the calculator's one-argument forms alone, at the section's time, so a time other than it is a convention
fact's (tests/NanoQL.Tests/Conventions/SmileSectionRNDDeferredRefusalTests.cs).

The section specs are tools/vectors/gen/termstructures/volatility/flatsmilesection.py's, sabrsmilesection.py's and
interpolatedsmilesection.py's, and {"type": "SviSmileSection", "exerciseTime", "forward", "sviParameters"}. The wheel's
calls, by QuantLib-SWIG 1.44: SmileSectionRNDCalculator(smile, nStrikes, nStd), nStd always given (the wheel's default
is 5, QuantLib's header 8), and its one-argument pdf, cdf and invcdf. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/SmileSectionRNDCalculatorVectorTests.cs."""
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


flat = _load("flatsmilesection", "termstructures/volatility/flatsmilesection.py")
sabr = _load("sabrsmilesection", "termstructures/volatility/sabrsmilesection.py")
interp = _load("interpolatedsmilesection", "termstructures/volatility/interpolatedsmilesection.py")
R = ql.Date(15, ql.October, 2026)


def svi_spec(t, forward, params):
    return {"type": "SviSmileSection", "exerciseTime": t, "forward": forward, "sviParameters": params}


def section(spec):
    if spec["type"] == "SviSmileSection":
        return ql.SviSmileSection(spec["exerciseTime"], spec["forward"], spec["sviParameters"])
    if spec["type"] == "FlatSmileSection":
        return flat.make_section(spec)
    if spec["type"] == "SabrSmileSection":
        return sabr.make_section(spec)
    return interp.make_section(spec)


LINEAR = interp.section("LinearInterpolatedSmileSection", "time", 1.0, [60.0, 80.0, 100.0, 120.0, 150.0],
                        interp.std_devs([0.32, 0.26, 0.22, 0.21, 0.23], 1.0), 100.0, interp.A365)
SECTIONS = [
    ("flat 20%, a year", flat.time_spec(1.0, 0.20, interp.A365, 100.0), 200, 8.0),
    ("flat 45%, a quarter, 50 strikes, 2 quantiles", flat.time_spec(0.25, 0.45, interp.A365, 50.0), 50, 2.0),
    ("SVI", svi_spec(1.0, 50.0, [0.1125, 0.10, 0.10, 0.525, 0.0]), 200, 8.0),
    ("SVI, fat wings, low at-the-money", svi_spec(1.0, 50.0, [-0.7124561725, 0.2210528950, 3.2365013812,
                                                              -0.0171352352, -0.0547257886]), 200, 8.0),
    ("SABR", sabr.time_spec(2.0, 0.03, [0.15, 0.8, 0.5, -0.48]), 200, 5.0),
    ("linear, interpolated", LINEAR, 100, 5.0),
]
OFFSETS = [-1.5, -0.8, -0.3, -0.1, 0.0, 0.1, 0.3, 0.8, 1.5]
PROBABILITIES = [1e-6, 1e-3, 0.02, 0.25, 0.5, 0.75, 0.98, 0.999, 1 - 1e-6]


def cases():
    out = []
    st = v.settings(R)
    for name, spec, n, nstd in SECTIONS:
        s = section(spec)
        calc = ql.SmileSectionRNDCalculator(s, n, nstd)
        fwd = s.atmLevel()
        xs = [math.log(fwd) + o for o in OFFSETS]
        out.append({"name": name,
                    "inputs": {"settings": st, "section": spec, "nStrikes": n, "nStd": nstd, "x": xs,
                               "probabilities": PROBABILITIES},
                    "expected": v.enc({
                        "pdf": [v.attempt(lambda x=x: calc.pdf(x)) for x in xs],
                        "cdf": [v.attempt(lambda x=x: calc.cdf(x)) for x in xs],
                        "invcdf": [v.attempt(lambda p=p: calc.invcdf(p)) for p in PROBABILITIES]})})
    no_atm = flat.time_spec(1.0, 0.20, interp.A365, None)
    calc = ql.SmileSectionRNDCalculator(section(no_atm), 200, 8.0)
    out.append({"name": "flat, no at-the-money level",
                "inputs": {"settings": st, "section": no_atm, "nStrikes": 200, "nStd": 8.0,
                           "x": [math.log(80.0), math.log(120.0)], "probabilities": [0.5]},
                "expected": v.enc({
                    "pdf": [v.attempt(lambda k=k: calc.pdf(math.log(k))) for k in (80.0, 120.0)],
                    "cdf": [v.attempt(lambda k=k: calc.cdf(math.log(k))) for k in (80.0, 120.0)],
                    "invcdf": [v.attempt(lambda: calc.invcdf(0.5))]})})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/smilesectionrndcalculator.hpp", cases)
