# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SpreadedOptionletVolatility (ql/termstructures/volatility/optionlet/spreadedoptionletvol.hpp),
which the wheel doesn't bind, each output computed by the C++ harness: spreads of -2%, 0 and 1.5% over constant
optionlet volatilities (on a fixed reference date and on two settlement days, shifted lognormal with a displacement and
normal, one with extrapolation enabled) and over the adapters of two strippers of
cxx/termstructures/volatility/optionlet/optionletstripper.py (one extrapolating), on 16 June 2025.

Each case's outputs are the structure's inspectors, each its own output (its base's dates, strikes, type, displacement, settlement days or
{"throws": true}, convention, and its extrapolation flag, the base's copied at construction), its volatility at 10
points by time and 4 by date, with and without the extrapolation flag, and its smile section's volatilities at 7 strikes
at two times. A base is {"kind": "structure", "structure": a constant optionlet volatility of
termstructures/volatility/optionlet/constantoptionletvol.py} or {"kind": "adapter", "stripper", "enableExtrapolation"}.
Read back by tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SpreadedOptionletVolatilityCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ops = _load("cxx_optionletstripper", "cxx/termstructures/volatility/optionlet/optionletstripper.py")
ql = ops.ql
TODAY = ops.JUN25
A365 = {"type": "Actual365Fixed"}


def constant(volatility, reference=None, days=None, vol_type=ql.ShiftedLognormal, displacement=0.0, extrapolate=False):
    out = {"type": "ConstantOptionletVolatility"}
    out.update({"referenceDate": v.enc(reference)} if reference is not None else {"settlementDays": days})
    out.update({"calendar": ops.TARGET, "convention": int(ql.Following), "volatility": volatility, "dayCounter": A365,
                "volatilityType": int(vol_type), "displacement": displacement, "enableExtrapolation": extrapolate})
    return out


def bases():
    by_name = {row[0]: row for row in ops.strippers()}
    return [
        ("constant, fixed", {"kind": "structure", "structure": constant(0.2, reference=TODAY)}),
        ("constant, two settlement days", {"kind": "structure", "structure": constant(0.25, days=2)}),
        ("constant, displacement 1%, extrapolating",
         {"kind": "structure", "structure": constant(0.3, reference=TODAY, displacement=0.01, extrapolate=True)}),
        ("constant, normal", {"kind": "structure", "structure": constant(0.008, days=0, vol_type=ql.Normal)}),
        ("adapter", {"kind": "adapter", "stripper": by_name["survey surface"][3], "enableExtrapolation": False}),
        ("adapter, extrapolating", {"kind": "adapter", "stripper": by_name["survey surface, Euribor 3M, lag 1"][3],
                                    "enableExtrapolation": True}),
    ]


def case(H, name, base, spread):
    with H.case_settings(v.enc(TODAY)) as st:
        inspectors = H.call("SpreadedOptionletVolatility::inspectors", base, spread)
        t1 = min(inspectors["maxTime"], 5.0)
        k0, k1 = max(inspectors["minStrike"], -0.01), min(inspectors["maxStrike"], 0.08)
        by_time = [[t1 * f, k0 + (k1 - k0) * g, False] for f, g in ((0.1, 0.5), (0.5, 0.2), (0.9, 0.8), (1.0, 0.5))]
        by_time += [[t1 + 1.0, 0.5 * (k0 + k1), False], [t1 + 1.0, 0.5 * (k0 + k1), True], [0.5, k1 + 0.01, False],
                    [0.5, k1 + 0.01, True], [0.5, k0 - 0.005, True], [0.0, 0.5 * (k0 + k1), False]]
        ref = inspectors["referenceDate"]
        by_date = [[ref + 200, 0.5 * (k0 + k1), False], [ref + 700, k0, False], [ref + 4000, k1, False],
                   [ref + 4000, k1, True]]
        strikes = [k0 - 0.005, k0, 0.25 * (3 * k0 + k1), 0.5 * (k0 + k1), 0.25 * (k0 + 3 * k1), k1, k1 + 0.02]
        times = [0.25 * t1, 0.75 * t1]
        return {"name": f"{name}, spread {spread}",
                "inputs": {"settings": st, "base": base, "spread": spread, "byTime": by_time, "byDate": by_date,
                           "times": times, "strikes": strikes},
                "expected": {
                    **inspectors,
                    "volatilityByTime": [v.attempt(lambda p=p: H.call("SpreadedOptionletVolatility::volatility(Time)",
                                                                      base, spread, *p)) for p in by_time],
                    "volatilityByDate": [v.attempt(lambda p=p: H.call("SpreadedOptionletVolatility::volatility(Date)",
                                                                      base, spread, *p)) for p in by_date],
                    "smileSections": [v.attempt(lambda t=t: H.call("SpreadedOptionletVolatility::smileSection",
                                                                   base, spread, t, strikes)) for t in times]}}


def cases():
    with cxx.connect() as H:
        return [case(H, name, base, s) for name, base in bases() for s in (-0.02, 0.0, 0.015)]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/spreadedoptionletvol.hpp", cases)
