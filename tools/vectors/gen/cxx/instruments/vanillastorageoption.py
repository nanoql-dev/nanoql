# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VanillaStorageOption (ql/instruments/vanillastorageoption.hpp) through the C++ harness, which
the wheel doesn't bind.

- "validate" cases: the arguments' validation of a storage option on a Bermudan exercise: valid quantities (a load of
  zero, a load and a change rate at the capacity among them), and QuantLib's refusals of a capacity, a change rate or
  a load out of range ("positive capacity, load and change rate required") and of a load or a change rate above the
  capacity ("illegal values load of changeRate").
- "isExpired" cases: the option's expiry under evaluation dates before, on and after its last exercise date, with the
  reference date's events included and not.

A case holds the exercise "dates" (serials), "capacity", "load" and "changeRate", and its settings. The harness's
calls: VanillaStorageOption::validate and VanillaStorageOption::isExpired
(tools/cxx/src/instruments/vanillastorageoption.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/VanillaStorageOptionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
DATES = [R + 30, R + 61, R + 91, R + 122]
QUANTITIES = [
    ("valid", 100.0, 50.0, 10.0),
    ("an empty store", 100.0, 0.0, 10.0),
    ("full, changing at the capacity", 100.0, 100.0, 100.0),
    ("no capacity", 0.0, 0.0, 10.0),
    ("no change rate", 100.0, 50.0, 0.0),
    ("a negative load", 100.0, -1.0, 10.0),
    ("a load above the capacity", 100.0, 120.0, 10.0),
    ("a change rate above the capacity", 100.0, 50.0, 150.0),
]
EVALUATIONS = [(-10, False), (91, False), (122, False), (122, True), (123, False)]


def cases():
    out = []
    with cxx.connect() as H:
        def attempt(f):
            try:
                return {"value": v.enc(f())}
            except RuntimeError as e:
                return {"error": str(e)}

        st = H.settings(R)
        for name, capacity, load, rate in QUANTITIES:
            out.append({"name": f"validate: {name}",
                        "inputs": {"settings": st, "dates": DATES, "capacity": capacity, "load": load,
                                   "changeRate": rate},
                        "expected": attempt(lambda: H.call("VanillaStorageOption::validate", DATES, capacity, load,
                                                           rate))})
        for days, include in EVALUATIONS:
            with H.case_settings(R + days, include_reference_date_events=include) as st:
                out.append({"name": f"isExpired: {days} days, reference date's events {'in' if include else 'out'}",
                            "inputs": {"settings": st, "dates": DATES, "capacity": 100.0, "load": 50.0,
                                       "changeRate": 10.0},
                            "expected": attempt(lambda: H.call("VanillaStorageOption::isExpired", DATES, 100.0, 50.0,
                                                               10.0))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/vanillastorageoption.hpp", cases)
