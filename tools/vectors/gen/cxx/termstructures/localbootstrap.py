# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LocalBootstrap (ql/termstructures/localbootstrap.hpp), which the wheel doesn't expose, each
output computed by the C++ harness: piecewise convex-monotone curves on the local bootstrap.

On QuantLib's test fixture's helpers (6 deposits on Euribor indexes and 15 swaps on Euribor 6M,
tools/vectors/gen/termstructures/yield/piecewiseyieldcurve.py's fixture) and on its 3 deposits and 9 swaps, under the
first two of that generator's evaluation dates, each curve with a fixed reference date two TARGET days after the
evaluation date: forward rates, as QuantLib's test builds them, on its default ConvexMonotone at localisations of 2
and 3, with positivity forced and not, at the curve's accuracy and at 1e-10 (zero rates on the local bootstrap, which
QuantLib's own code doesn't build, stall at its 100 iterations on most of these sets, and are left out). The curve's
nodes' dates and data, its maximum date, every helper's quote error, and the discounts at yearly dates from its
reference date and past its maximum date, extrapolated. A case's inputs are its settings, "curve" ({"traits",
"quadraticity", "monotonicity", "forcePositive", "referenceDate", "dayCounter"}), "helpers" (helper specs of
termstructures/yield/ratehelpers.py), "bootstrap" ({"localisation", "forcePositive", "accuracy"}) and "discountDates",
as tools/cxx/src/termstructures/localbootstrap.cpp documents them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/LocalBootstrapCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)
rh = pw.rh


def short():
    """3 deposits and 9 swaps."""
    return pw.SHORT_DEPOSITS + [rh.swap_tenor(rate, (n, ql.Years)) for n, rate in pw.SWAP_YEARS]


def cases():
    out = []
    with cxx.connect() as H:
        for today in pw.EVALUATION_DATES[:2]:
            spot = ql.TARGET().advance(today, 2, ql.Days)
            for set_name, helpers in (("fixture", pw.fixture()), ("short", short())):
                st = H.settings(today.serialNumber())
                rows = [("ForwardRate", 2, True, None), ("ForwardRate", 3, True, None), ("ForwardRate", 2, False, None),
                        ("ForwardRate", 2, True, 1e-10)]
                for traits, loc, fp, accuracy in rows:
                    curve = {"traits": traits, "quadraticity": 0.3, "monotonicity": 0.7, "forcePositive": True,
                             "referenceDate": v.enc(spot), "dayCounter": pw.A360}
                    bootstrap = {"localisation": loc, "forcePositive": fp, "accuracy": accuracy}
                    years = 29 if set_name == "fixture" else 14
                    dates = [v.enc(spot + ql.Period(k, ql.Years)) for k in range(years + 1)]
                    dates += [v.enc(spot + ql.Period(k, ql.Years)) for k in (35, 50)]
                    out.append({"name": f"{today.ISO()} {set_name} {traits} localisation {loc} forcePositive {fp} "
                                        f"accuracy {accuracy}",
                                "inputs": {"settings": st, "curve": curve, "helpers": helpers,
                                           "bootstrap": bootstrap, "discountDates": dates},
                                "expected": v.enc(H.call("LocalBootstrap::curve", curve, helpers, bootstrap,
                                                         dates))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/localbootstrap.hpp", cases)
