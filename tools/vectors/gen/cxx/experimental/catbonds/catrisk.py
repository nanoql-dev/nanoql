# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's catastrophe risks (ql/experimental/catbonds/catrisk.hpp), which the wheel doesn't expose,
each output computed by the C++ harness (tools/cxx/src/experimental/catbonds/catrisk.cpp).

- "event set" cases: every path of an event set's simulation, on QuantLib's test's events and on denser ones, for
  whole years, irregular periods and periods longer than the set allows; and on no events.
- "beta risk" cases: BetaRisk's first paths on MSVC's streams, the harness's toolchain, as nanoql draws them:
  QuantLib's events arrive by fresh draws where nanoql cumulates them
  (tests/NanoQL.Tests/Divergences/BetaRiskTests.cs), so each case stops before the first path where the two can part, a
  path whose cumulated time differs from QuantLib's next draw: on QuantLib's testBetaRisk risk 40 paths (its path 42 is
  its first of two events), and on long return periods where the paths hold one event or none.
- "beta" cases: losses drawn by the simulation's generateBeta alone, no events, at shapes reaching each of the gamma's
  branches: below 1, at 1, integers below 20 and the tangent method above.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/CatBonds/CatRiskCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def d(day, month, year):
    return ql.Date(day, month, year).serialNumber()


SAMPLE = [[d(1, 2, 2012), 100], [d(1, 7, 2013), 150], [d(5, 1, 2014), 50]]
DENSE = [[d(15, 3, 2001), 20], [d(1, 9, 2001), 70], [d(30, 12, 2002), 5], [d(1, 1, 2003), 40], [d(14, 7, 2004), 90],
         [d(31, 10, 2005), 12], [d(28, 2, 2006), 33], [d(1, 3, 2006), 8], [d(15, 6, 2008), 61]]
EVENT_SETS = [
    ("test events, whole years", SAMPLE, d(1, 1, 2011), d(31, 12, 2014), d(1, 1, 2015), d(31, 12, 2015)),
    ("test events, irregular", SAMPLE, d(1, 1, 2011), d(31, 12, 2014), d(2, 1, 2015), d(5, 1, 2016)),
    ("no events", [], d(1, 1, 2011), d(31, 12, 2014), d(2, 1, 2015), d(5, 1, 2016)),
    ("dense, a year", DENSE, d(1, 1, 2001), d(31, 12, 2009), d(1, 4, 2015), d(31, 3, 2016)),
    ("dense, three years from June", DENSE, d(1, 1, 2001), d(31, 12, 2009), d(15, 6, 2020), d(15, 6, 2023)),
    ("dense, longer than the set", DENSE, d(1, 1, 2001), d(31, 12, 2009), d(1, 1, 2020), d(1, 1, 2031)),
]
BETA = [
    ("testBetaRisk's risk", [100.0, 100.0, 10.0, 15.0, d(2, 1, 2015), d(2, 1, 2018), 40]),
    ("a fifty-year return period over four years", [5000.0, 1000.0, 500.0, 500.0, d(30, 11, 2004), d(30, 11, 2008),
                                                    200]),
    ("a small mean, a short period", [10.0, 500.0, 0.5, 0.4, d(1, 1, 2020), d(1, 7, 2020), 200]),
]
BETA_SHAPES = [(0.3, 2.7), (1.0, 3.0), (2.0, 19.0), (20.0, 0.5), (25.5, 1.0), (0.999, 1.5), (7.0, 7.0)]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(d(15, 1, 2024)) as st:
            for label, events, es, ee, start, end in EVENT_SETS:
                args = [events, es, ee, start, end]
                out.append({"name": f"event set {label}", "inputs": {"settings": st, "kind": "eventSet", "args": args},
                            "expected": {"paths": call("EventSet::paths", *args)}})
            for alpha, beta in BETA_SHAPES:
                args = [d(2, 1, 2015), d(2, 1, 2018), 100.0, 0.01, alpha, beta, 300]
                out.append({"name": f"betas alpha {alpha} beta {beta}",
                            "inputs": {"settings": st, "kind": "betas", "args": args},
                            "expected": {"betas": call("BetaRiskSimulation::betas", *args)}})
            for label, args in BETA:
                out.append({"name": f"beta risk {label}", "inputs": {"settings": st, "kind": "betaRisk", "args": args},
                            "expected": {"paths": call("BetaRisk::paths", *args)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/catbonds/catrisk.hpp", cases)
