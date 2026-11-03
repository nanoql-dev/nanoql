# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeMCLookbackEngine and MCLookbackEngine (ql/pricingengines/lookback/mclookbackengine.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the builder's checks for each of the
four lookback options, and the engine's prices on QuantLib's own Monte Carlo market.

For the fixed, floating, partial fixed and partial floating options: steps given, steps per year given, neither
("number of steps not given") and both ("number of steps overspecified"); samples then a tolerance ("number of samples
already set"), a tolerance then samples ("tolerance already set"); a tolerance alone; and a tolerance on
low-discrepancy paths ("chosen random generator policy does not allow an error estimate").

The prices: the fixed (struck at 90) and floating call and put, the extremum so far 100, on testMonteCarloLookback's
market (test-suite/lookbackoptions.cpp: spot 100, a 6% rate, no dividend yield and a 10% volatility on Act/360, a year
of 360 days), at 2,000 steps, 4,095 samples and seed 1, pseudo-random with antithetic variates and low-discrepancy
without: the NPV, and the error estimate under the pseudo-random traits. QuantLib's path pricers take the path's
extremum from its second node, never the spot or the running extremum, so nanoql's fold of the observed extremum moves
the price wherever the spot can be the extremum (tests/NanoQL.Tests/Divergences/McLookbackSeasonedTests.cs). At 2,000
steps the spot rarely stays the extremum: on these cases, with the override off, the fold moves a price by at most
3.7e-3 (3.4e-4 of it, the floating call), so the prices carry a relative tolerance of 1e-3 naming it; and an error
estimate, about 0.03 to 0.04, by at most 6.9e-6 (1.7e-4 of it), so the error estimates carry 3e-5, absolute below 1,
where the tolerance's size is its floor of 1 (docs/porting-playbook.md §7.3). The fixed put struck below the extremum so
far can't reach the fold and keeps the default tolerance. The partial options' prices are left out: their pricers snap
their window to a regular grid and drop λ, so nanoql's window on the grid and its λ change every price
(McLookbackWindowTests.cs, McLookbackLambdaTests.cs).

The builder's process is tools/cxx/src/pricingengines/lookback/mclookbackengine.cpp's, on the evaluation date 15 October
2026. nanoql's builder takes its seed at construction; the harness sets QuantLib's to
42. Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Lookback/MCLookbackEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1
FOLD = ("nanoql folds the spot and the observed extremum into each path's extremum, where "
        "QuantLib's pricers start from the path's second node; on these cases at 2,000 steps it moves ")
# A price's tolerance is relative; an error estimate's, under 1, is absolute: the tolerance's size is max(|a|, |b|, 1).
OVERRIDES = {"NPV": (1e-3, FOLD + "a price by at most 3.7e-3, 3.4e-4 of it, so 1e-3 relative"),
             "errorEstimate": (3e-5, FOLD + "an error estimate by at most 6.9e-6, 1.7e-4 of it, so 3e-5, absolute below 1")}
PRICED = [("fixed", "call", CALL), ("floating", "call", CALL), ("fixed", "put", PUT), ("floating", "put", PUT)]
TRAITS = [("pseudo-random, antithetic", "pseudorandom", True), ("low-discrepancy", "lowdiscrepancy", False)]


def folded(instrument, option_type, output, x):
    """The fold can't bind for the fixed put struck at 90, below the extremum so far: the rest take the output's
    override."""
    if instrument == "fixed" and option_type == PUT:
        return x
    tolerance, reason = OVERRIDES[output]
    return {"value": x, "tolerance": tolerance, "reason": reason}


INSTRUMENTS = ["fixed", "floating", "partial fixed", "partial floating"]
BUILDERS = [("steps", "pseudorandom", 10, None, 100, None, "samples"),
            ("steps per year", "pseudorandom", None, 12, 100, None, "samples"),
            ("no steps", "pseudorandom", None, None, 100, None, "samples"),
            ("steps and steps per year", "pseudorandom", 10, 12, 100, None, "samples"),
            ("samples, then a tolerance", "pseudorandom", 10, None, 100, 0.01, "samples"),
            ("a tolerance, then samples", "pseudorandom", 10, None, 100, 0.01, "tolerance"),
            ("a tolerance alone", "pseudorandom", 10, None, None, 0.01, "tolerance"),
            ("a tolerance on low-discrepancy paths", "lowdiscrepancy", 10, None, None, 0.01, "tolerance")]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for instrument in INSTRUMENTS:
            for name, traits, steps, per_year, samples, tolerance, order in BUILDERS:
                out.append({"name": f"builder, the {instrument} option, {name}",
                            "inputs": {"settings": st, "instrument": instrument, "traits": traits, "steps": steps,
                                       "stepsPerYear": per_year, "samples": samples, "tolerance": tolerance,
                                       "order": order},
                            "expected": {"built": v.attempt(lambda: H.call("MakeMCLookbackEngine", instrument, traits,
                                                                           steps, per_year, samples, tolerance,
                                                                           order))}})
        for instrument, tname, option_type in PRICED:
            for trname, traits, antithetic in TRAITS:
                priced = {"instrument": instrument, "optionType": option_type, "traits": traits, "steps": 2000,
                          "samples": 4095, "antithetic": antithetic, "seed": 1}
                r = H.call("MCLookbackEngine", *priced.values())
                out.append({"name": f"priced, the {instrument} {tname}, {trname}",
                            "inputs": {"settings": st, "priced": priced},
                            "expected": {k: folded(instrument, option_type, k, x) for k, x in r.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/lookback/mclookbackengine.hpp", cases)
