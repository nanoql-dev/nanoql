# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for AdaptiveRungeKutta<std::complex<Real>> (ql/math/ode/adaptiverungekutta.hpp), which the wheel doesn't
instantiate, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the solution f(x2) of
f'(x) = F(x, f(x)) with f(x1) = y1, for the harness's named complex equations (tools/cxx/src/math/ode/
adaptiverungekutta.cpp: four scalar ones, test-suite/ode.cpp's rotation among them, and two vector ones, its
oscillator among them), each from initial values with a zero real or imaginary part and with both, to three points,
under four settings (eps, h1, hmin): QuantLib's defaults (1e-6, 1e-4, 0), test-suite/ode.cpp's (1e-12, 1e-4, 0),
(1e-8, 1e-5, 0) and a loose one, (1e-3, 1e-2, 0), whose steps the magnitudes of complex values set most; the scalar
equations also in the vector form, on vectors of one value; then integrations backward and of length zero, and the
failures: a step at or below hmin and too many steps (10000).

A complex value is [real, imaginary]. Each case's inputs hold the equation's name ("equation"), whether it is
integrated in the vector form ("vector"), the initial value ("y1", a list of complex values in the vector form),
"x1", "x2" and the settings ("integrator": [eps, h1, hmin]); its output "y" is the solution, or {"throws": true}.
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Ode/AdaptiveRungeKuttaCxxVectorTests.cs reads the file back, with the
equations of tests/NanoQL.Tests/Vectors/Numerics/Ode/OdeEquations.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SCALAR_METHOD = "AdaptiveRungeKutta<std::complex<Real>>::operator()(OdeFct1d)"
VECTOR_METHOD = "AdaptiveRungeKutta<std::complex<Real>>::operator()(OdeFct)"

SETTINGS = [[1e-6, 1e-4, 0.0], [1e-12, 1e-4, 0.0], [1e-8, 1e-5, 0.0], [1e-3, 1e-2, 0.0]]
# (equation, initial values, x1, points)
SCALAR = [
    ("rotation", [[0.0, 1.0], [1.0, 0.0], [0.6, -0.8]], 0.0, [0.5, 2.0, 5.0]),
    ("dampedRotation", [[1.0, 0.0], [0.0, -2.0], [1.5, 0.5]], 0.0, [0.25, 1.0, 4.0]),
    ("chirp", [[1.0, 0.0], [0.0, 1.0], [0.3, 0.4]], -1.0, [0.0, 1.0, 3.0]),
    ("decay", [[0.0, 0.0], [2.0, 0.0], [-1.0, 3.0]], 0.0, [0.1, 1.0, 6.0]),
]
VECTOR = [
    ("oscillator", [[[1.0, 0.0], [0.0, 1.0]], [[0.0, 1.0], [1.0, 0.0]], [[0.5, -0.5], [-1.0, 2.0]]], 0.0,
     [1.0, 3.0, 5.0]),
    ("coupled", [[[1.0, 0.0], [0.0, 0.0]], [[0.0, 1.0], [1.0, -1.0]], [[0.2, 0.3], [0.4, 0.5]]], 0.0,
     [0.5, 2.0, 8.0]),
]
# (label, equation, vector form, y1, x1, x2, settings)
SPECIAL = [
    ("backward", "rotation", False, [0.0, 1.0], 2.0, -1.0, [1e-10, 1e-4, 0.0]),
    ("backward", "oscillator", True, [[1.0, 0.0], [0.0, 1.0]], 5.0, 0.0, [1e-10, 1e-3, 0.0]),
    ("length zero", "chirp", False, [1.0, 2.0], 1.0, 1.0, [1e-6, 1e-4, 0.0]),
    ("length zero", "coupled", True, [[1.0, 2.0], [3.0, 4.0]], -2.0, -2.0, [1e-6, 1e-4, 0.0]),
    ("step at hmin", "rotation", False, [0.0, 1.0], 0.0, 5.0, [1e-10, 1e-4, 0.5]),
    ("step at hmin", "oscillator", True, [[1.0, 0.0], [0.0, 1.0]], 0.0, 5.0, [1e-10, 1e-4, 0.1]),
    ("too many steps", "rotation", False, [0.0, 1.0], 0.0, 1000.0, [1e-12, 1e-4, 0.0]),
    ("too many steps", "coupled", True, [[1.0, 0.0], [0.0, 0.0]], 0.0, 2000.0, [1e-12, 1e-4, 0.0]),
]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(label, equation, vector, y1, x1, x2, settings):
            method = VECTOR_METHOD if vector else SCALAR_METHOD
            return {"name": f"{label} / {equation}{' (vector form)' if vector else ''} {y1} from {x1!r} to {x2!r} / "
                            f"{settings}",
                    "inputs": {"settings": st, "equation": equation, "vector": vector, "y1": v.enc(y1), "x1": x1,
                               "x2": x2, "integrator": settings},
                    "expected": {"y": v.attempt(lambda: H.call(method, *settings, equation, y1, x1, x2))}}

        out = []
        for equation, starts, x1, points in SCALAR:
            for y1 in starts:
                for settings in SETTINGS:
                    for x2 in points:
                        out.append(case("integration", equation, False, y1, x1, x2, settings))
        for equation, starts, x1, points in SCALAR:
            for settings in SETTINGS:
                out.append(case("integration", equation, True, [starts[-1]], x1, points[1], settings))
        for equation, starts, x1, points in VECTOR:
            for y1 in starts:
                for settings in SETTINGS:
                    for x2 in points:
                        out.append(case("integration", equation, True, y1, x1, x2, settings))
        out += [case(*row) for row in SPECIAL]
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/ode/adaptiverungekutta.hpp", cases)
