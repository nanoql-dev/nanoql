# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year inflation cap and floor engines
(ql/pricingengines/inflation/inflationcapfloorengines.hpp): the Black, unit-displaced Black and Bachelier engines
through the wheel's YoYInflationBlackCapFloorEngine, YoYInflationUnitDisplacedBlackCapFloorEngine and
YoYInflationBachelierCapFloorEngine.

- "engine" cases: a 5-year cap and floor (tools/vectors/gen/instruments/inflationcapfloor.py's instruments) at five
  strikes, deep in and out of the money, under three volatilities per engine, on surfaces with and without settlement
  days and an interpolated index's base date: NPV and optionletPrices. Every coupon fixes after the volatility's base
  date, observed flat with no fixing days on a regular schedule, where QuantLib's engines' forward is the coupon's own
  fixing (tests/NanoQL.Tests/Divergences/YoYCapFloorEngineFixingTests.cs). The engines' other additional results are
  the harness's (tools/vectors/gen/cxx/pricingengines/inflation/inflationcapfloorengines.py).

A case's inputs are those of tools/vectors/gen/instruments/inflationcapfloor.py. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/InflationCapFloorEnginesVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_inflationcapfloor",
                                               os.path.join(v.GEN_ROOT, "instruments", "inflationcapfloor.py"))
icap = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(icap)
vol = icap.vol
cf = icap.cf

UK = cf.UK_SETTLEMENT
M = int(ql.Months)
STRIKES = [-0.01, 0.0, 0.025, 0.03, 0.06]
VOLATILITIES = {"Black": [0.05, 0.15, 0.4], "UnitDisplacedBlack": [0.002, 0.01, 0.03],
                "Bachelier": [0.002, 0.008, 0.02]}
SURFACES = [(0, ql.ModifiedFollowing, cf.A365, False), (2, ql.Following, cf.THIRTY360_BB, True)]


def engine_cases():
    out = []
    k = 0
    for kind, vols in VOLATILITIES.items():
        for days, convention, dc, interpolated in SURFACES:
            for sigma in vols:
                vol_spec = vol.surface_spec(sigma, days, UK, convention, dc, [2, M], ql.Monthly, interpolated)
                for t in (icap.CAP, icap.FLOOR):
                    for strike in STRIKES:
                        if kind == "Black" and strike <= 0.0:
                            continue
                        spec = icap.instrument_spec(t, icap.leg(5), caps=[strike] if t == icap.CAP else (),
                                                    floors=[strike] if t == icap.FLOOR else ())
                        case = icap.priced(spec, icap.engine_spec(kind, vol_spec),
                                           f"engine {kind} {sigma} {'cap' if t == icap.CAP else 'floor'} {strike}, "
                                           f"{days} days / {k}")
                        del case["expected"]["isExpired"]
                        out.append(case)
                        k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/inflation/inflationcapfloorengines.hpp", engine_cases)
