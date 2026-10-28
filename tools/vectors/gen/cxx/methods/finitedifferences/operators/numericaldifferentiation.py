# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for NumericalDifferentiation (ql/methods/finitedifferences/operators/numericaldifferentiation.hpp, .cpp),
which the wheel doesn't bind, each output computed by the C++ harness: Fornberg's weights on given offsets, uniform and
not, symmetric and one-sided, for derivatives of order 0 to 4, and the derivative of four functions at five points; the
central, backward and forward schemes at step sizes 1e-3 and 0.1 and 3 to 9 steps; and the inputs QuantLib refuses (no
more points than the order; a central scheme of an even number of steps or fewer than three, a one-sided one of fewer
than two). Repeated offsets, which nanoql refuses where QuantLib's weights are NaN and its derivative drops them (the
S-core plan's CR7 (xxxviii); tests/NanoQL.Tests/Conventions/FiniteDifferenceRemainderConventionTests.cs), are left out.
Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Operators/NumericalDifferentiationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FUNCTIONS = ["sin(x)", "exp(x)", "x x x", "1/(1+x x)"]
AT = [-1.3, -0.2, 0.0, 0.7, 2.0]
OFFSETS = [[-0.1, 0.0, 0.1], [-0.2, -0.1, 0.0, 0.1, 0.2], [0.0, 0.05, 0.1, 0.15],
           [-0.13, -0.02, 0.0, 0.04, 0.11, 0.3], [-1e-3, -2e-4, 5e-4, 1e-3]]
FIELDS = ("offsets", "weights", "derivative")


def cases():
    out = []
    with cxx.connect() as H:
        def case(name, method, args, inputs):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = v.attempt(lambda: H.call(method, *args))
            out.append({"name": name, "inputs": dict({"settings": st, "method": method}, **inputs),
                        "expected": {k: r[k] for k in FIELDS} if "throws" not in r else {"construction": r}})

        for f in FUNCTIONS:
            for offsets in OFFSETS:
                for m in range(min(len(offsets), 5)):
                    case(f"{f}, order {m}, offsets {offsets}", "NumericalDifferentiation", [f, m, offsets, AT],
                         {"function": f, "order": m, "offsets": offsets, "at": AT})
            for scheme in ("Central", "Backward", "Forward"):
                for h in (1e-3, 0.1):
                    for steps in (3, 5, 9):
                        for m in (1, 2):
                            case(f"{f}, order {m}, {scheme}, step {h}, {steps} steps", "NumericalDifferentiationScheme",
                                 [f, m, h, steps, scheme, AT], {"function": f, "order": m, "stepSize": h,
                                                                 "steps": steps, "scheme": scheme, "at": AT})
        case("order 3 on three offsets (refused)", "NumericalDifferentiation", ["sin(x)", 3, OFFSETS[0], AT],
             {"function": "sin(x)", "order": 3, "offsets": OFFSETS[0], "at": AT})
        for scheme, steps in (("Central", 4), ("Central", 1), ("Backward", 1), ("Forward", 1)):
            case(f"{scheme}, {steps} steps (refused)", "NumericalDifferentiationScheme",
                 ["sin(x)", 1, 0.1, steps, scheme, AT],
                 {"function": "sin(x)", "order": 1, "stepSize": 0.1, "steps": steps, "scheme": scheme, "at": AT})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/numericaldifferentiation.hpp", cases)
