# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's detail::GsrProcessCore (ql/processes/gsrprocesscore.hpp, .cpp), which the wheel doesn't
bind, each output computed by the C++ harness on a fresh core: the seven processes of gen/processes/gsrprocess.py
(constant and piecewise volatilities and reversions, a zero reversion among them, at forward measure times from 10 to
60), and at its points the core's members: expectation_x0dep_part, expectation_rn_part and expectation_tf_part at the
eight (w, xw, dt) steps, variance at the same steps, y, sigma and reversion at its seven times, and G at its eight (t,
w) pairs. The reversions stay at 0 or at |a| >= 1e-2, where nanoql keeps QuantLib's formulas.
tests/NanoQL.Tests/Vectors/Cxx/Processes/GsrProcessCoreCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_gsrprocess", os.path.join(v.GEN_ROOT, "processes", "gsrprocess.py"))
gp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(gp)


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 1, 2025))
        out = []
        for name, times, vols, reversions, T, _ in gp.PROCESSES:
            def call(member, x=0.0, y=0.0, z=0.0):
                return v.attempt(lambda: H.call("GsrProcessCore", times, vols, reversions, T, member, x, y, z))

            expected = {"expectation_x0dep_part": [call("expectation_x0dep_part", w, xw, dt) for w, xw, dt in gp.STEP_POINTS],
                        "expectation_rn_part": [call("expectation_rn_part", w, dt) for w, _, dt in gp.STEP_POINTS],
                        "expectation_tf_part": [call("expectation_tf_part", w, dt) for w, _, dt in gp.STEP_POINTS],
                        "variance": [call("variance", w, dt) for w, _, dt in gp.STEP_POINTS],
                        "y": [call("y", t) for t in gp.TIMES],
                        "sigma": [call("sigma", t) for t in gp.TIMES],
                        "reversion": [call("reversion", t) for t in gp.TIMES],
                        "G": [call("G", t, w) for t, w in gp.G_POINTS]}
            out.append({"name": name,
                        "inputs": {"settings": st, "times": times, "vols": vols, "reversions": reversions, "T": T,
                                   "stepPoints": gp.STEP_POINTS, "pointTimes": gp.TIMES, "gPoints": gp.G_POINTS},
                        "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/gsrprocesscore.hpp", cases)
