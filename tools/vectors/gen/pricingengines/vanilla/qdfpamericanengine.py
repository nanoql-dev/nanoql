# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's QdFpAmericanEngine and its iteration schemes (ql/pricingengines/vanilla/
qdfpamericanengine.hpp, .cpp), on the evaluation date 15 October 2026: the NPVs of American calls and puts on plain
vanilla payoffs, exercisable from the evaluation date, over the Barone-Adesi-Whaley generator's grid
(tools/vectors/gen/pricingengines/vanilla/baroneadesiwhaleyengine.py), each priced by eight settings, CONFIGS below:
the engine's three named schemes (fast, accurate and high precision) with the equation chosen automatically, the fast
scheme on each fixed-point equation, and a Gauss-Legendre, a Legendre-tanh-sinh and a tanh-sinh scheme
of their own sizes. Then an option whose window opened before the evaluation date, an expired one, and the refusals of
a European exercise, a payoff at expiry and a spot at zero, on the defaults.

Every structure is dated on the evaluation date and counts on Actual/365 (Fixed), and no window opens after it, so
neither the mismatched-dates class (tests/NanoQL.Tests/Divergences/QdMismatchedTimesTests.cs) nor the window
(ApproximationWindowTests.cs) reaches the file.

An engine spec is {"scheme", "fpEquation" (QuantLib's FixedPointEquation as an integer)}; a scheme {"type": "fast" |
"accurate" | "highPrecision"} or {"type": "Legendre", "l", "m", "n", "p"} | {"type": "LegendreTanhSinh", "l", "m", "n",
"eps"} | {"type": "TanhSinh", "m", "n", "eps"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
QdFpAmericanEngine(process, scheme, fpEquation), the C++ constructor; QdFpAmericanEngine.fastScheme(),
accurateScheme() and highPrecisionScheme(), and the three schemes' constructors; VanillaOption(payoff, exercise) and
its NPV. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/QdFpAmericanEngineVectorTests.cs."""
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


GRID = _load("baroneadesiwhaleyengine", "pricingengines/vanilla/baroneadesiwhaleyengine.py")
E = ql.QdFpAmericanEngine

FAST, ACCURATE, HIGH = {"type": "fast"}, {"type": "accurate"}, {"type": "highPrecision"}
CONFIGS = [("fast", FAST, E.Auto), ("accurate", ACCURATE, E.Auto), ("high precision", HIGH, E.Auto),
           ("fast, FP-A", FAST, E.FP_A), ("fast, FP-B", FAST, E.FP_B),
           ("Legendre (5, 3, 5, 15)", {"type": "Legendre", "l": 5, "m": 3, "n": 5, "p": 15}, E.Auto),
           ("Legendre-tanh-sinh (10, 4, 9, 1e-7)",
            {"type": "LegendreTanhSinh", "l": 10, "m": 4, "n": 9, "eps": 1e-7}, E.Auto),
           ("tanh-sinh (6, 12, 1e-9)", {"type": "TanhSinh", "m": 6, "n": 12, "eps": 1e-9}, E.Auto)]


def scheme(spec):
    t = spec["type"]
    if t == "fast":
        return E.fastScheme()
    if t == "accurate":
        return E.accurateScheme()
    if t == "highPrecision":
        return E.highPrecisionScheme()
    if t == "Legendre":
        return ql.QdFpLegendreScheme(spec["l"], spec["m"], spec["n"], spec["p"])
    if t == "LegendreTanhSinh":
        return ql.QdFpLegendreTanhSinhScheme(spec["l"], spec["m"], spec["n"], spec["eps"])
    if t == "TanhSinh":
        return ql.QdFpTanhSinhIterationScheme(spec["m"], spec["n"], spec["eps"])
    raise ValueError(f"unknown scheme {t}")


def engine(spec):
    return lambda p: ql.QdFpAmericanEngine(p, scheme(spec["scheme"]), spec["fpEquation"])


def with_engine(cases, name, spec):
    for c in cases:
        c["name"] = f"{name}: {c['name']}"
        c["inputs"]["engine"] = spec
    return cases


def cases():
    out = []
    for name, s, equation in CONFIGS:
        spec = {"scheme": s, "fpEquation": int(equation)}
        out += with_engine(GRID.grid_cases(engine(spec), lambda p, date, option_type, strike: True, []), name, spec)
    defaults = {"scheme": ACCURATE, "fpEquation": int(E.Auto)}
    return out + with_engine(GRID.edge_cases(engine(defaults), [], []), "defaults", defaults)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/qdfpamericanengine.hpp", cases)
