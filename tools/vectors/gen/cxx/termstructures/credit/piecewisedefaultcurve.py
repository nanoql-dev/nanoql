# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's piecewise default curve (ql/termstructures/credit/piecewisedefaultcurve.hpp) on the
default-density and survival-probability traits and the interpolators the wheel doesn't bind, each output computed
by the C++ harness.

- "curve" cases: tools/vectors/gen/termstructures/credit/defaultprobabilityhelpers.py's 2015-rule spread helpers and
  upfront helpers (mid-point model) on default densities (backward-flat, linear), survival probabilities (linear,
  log-linear) and hazard rates (linear), and the ISDA-model spread helpers on log-linear survival probabilities, which
  the ISDA engine accepts as it accepts QuantLib's own cast: the curve's dates, times and data, and its survival
  probability and hazard rate at 20 dates; the ISDA-model helpers on linear densities, refused by the ISDA engine.

The data and the values are solved, so the stability filter runs on them, as in the wheel's piecewisedefaultcurve
file; a dropped output is written null and its index listed in the case's inputs "dropped". A case's inputs hold its
settings, the traits, the interpolator, the helper specs, the curve's reference date and day counter and the sample
dates, as tools/cxx/src/termstructures/credit/piecewisedefaultcurve.cpp reads them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/PiecewiseDefaultCurveCxxVectorTests.cs."""
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
    "wheel_defaultprobabilityhelpers",
    os.path.join(v.GEN_ROOT, "termstructures", "credit", "defaultprobabilityhelpers.py"))
dph = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dph)
dts = dph.dts

EV = dph.MONDAY
SPREAD = dph.spread_set(EV, ql.DateGeneration.CDS2015)
UPFRONT = dph.upfront_set(EV)
ISDA = dph.spread_set(EV, ql.DateGeneration.CDS2015, dph.ISDA)
CURVES = [
    ("spread", "DefaultDensity", "BackwardFlat", SPREAD),
    ("spread", "DefaultDensity", "Linear", SPREAD),
    ("spread", "SurvivalProbability", "Linear", SPREAD),
    ("spread", "SurvivalProbability", "LogLinear", SPREAD),
    ("spread", "HazardRate", "Linear", SPREAD),
    ("upfront", "DefaultDensity", "Linear", UPFRONT),
    ("upfront", "SurvivalProbability", "LogLinear", UPFRONT),
    ("ISDA spread", "SurvivalProbability", "LogLinear", ISDA),
]
REFUSED = [("ISDA spread", "DefaultDensity", "Linear", ISDA)]
SAMPLE = [EV + 30 + 220 * n for n in range(20)]


def cases():
    out = []
    tally = [0, 0]
    k = 0
    with cxx.connect() as H:
        def build(traits, interp, specs, quotes):
            with H.case_settings(v.enc(EV)):
                return H.call("PiecewiseDefaultCurve::build", traits, interp, specs, quotes, v.enc(EV), dts.A365,
                              v.enc(SAMPLE))

        for label, traits, interp, specs in CURVES:
            quotes = [s["quote"] for s in specs]
            base = build(traits, interp, specs, quotes)
            moved = [build(traits, interp, specs, m) for m in dph.moves(quotes)]
            dropped = {}
            for name in ("data", "survivalProbability", "hazardRate"):
                values = base[name]
                keep = [all(dph.stable(values[i], m[name][i]) for m in moved) for i in range(len(values))]
                dropped[name] = [i for i, kept in enumerate(keep) if not kept]
                base[name] = [x if kept else None for x, kept in zip(values, keep)]
                tally[0] += len(dropped[name])
                tally[1] += len(values)
            print(f"curve {label} {traits} {interp}: dropped {sum(len(d) for d in dropped.values())} outputs")
            with H.case_settings(v.enc(EV)) as st:
                out.append({"name": f"curve {label} on {traits} {interp} / {k}",
                            "inputs": {"settings": st, "traits": traits, "interpolator": interp, "helpers": specs,
                                       "referenceDate": v.enc(EV), "dayCounter": dts.A365, "sample": v.enc(SAMPLE),
                                       "dropped": dropped},
                            "expected": base})
            k += 1
        for label, traits, interp, specs in REFUSED:
            quotes = [s["quote"] for s in specs]
            with H.case_settings(v.enc(EV)) as st:
                out.append({"name": f"refused {label} on {traits} {interp} / {k}",
                            "inputs": {"settings": st, "traits": traits, "interpolator": interp, "helpers": specs,
                                       "referenceDate": v.enc(EV), "dayCounter": dts.A365, "sample": v.enc(SAMPLE),
                                       "dropped": {}},
                            "expected": {"build": v.attempt(lambda: build(traits, interp, specs, quotes))}})
            k += 1
    print(f"cxx/termstructures/credit/piecewisedefaultcurve: dropped {tally[0]} of {tally[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/piecewisedefaultcurve.hpp", cases)
