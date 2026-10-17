# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's TridiagonalOperator (ql/methods/finitedifferences/tridiagonaloperator.hpp,
.cpp) that the wheel doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

Eight operators of 3 to 8 rows, half of them time-dependent, each with a right-hand side, SOR tolerances, times, a
scalar s and a partner of another size: isTimeDependent; the three diagonals' accessors; SOR; solveFor into another
Array and into its right-hand side itself; setTime at each time in turn; swap with the partner; the unary + and -,
s times the operator and the operator divided by s, each with whether the result is time-dependent. Then the size
constructor's sizes and its refusal of size 1; identity's refusal of size 0 and its operator of size 1; and the
refusals: of an operator of size 0 (solveFor, SOR), of a right-hand side of another size, of a first diagonal element
close to zero, of a later pivot that is zero, and of SOR's iteration limit, with SOR's results for a tolerance it can't
reach before the first sweep (0, negative, NaN) and for a system on which it diverges.

QuantLib defines no TimeSetter: the harness's ScaleByTime (tools/cxx/src/methods/finitedifferences/
tridiagonaloperator.cpp) multiplies every element of the three diagonals by the time, and
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/TridiagonalOperatorCxxVectorTests.cs, which reads this file
back, defines the same setter. An operator argument is the spec {"low", "mid", "high"}, with "timeSetter":
"scaleByTime" for a time-dependent one, or {"size": 0}."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TOLERANCES = [1e-6, 1e-12]
TIMES = [2.0, 0.5, -1.5]


def operator_spec(rnd, n, time_dependent):
    spec = {"low": [rnd.uniform(-1.0, 1.0) for _ in range(n - 1)],
            "mid": [rnd.uniform(2.5, 4.0) for _ in range(n)],
            "high": [rnd.uniform(-1.0, 1.0) for _ in range(n - 1)]}
    if time_dependent:
        spec["timeSetter"] = "scaleByTime"
    return spec


def diagonals(r):
    """An operator result, {"low", "mid", "high", "timeDependent"}, as its three diagonals."""
    return [v.enc(r["low"]), v.enc(r["mid"]), v.enc(r["high"])]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for k in range(8):
            n = 3 + k % 6
            spec = operator_spec(rnd, n, k % 2 == 1)
            partner = operator_spec(rnd, n + 2, k % 2 == 0)
            rhs = [rnd.uniform(-5.0, 5.0) for _ in range(n)]
            s = rnd.uniform(-3.0, 3.0)
            swapped = H.call("TridiagonalOperator::swap", spec, partner)
            results = {name: H.call(method, *params) for name, method, params in (
                ("positive", "operator+(const TridiagonalOperator&)", (spec,)),
                ("negative", "operator-(const TridiagonalOperator&)", (spec,)),
                ("scaledLeft", "operator*(Real,const TridiagonalOperator&)", (s, spec)),
                ("quotient", "operator/(const TridiagonalOperator&,Real)", (spec, s)))}
            expected = {
                "isTimeDependent": H.call("TridiagonalOperator::isTimeDependent", spec),
                "lowerDiagonal": v.enc(H.call("TridiagonalOperator::lowerDiagonal", spec)),
                "diagonal": v.enc(H.call("TridiagonalOperator::diagonal", spec)),
                "upperDiagonal": v.enc(H.call("TridiagonalOperator::upperDiagonal", spec)),
                "sor": [call("TridiagonalOperator::SOR", spec, rhs, tol) for tol in TOLERANCES],
                "solveForInto": call("TridiagonalOperator::solveFor(const Array&,Array&)", spec, rhs, False),
                "solveForInPlace": call("TridiagonalOperator::solveFor(const Array&,Array&)", spec, rhs, True),
                "setTime": [diagonals(r) for r in H.call("TridiagonalOperator::setTime", spec, TIMES)],
                "swapSizes": [swapped[0]["size"], swapped[1]["size"]],
                "swapFirst": diagonals(swapped[0]["operator"]),
                "swapSecond": diagonals(swapped[1]["operator"]),
                "swapTimeDependent": [swapped[0]["operator"]["timeDependent"],
                                      swapped[1]["operator"]["timeDependent"]]}
            for name, r in results.items():
                expected[name] = diagonals(r)
                expected[name + "TimeDependent"] = r["timeDependent"]
            out.append({"name": f"{k} size {n}{' time-dependent' if k % 2 == 1 else ''}",
                        "inputs": {"settings": st, "operator": v.enc(spec), "partner": v.enc(partner),
                                   "rhs": v.enc(rhs), "tolerances": TOLERANCES, "times": TIMES, "s": s},
                        "expected": expected})

        sizes = [0, 1, 2, 3, 7]
        identity_one = H.call("TridiagonalOperator::identity", 1)
        out.append({"name": "size constructor and identity",
                    "inputs": {"settings": st, "sizes": sizes},
                    "expected": {"sizeConstructor": [call("TridiagonalOperator(Size)", n) for n in sizes],
                                 "identityZero": call("TridiagonalOperator::identity", 0),
                                 "identityOne": diagonals(identity_one)}})

        dominant = {"low": [0.5, -0.3, 0.2], "mid": [3.0, 4.0, 3.5, 2.5], "high": [-0.4, 0.6, 0.1]}
        divergent = {"low": [1.0, 1.0, 1.0], "mid": [1.0, 1.0, 1.0, 1.0], "high": [1.0, 1.0, 1.0]}
        rhs = [1.0, 1.5, 2.0, 2.5]
        refusals = [
            ("size 0 solveFor", "TridiagonalOperator::solveFor(const Array&)", [{"size": 0}, []]),
            ("size 0 SOR", "TridiagonalOperator::SOR", [{"size": 0}, [], 1e-6]),
            ("solveFor of another size", "TridiagonalOperator::solveFor(const Array&)", [dominant, rhs[:-1]]),
            ("solveFor into another size", "TridiagonalOperator::solveFor(const Array&,Array&)",
             [dominant, rhs + [1.0], False]),
            ("SOR of another size", "TridiagonalOperator::SOR", [dominant, rhs[:-1], 1e-6]),
            ("first diagonal element zero", "TridiagonalOperator::solveFor(const Array&)",
             [{**dominant, "mid": [0.0, 4.0, 3.5, 2.5]}, rhs]),
            ("first diagonal element close to zero", "TridiagonalOperator::solveFor(const Array&)",
             [{**dominant, "mid": [1e-30, 4.0, 3.5, 2.5]}, rhs]),
            ("first diagonal element small but not close to zero", "TridiagonalOperator::solveFor(const Array&)",
             [{**dominant, "mid": [1e-27, 4.0, 3.5, 2.5]}, rhs]),
            ("later pivot zero", "TridiagonalOperator::solveFor(const Array&)",
             [{"low": [1.0], "mid": [1.0, 1.0], "high": [1.0]}, [1.0, 2.0]]),
            ("SOR iteration limit", "TridiagonalOperator::SOR", [dominant, rhs, 1e-300]),
            ("SOR tolerance 0", "TridiagonalOperator::SOR", [dominant, rhs, 0.0]),
            ("SOR negative tolerance", "TridiagonalOperator::SOR", [dominant, rhs, -1.0]),
            ("SOR tolerance NaN", "TridiagonalOperator::SOR", [dominant, rhs, math.nan]),
            ("SOR divergent", "TridiagonalOperator::SOR", [divergent, rhs, 1e-6])]
        for name, method, params in refusals:
            out.append({"name": name,
                        "inputs": {"settings": st, "method": method, "params": v.enc(params)},
                        "expected": {"result": call(method, *params)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/tridiagonaloperator.hpp", cases)
