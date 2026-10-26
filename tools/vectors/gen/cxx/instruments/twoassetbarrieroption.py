# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TwoAssetBarrierOption (ql/instruments/twoassetbarrieroption.hpp, .cpp), whose expiry,
argument validation and trigger the wheel doesn't expose, each output computed by the C++ harness: isExpired with the
exercise a day before, on and a day after the evaluation date 15 October 2026, under includeReferenceDateEvents false
and true; the arguments' validation for each barrier type, a value outside the type ("unknown type") and a null
barrier ("no barrier given"); and the engine's trigger for each type below, on and above the barrier.

The option, arguments and engine are tools/cxx/src/instruments/twoassetbarrieroption.cpp's. nanoql's barrier is a
double the constructor takes, so the null barrier is the harness's alone and isn't read back; its constructor refuses
the type outside the enum, where QuantLib's validation does. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/TwoAssetBarrierOptionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
TYPES = [("down-and-in", 0), ("up-and-in", 1), ("down-and-out", 2), ("up-and-out", 3)]


def cases():
    out = []
    with cxx.connect() as H:
        for include in (False, True):
            st = H.settings(R, include)
            for offset in (-1, 0, 1):
                out.append({"name": f"isExpired, the exercise at the evaluation date {offset:+d}, "
                                    f"includeReferenceDateEvents {str(include).lower()}",
                            "inputs": {"settings": st, "exerciseDate": R + offset},
                            "expected": {"isExpired": H.call("TwoAssetBarrierOption::isExpired", R + offset)}})
        st = H.settings(R)
        for tname, t in TYPES + [("a type outside the enum", 7)]:
            out.append({"name": f"validate, {tname}", "inputs": {"settings": st, "barrierType": t, "barrier": 90.0},
                        "expected": {"valid": v.attempt(lambda t=t: H.call(
                            "TwoAssetBarrierOption::arguments::validate", t, 90.0))}})
        for tname, t in TYPES:
            for underlying in (89.0, 90.0, 91.0):
                out.append({"name": f"triggered, {tname}, the underlying at {underlying:g}",
                            "inputs": {"settings": st, "barrierType": t, "barrier": 90.0, "underlying": underlying},
                            "expected": {"triggered": H.call("TwoAssetBarrierOption::engine::triggered", t, 90.0,
                                                             underlying)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/twoassetbarrieroption.hpp", cases)
