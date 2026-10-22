# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SviSmileSection (ql/experimental/volatility/svismilesection.hpp, .cpp): the SVI smile at given
parameters.

Sections on five parameter sets (QuantLib's own test's, a flat one, a steep put skew, a call skew and one at its
constraints' edges: a + bσ√(1−ρ²) at 0 and b(1+|ρ|) at 4), built from a time to expiry and from an expiry date (with
its default day counter and with Actual/360) on the evaluation date 15 October 2026: the volatility and variance at
strikes from 1e-8 (below the 1e-6 floor) to three times the forward, the ATM level, the minimum and maximum strikes and
the exercise time. Then QuantLib's refusals: a non-positive time, four or six parameters, and each of the parameters'
five checks failing.

A case's inputs are the "section": {"timeToExpiry"} or {"date", "dayCounter" (null for the default)}, with its
"forward" and "parameters" (a, b, σ, ρ, m), and the "strikes". The wheel's calls, by QuantLib-SWIG 1.44's:
SviSmileSection's two constructors, and volatility, variance, atmLevel, minStrike, maxStrike and exerciseTime. Read
back by tests/NanoQL.Tests/Vectors/Experimental/Volatility/SviSmileSectionVectorTests.cs."""
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")
TODAY = ql.Date(15, ql.October, 2026)
EDGE_B, EDGE_RHO, EDGE_SIGMA = 4.0 / 1.25, 0.25, 0.2
PARAMETERS = [
    ("QuantLib's test", [-0.0666, 0.229, 0.337, 0.439, 0.193]),
    ("flat", [0.04, 0.0, 0.1, 0.0, 0.0]),
    ("put skew", [0.01, 0.4, 0.15, -0.7, 0.05]),
    ("call skew", [0.02, 0.3, 0.2, 0.5, -0.1]),
    ("at the edges", [-EDGE_B * EDGE_SIGMA * (1.0 - EDGE_RHO * EDGE_RHO) ** 0.5, EDGE_B, EDGE_SIGMA, EDGE_RHO, 0.0]),
]
REFUSED = [
    ("four parameters", [0.04, 0.1, 0.1, 0.0]),
    ("six parameters", [0.04, 0.1, 0.1, 0.0, 0.0, 0.0]),
    ("b negative", [0.04, -0.1, 0.1, 0.0, 0.0]),
    ("rho at 1", [0.04, 0.1, 0.1, 1.0, 0.0]),
    ("sigma at 0", [0.04, 0.1, 0.0, 0.0, 0.0]),
    ("a below its constraint", [-0.1, 0.1, 0.1, 0.0, 0.0]),
    ("b(1+|rho|) above 4", [0.04, 3.5, 0.1, 0.3, 0.0]),
]


def section(spec):
    params = spec["parameters"]
    if "timeToExpiry" in spec:
        return ql.SviSmileSection(spec["timeToExpiry"], spec["forward"], params)
    if spec["dayCounter"] is None:
        return ql.SviSmileSection(ql.Date(spec["date"]), spec["forward"], params)
    return ql.SviSmileSection(ql.Date(spec["date"]), spec["forward"], params, irx.day_counter(spec["dayCounter"]))


def outputs(s, strikes):
    return {"volatility": [v.attempt(lambda k=k: s.volatility(k)) for k in strikes],
            "variance": [v.attempt(lambda k=k: s.variance(k)) for k in strikes],
            "atmLevel": s.atmLevel(), "minStrike": s.minStrike(), "maxStrike": s.maxStrike(),
            "exerciseTime": s.exerciseTime()}


def cases():
    ql.Settings.instance().evaluationDate = TODAY
    st = v.settings(TODAY)
    out = []
    forward = 100.0
    strikes = [1e-8, 50.0, 80.0, 100.0, 125.0, 200.0, 300.0]
    shapes = [("a time to expiry", {"timeToExpiry": 0.75}),
              ("an expiry date", {"date": (TODAY + 274).serialNumber(), "dayCounter": None}),
              ("an expiry date, Actual/360", {"date": (TODAY + 274).serialNumber(), "dayCounter": irx.A360})]
    for pname, params in PARAMETERS:
        for sname, shape in shapes:
            spec = dict(shape, forward=forward, parameters=params)
            out.append({"name": f"{pname}, {sname}", "inputs": {"settings": st, "section": spec, "strikes": strikes},
                        "expected": outputs(section(spec), strikes)})
    for rname, params in REFUSED + [("a zero time", PARAMETERS[1][1])]:
        spec = {"timeToExpiry": 0.0 if rname == "a zero time" else 0.75, "forward": forward, "parameters": params}
        try:
            section(spec)
            raise AssertionError(f"{rname} built")
        except RuntimeError:
            pass
        out.append({"name": rname, "inputs": {"settings": st, "section": spec, "strikes": strikes},
                    "expected": {"build": dict(v.THROWS)}})
    spec = {"date": TODAY.serialNumber(), "dayCounter": None, "forward": forward, "parameters": PARAMETERS[1][1]}
    try:
        section(spec)
        raise AssertionError("an expiry on the evaluation date built")
    except RuntimeError:
        pass
    out.append({"name": "an expiry on the evaluation date", "inputs": {"settings": st, "section": spec,
                                                                        "strikes": strikes},
                "expected": {"build": dict(v.THROWS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/svismilesection.hpp", cases)
