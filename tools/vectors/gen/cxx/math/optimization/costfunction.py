# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for CostFunction's defaults and SimpleCostFunction (ql/math/optimization/costfunction.hpp), each output
computed by the C++ harness (docs/porting-playbook.md §9.4): the harness's named cost functions rosenbrock,
polynomialFit and beale (tools/cxx/src/math/optimization/specs.hpp) override only values(), so value (the root mean
square of the values), the central-difference gradient and Jacobian, valueAndGradient, valuesAndJacobian and
finiteDifferenceEpsilon are QuantLib's defaults. For each, value, gradient and jacobian at ten points, and
valueAndGradient and valuesAndJacobian at the first three; for SimpleCostFunction over the named values function
circle, its values, value, gradient and jacobian at five points; and each function's finiteDifferenceEpsilon.

tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/CostFunctionCxxVectorTests.cs reads the file back, with the
same functions (OptimizationSpecs.cs)."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

NAMED = {"rosenbrock": 2, "polynomialFit": 3, "beale": 2}
POINTS = 10
BOTH = 3
CIRCLE_POINTS = 5


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        for name, n in NAMED.items():
            spec = {"type": name}
            for i in range(POINTS):
                x = [rnd.uniform(-2.0, 3.0) for _ in range(n)]
                expected = {"value": v.enc(H.call("CostFunction::value", spec, x)),
                            "gradient": v.enc(H.call("CostFunction::gradient", spec, x)),
                            "jacobian": v.enc(H.call("CostFunction::jacobian", spec, x))}
                if i < BOTH:
                    vg = H.call("CostFunction::valueAndGradient", spec, x)
                    vj = H.call("CostFunction::valuesAndJacobian", spec, x)
                    expected.update({"valueAndGradient": v.enc(vg["value"]),
                                     "valueAndGradientGradient": v.enc(vg["gradient"]),
                                     "valuesAndJacobian": v.enc(vj["values"]),
                                     "valuesAndJacobianJacobian": v.enc(vj["jacobian"])})
                if i == 0:
                    expected["finiteDifferenceEpsilon"] = H.call("CostFunction::finiteDifferenceEpsilon", spec)
                out.append({"name": f"{name} point {i}", "inputs": {"settings": st, "costFunction": spec, "x": x},
                            "expected": expected})
        spec = {"type": "SimpleCostFunction", "values": "circle"}
        for i in range(CIRCLE_POINTS):
            x = [rnd.uniform(-2.0, 2.0) for _ in range(2)]
            expected = {"values": v.enc(H.call("CostFunction::values", spec, x)),
                        "value": v.enc(H.call("CostFunction::value", spec, x)),
                        "gradient": v.enc(H.call("CostFunction::gradient", spec, x)),
                        "jacobian": v.enc(H.call("CostFunction::jacobian", spec, x))}
            if i == 0:
                expected["finiteDifferenceEpsilon"] = H.call("CostFunction::finiteDifferenceEpsilon", spec)
            out.append({"name": f"SimpleCostFunction circle point {i}",
                        "inputs": {"settings": st, "costFunction": spec, "x": x},
                        "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/costfunction.hpp", cases)
