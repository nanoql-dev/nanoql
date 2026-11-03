# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's older CDO (ql/experimental/credit/cdo.hpp), which the wheel doesn't expose, each output
computed by the C++ harness (tools/cxx/src/experimental/credit/cdo.cpp): Gaussian copulas (a Student factor's range
differs), equity, mezzanine and senior tranches, sellers and buyers, given and repeated nominals, one and ten-year
integration steps, a forward schedule and an expired one; the NPV, the legs, the fair premium, the count of falling
losses, the nominal and the expiry; an expired CDO's legs, fair premium and count, which QuantLib leaves uninitialized,
read as null. Not lgd(), which QuantLib sums onto an uninitialized value (Divergences/OldCdoTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/CDOCxxVectorTests.cs."""
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


pf = _load("cxx_defaultevent", "cxx/experimental/credit/defaultevent.py")
dts = pf.dts
TODAY = pf.TODAY

D = pf.D
TARGET = {"type": "TARGET"}
A360 = {"type": "Actual360"}
DISCOUNT = dts.discount_spec(TODAY, 0.05)
GAUSS = {"kind": "Gaussian", "correlation": 0.3}


def schedule(start, end, months=3):
    return {"effectiveDate": start.serialNumber(), "terminationDate": end.serialNumber(), "tenor": [months, pf.MONTHS],
            "calendar": TARGET, "convention": 0, "terminationDateConvention": 0, "rule": 0}


def curves(hazards):
    return [pf.flat(h) for h in hazards]


H10 = [0.01, 0.02, 0.015, 0.03, 0.025, 0.01, 0.02, 0.015, 0.03, 0.02]
FIVE = schedule(D(1, 9, 2006), D(1, 9, 2011))
CASES = [
    ("Gaussian seller, [3%, 7%], one-year steps", (GAUSS, 0.03, 0.07, [10.0] * 10, curves(H10), True, FIVE, 0.02, A360,
                                                   0.4, 0.0, DISCOUNT, 50, [1, pf.YEARS])),
    ("Gaussian buyer, equity, repeated nominals, an upfront", (GAUSS, 0.0, 0.03, [10.0, 20.0], curves(H10), False, FIVE,
                                                               0.05, A360, 0.35, 0.02, DISCOUNT, 100, [10, pf.YEARS])),
    ("Gaussian seller, senior, half-year steps", (GAUSS, 0.07, 0.3, [10.0] * 10, curves(H10), True, FIVE, 0.01, A360, 0.4,
                                                  0.0, DISCOUNT, 50, [6, pf.MONTHS])),
    ("Gaussian, forward start", (GAUSS, 0.03, 0.07, [10.0] * 10, curves(H10), True,
                                 schedule(D(1, 3, 2007), D(1, 3, 2012)), 0.02, A360, 0.4, 0.01, DISCOUNT, 50,
                                 [1, pf.YEARS])),
    ("Gaussian, expired", (GAUSS, 0.03, 0.07, [10.0] * 10, curves(H10), True, schedule(D(1, 9, 2004), D(1, 6, 2006)),
                           0.02, A360, 0.4, 0.0, DISCOUNT, 50, [1, pf.YEARS])),
]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, args in CASES:
            with H.case_settings(TODAY.serialNumber()) as st:
                r = call("CDO::calculate", *args)
                if isinstance(r, list) and r[-1] is True:
                    r[1:5] = [None] * 4
                out.append({"name": name, "inputs": {"settings": st, "args": list(args)},
                            "expected": {"calculate": r}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/cdo.hpp", cases)
