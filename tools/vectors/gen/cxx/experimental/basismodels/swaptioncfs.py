# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's swaption cash flows (ql/experimental/basismodels/swaptioncfs.hpp, .cpp), which the wheel
doesn't bind, each output computed by the C++ harness (tools/cxx/src/experimental/basismodels/basismodels.cpp,
"SwaptionCashFlows::flows").

- "flows" cases: test-suite/basismodels.cpp's markets on 15 June 2026 (its discount and Euribor 6M projection curves,
  cubic in zero rates), its 5-into-10-year payer swaption and a 1-into-5 and a 2-into-3, each mapped on the discount
  curve and on the projection curve (the single-curve setting), with continuously and simply compounded spreads: the
  floating and fixed times and weights, the annuity weights and the exercise times.

The market and spec functions here are shared by tools/vectors/gen/cxx/experimental/basismodels/tenoroptionletvts.py
and tenorswaptionvts.py, which import this file. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/BasisModels/SwaptionCashFlowsCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

EVAL = ql.Date(15, ql.June, 2026)
M, Y, D = int(ql.Months), int(ql.Years), int(ql.Days)

# test-suite/basismodels.cpp's auxiliary data
TERMS = [ql.Period(0, ql.Days)] + [ql.Period(n, ql.Years) for n in (1, 2, 3, 5, 7, 10, 15, 20, 61)]
DISC_RATES = [-0.00147407, -0.001761684, -0.001736745, -0.00119244, 0.000896055, 0.003537077, 0.007213824,
              0.011391278, 0.013334611, 0.013982809]
PROJ3M_RATES = [-0.000483439, -0.000578569, -0.000383832, 0.000272656, 0.002478699, 0.005100113, 0.008750643,
                0.012788095, 0.014534052, 0.014942896]
PROJ6M_RATES = [0.000233608, 0.000218862, 0.000504018, 0.001240556, 0.003554415, 0.006153921, 0.009688264,
                0.013521628, 0.015136391, 0.015377704]


def curve_spec(rates, spread=0.0):
    """The test's getYTS: a cubic zero curve on Actual/365 (Fixed), its dates the terms from the evaluation date."""
    return {"dates": [v.enc(ql.NullCalendar().advance(EVAL, t, ql.Unadjusted)) for t in TERMS],
            "rates": [r + spread for r in rates]}


def index_spec(tenor, curve):
    return {"tenor": tenor, "curve": curve}


DISC = curve_spec(DISC_RATES)
PROJ6M = curve_spec(PROJ6M_RATES)


def swap_spec(start_years, length_years):
    start = ql.TARGET().advance(EVAL, ql.Period(start_years, ql.Years), ql.Following)
    end = ql.TARGET().advance(start, ql.Period(length_years, ql.Years), ql.Following)
    exercise = ql.TARGET().advance(start, ql.Period(-2, ql.Days), ql.Preceding)
    return {"start": v.enc(start), "end": v.enc(end), "nominal": 10000.0, "fixedRate": 0.03}, v.enc(exercise)


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(v.enc(EVAL))
        for start, length in ((5, 10), (1, 5), (2, 3)):
            swap, exercise = swap_spec(start, length)
            for label, disc in (("two curves", DISC), ("single curve", PROJ6M)):
                for cont in (True, False):
                    out.append({"name": f"{start} into {length} years, {label}, continuous {cont}",
                                "inputs": {"settings": st, "discount": disc, "projection": PROJ6M, "swap": swap,
                                           "exercise": exercise, "contTenorSpread": cont},
                                "expected": H.call("SwaptionCashFlows::flows", disc, PROJ6M, swap, exercise, cont)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/basismodels/swaptioncfs.hpp", cases)
