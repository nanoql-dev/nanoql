# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's StochasticProcessArray (ql/processes/stochasticprocessarray.hpp, .cpp) the
wheel doesn't expose, each output computed by the C++ harness: on stochasticprocessarray.py's arrays, apply at 10
states and changes, the time of 5 dates (the first process's: a Black-Scholes-Merton process's on its risk-free
curve's day counter, and the refusal of a process that converts no date), each process's x0 through process(i), and
the correlation the array's square root gives back, S·Sᵀ. tests/NanoQL.Tests/Vectors/Cxx/Processes/
StochasticProcessArrayCxxVectorTests.cs reads it back."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(_dir, "gen", "processes"))

import cxx  # noqa: E402
import stochasticprocessarray as spa  # noqa: E402
import vectors as v  # noqa: E402

DATE_OFFSETS = [0, 1, 30, 365, 3653]


def cases():
    with cxx.connect() as H:
        today = H.call("Date(Day,Month,Year)", 22, 10, 2026)
        st = H.settings(today)
        rnd = random.Random(v.SEED)
        out = []
        for name, corr in spa.CORRELATIONS.items():
            specs = [spa.process_input(*s) for s in spa.PROCESSES[name]]
            n = len(specs)

            def call(m, x=None, y=None):
                return v.attempt(lambda: H.call("StochasticProcessArray", specs, corr, today, m, x, y))

            states = [[[rnd.uniform(20.0, 120.0) for _ in range(n)], [rnd.gauss(0.0, 0.5) for _ in range(n)]]
                      for _ in range(10)]
            dates = [today + d for d in DATE_OFFSETS]
            out.append({"name": name,
                        "inputs": {"settings": st, "processes": specs, "correlation": corr, "states": states,
                                   "dates": dates},
                        "expected": {"apply": [call("apply", x, dx) for x, dx in states],
                                     "time": [call("time", d) for d in dates],
                                     "process": [call("process", i) for i in range(n)],
                                     "correlation": call("correlation")}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/stochasticprocessarray.hpp", cases)
