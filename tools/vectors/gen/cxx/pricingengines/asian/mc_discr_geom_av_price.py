# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GeometricAPOPathPricer and MakeMCDiscreteGeometricAPEngine (ql/pricingengines/asian/mc_discr_geom_av_price.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the geometric average-price path pricer on given
paths and the builder's checks.

The pricer: calls and puts at 90 and 105, discounted at 0.95, unseasoned, with two past fixings of product 9,975 and with a running product of 1e300, flushed into the average as QuantLib flushes it, and a negative strike (refused); on paths from 0 (the start a fixing), from a quarter (the grid adding 0, a node
that isn't one) and of one fixing. The builder: samples, then a tolerance ("number of samples already set"), a
tolerance, then samples ("tolerance already set"), a tolerance alone, neither, and a tolerance on low-discrepancy
paths ("chosen random generator policy does not allow an error estimate").

The prices of the Monte Carlo engine are the wheel's (tools/vectors/gen/pricingengines/asian/mc_discr_geom_av_price.py).
The builder's process is tools/cxx/src/pricingengines/asian/mc_discr_geom_av_price.cpp's, on the evaluation date 15
October 2026. nanoql's builder takes its seed at construction; the harness sets QuantLib's to 42. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Asian/MCDiscreteGeometricAPEngineCxxVectorTests.cs."""
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
BUILDERS = [("samples", "pseudorandom", 100, None, "samples"),
            ("samples, then a tolerance", "pseudorandom", 100, 0.01, "samples"),
            ("a tolerance, then samples", "pseudorandom", 100, 0.01, "tolerance"),
            ("a tolerance alone", "pseudorandom", None, 0.01, "tolerance"),
            ("neither", "pseudorandom", None, None, "samples"),
            ("a tolerance on low-discrepancy paths", "lowdiscrepancy", None, 0.01, "tolerance"),
            ("samples on low-discrepancy paths", "lowdiscrepancy", 100, None, "samples")]
# paths: their times, the first 0 or not (the grid then starts at 0, a node that isn't a fixing), and a value per node
PATHS = [("from 0", [0.0, 0.25, 0.5, 0.75, 1.0], [100.0, 104.0, 97.0, 110.0, 101.0]),
         ("from a quarter", [0.25, 0.5, 0.75, 1.0], [100.0, 104.0, 97.0, 110.0, 101.0]),
         ("one fixing", [1.0], [100.0, 92.0])]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for pname, times, values in PATHS:
            variance = [0.04] * len(values)
            for running, past in [(1.0, 0), (9975.0, 2), (1e+300, 2)]:
                for tname, t in (("call", CALL), ("put", PUT)):
                    for k in (90.0, 105.0):
                        inputs = {"settings": st, "optionType": t, "strike": k, "discount": 0.95, "runningAccumulator": running,
                                  "pastFixings": past, "times": times, "values": values}
                        out.append({"name": f"pricer, a {tname}{'' if k is None else f' at {k:g}'}, {pname}, running {running:g}, {past} past",
                                    "inputs": inputs,
                                    "expected": {"value": v.attempt(lambda: H.call("GeometricAPOPathPricer", *[t, k, 0.95, running, past, times, values]))}})
        times, values = PATHS[0][1], PATHS[0][2]
        variance = [0.04] * len(values)
        out.append({"name": "pricer, a negative strike", "inputs": {"settings": st, "optionType": CALL, "strike": -1.0, "discount": 0.95, "runningAccumulator": 1.0, "pastFixings": 0, "times": times, "values": values}, "expected": {"value": v.attempt(lambda: H.call("GeometricAPOPathPricer", CALL, -1.0, 0.95, 1.0, 0, times, values))}})
        for name, traits, samples, tolerance, order in BUILDERS:
            out.append({"name": f"builder, {name}",
                        "inputs": {"settings": st, "traits": traits, "samples": samples, "tolerance": tolerance,
                                   "order": order},
                        "expected": {"built": v.attempt(lambda: H.call("MakeMCDiscreteGeometricAPEngine", traits, samples, tolerance, order))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/mc_discr_geom_av_price.hpp", cases)
