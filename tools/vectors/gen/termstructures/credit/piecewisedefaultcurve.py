# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's piecewise default curve (ql/termstructures/credit/piecewisedefaultcurve.hpp), through the
wheel's PiecewiseFlatHazardRate (hazard rates, backward-flat, iterative bootstrap) on
tools/vectors/gen/termstructures/credit/defaultprobabilityhelpers.py's helper sets.

- "curve" cases: each helper set's curve on its evaluation date, and the 2015-rule spread set's curve after 0 and
  1 business days on WeekendsOnly and TARGET (the settlement-days constructor): the curve's reference and maximum
  dates, its node dates, times and hazard rates, and its survival probability and hazard rate at 20 dates to 12 years.

The data and the values are solved, so the stability filter of the S-infl plan's SID20 (the S7 plan's S7R6) runs on
them, as in the helpers' file; a dropped output is written null and its index listed in the case's inputs "dropped".
The default-density and survival-probability traits' nodes, which the wheel doesn't bind, are in the harness's
cxx/termstructures/credit/piecewisedefaultcurve. A case's inputs are the helpers' file's, with the sample dates. Read
back by tests/NanoQL.Tests/Vectors/TermStructures/Credit/PiecewiseDefaultCurveVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_defaultprobabilityhelpers",
    os.path.join(v.GEN_ROOT, "termstructures", "credit", "defaultprobabilityhelpers.py"))
dph = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dph)
dts = dph.dts


def curves():
    out = [(label, ev, build, dph.curve_spec(ev)) for label, ev, build in dph.SETS]
    label, ev, build = dph.SETS[0]
    for days in (0, 1):
        for cal in (dts.WEEKENDS_ONLY, dts.TARGET):
            curve = {"referenceDate": None, "settlementDays": days, "calendar": cal, "dayCounter": dts.A365}
            out.append((f"{label}, {days} days on {cal['type']}", dph.SATURDAY, build, curve))
    return out


def make_cases():
    out = []
    tally = [0, 0]
    for k, (label, ev, build, curve) in enumerate(curves()):
        specs = build(ev)
        sample = [ev + 30 + 220 * n for n in range(20)]

        def solved(built, helpers, sample=sample):
            return {"data": [n[1] for n in built.nodes()],
                    "survivalProbability": [built.survivalProbability(d, True) for d in sample],
                    "hazardRate": [built.hazardRate(d, True) for d in sample]}
        base, dropped = dph.filtered(ev, specs, curve, solved, tally)
        print(f"curve {label}: dropped {sum(len(d) for d in dropped.values())} outputs")
        with v.case_settings(ev) as st:
            helpers = [dph.make_helper(s, s["quote"]) for s in specs]
            built = dph.make_curve(curve, helpers)
            expected = {"referenceDate": v.enc(built.referenceDate()), "maxDate": v.enc(built.maxDate()),
                        "dates": v.enc(list(built.dates())), "times": list(built.times())}
            expected.update(base)
            out.append({"name": f"curve {label} / {k}",
                        "inputs": {"settings": st, "helpers": specs, "curve": curve, "sample": v.enc(sample),
                                   "dropped": dropped},
                        "expected": expected})
    print(f"termstructures/credit/piecewisedefaultcurve: dropped {tally[0]} of {tally[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/piecewisedefaultcurve.hpp", make_cases)
