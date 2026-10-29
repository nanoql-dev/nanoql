# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's generalized Hull-White model and its Ornstein-Uhlenbeck process
(ql/experimental/shortrate/generalizedhullwhite.hpp, .cpp; generalizedornsteinuhlenbeckprocess.hpp, .cpp), which the
wheel doesn't bind, each output computed by the C++ harness (tools/cxx/src/experimental/shortrate/generalizedhullwhite.cpp).

- "model" cases: Hull-White's constant form at reversions 0.1, 0.05, 0.02 and 0.01 on a flat 3% curve, and the
  linear-flat form with three speed dates and two volatility dates, on a flat curve and on a log-linear discount curve:
  a() and sigma(), fixedReversion(), three bond options on the numerical B and V integrals, three discount bonds (A and
  B), the analytical fitting parameter φ at three times, and the tree on a ten-step grid to five years, its columns'
  node discounts; and the linear form, the template constructor on Linear interpolations, on the flat curve. Each
  model's dynamics() throws, and the linear-flat form gives LinearFlat's traits. Every reversion is at least 1e-2, where
  φ is QuantLib's formula (below, nanoql's expm1 form; the S-xrt plan's XRR5 (xix)); a fifth constant case at 0.005
  reads no φ.
- "process" cases: the process at constant speeds 0.1, 0.02 and 0.01 and volatility 0.01, its drift, diffusion,
  expectation, deviation and variance at two points, where QuantLib's variance formula holds (|a| ≥ 1e-2), and its
  speed and volatility at those times and its level.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/ShortRate/GeneralizedHullWhiteCxxVectorTests.cs, with
tests/NanoQL.Tests/Vectors/ModelInputs.cs's curves."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

A365 = {"type": "Actual365Fixed"}
CONTINUOUS, ANNUAL = 2, 1
REFERENCE = 46311  # Friday 16 October 2026

FLAT = {"type": "FlatForward", "referenceDate": REFERENCE, "forward": 0.03, "dayCounter": A365,
        "compounding": CONTINUOUS, "frequency": ANNUAL}
DISCOUNT = {"type": "DiscountCurve", "dates": [REFERENCE, REFERENCE + 365, REFERENCE + 1095, REFERENCE + 3650],
            "discounts": [1.0, 0.97, 0.905, 0.72], "dayCounter": A365}

QUERIES = {"bondOptions": [[1, 0.9, 1.0, 5.0], [-1, 0.95, 2.0, 4.0], [1, 0.85, 0.5, 6.0]],
           "discountBonds": [[0.0, 1.0, 0.03], [1.0, 3.0, 0.02], [2.0, 5.0, 0.04]],
           "phi": [0.5, 1.0, 5.0], "tree": [5.0, 10]}

LINEAR_FLAT = {"type": "linearFlat", "speedDates": [REFERENCE + 365, REFERENCE + 730, REFERENCE + 1825],
               "volDates": [REFERENCE + 365, REFERENCE + 1095], "speeds": [0.1, 0.05, 0.08], "vols": [0.01, 0.012]}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        models = [(f"constant a {a}", FLAT, {"type": "constant", "a": a, "sigma": 0.01}, QUERIES)
                  for a in (0.1, 0.05, 0.02, 0.01)]
        models.append(("constant a 0.005, no phi", FLAT, {"type": "constant", "a": 0.005, "sigma": 0.01},
                       dict(QUERIES, phi=[])))
        models.append(("linear-flat, flat curve", FLAT, LINEAR_FLAT, QUERIES))
        models.append(("linear-flat, discount curve", DISCOUNT, LINEAR_FLAT, QUERIES))
        models.append(("linear, flat curve", FLAT, dict(LINEAR_FLAT, type="linear"), QUERIES))
        for name, curve, model, queries in models:
            out.append({"name": f"model: {name}",
                        "inputs": {"settings": st, "curve": curve, "model": model, "queries": queries},
                        "expected": H.call("GeneralizedHullWhite::values", curve, model, queries)})
        points = [[0.0, 0.02, 0.25], [1.5, -0.01, 1.0]]
        for speed in (0.1, 0.02, 0.01):
            out.append({"name": f"process: speed {speed}",
                        "inputs": {"settings": st, "speed": speed, "vol": 0.01, "x0": 0.02, "level": 0.03,
                                   "points": points},
                        "expected": {"values": H.call("GeneralizedOrnsteinUhlenbeckProcess::values", speed, 0.01,
                                                      0.02, 0.03, points),
                                     **H.call("GeneralizedOrnsteinUhlenbeckProcess::members", speed, 0.01, 0.02, 0.03,
                                              [r[0] for r in points])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/shortrate/generalizedhullwhite.hpp", cases)
