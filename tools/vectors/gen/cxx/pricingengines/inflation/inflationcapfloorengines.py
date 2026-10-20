# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year cap and floor engines' additional results
(ql/pricingengines/inflation/inflationcapfloorengines.hpp), each output computed by the C++ harness: the wheel binds
no additional result.

- "engine" cases: caps, floors and a collar (tools/vectors/gen/instruments/inflationcapfloor.py's instruments) under
  each engine, on surfaces with and without settlement days: NPV and the additional results optionletsPrice,
  optionletsAtmForward and optionletsStdDev, the last refused for a collar ("not provided"). Every coupon fixes after
  the volatility's base date, observed flat with no fixing days on a regular schedule, where QuantLib's engines'
  forward is the coupon's own fixing (tests/NanoQL.Tests/Divergences/YoYCapFloorEngineFixingTests.cs).

The case's settings store UK RPI's fixings by name (tools/vectors/gen/cxx/cashflows/inflationcoupon.py's). A case's
inputs are those of tools/vectors/gen/instruments/inflationcapfloor.py, its fixings in its settings. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/InflationCapFloorEnginesCxxVectorTests.cs."""
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


def _load(name, *path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ic = _load("cxx_inflationcoupon", "cxx", "cashflows", "inflationcoupon.py")
icap = _load("wheel_inflationcapfloor", "instruments", "inflationcapfloor.py")
vol = icap.vol
cf = icap.cf

TODAY = ic.TODAY
UK = ic.UK
M = ic.M
SURFACES = {
    "Black": [vol.surface_spec(0.12, 0, UK, ql.ModifiedFollowing, cf.A365, [2, M], ql.Monthly, False),
              vol.surface_spec(0.2, 2, UK, ql.Following, cf.THIRTY360_BB, [2, M], ql.Monthly, True)],
    "UnitDisplacedBlack": [vol.surface_spec(0.01, 0, UK, ql.ModifiedFollowing, cf.A365, [2, M], ql.Monthly, False)],
    "Bachelier": [vol.surface_spec(0.008, 0, UK, ql.ModifiedFollowing, cf.A365, [2, M], ql.Monthly, False),
                  vol.surface_spec(0.015, 2, UK, ql.Following, cf.THIRTY360_BB, [2, M], ql.Monthly, True)],
}
INSTRUMENTS = [
    ("cap, 5 years", icap.instrument_spec(icap.CAP, icap.leg(5), caps=[0.03])),
    ("floor, varying strikes", icap.instrument_spec(icap.FLOOR, icap.leg(3), floors=[0.025, 0.03, 0.035])),
    ("collar, 4 years", icap.instrument_spec(icap.COLLAR, icap.leg(4), caps=[0.033], floors=[0.027])),
]
TAGS = ["NPV", "optionletsPrice", "optionletsAtmForward", "optionletsStdDev"]


def cases():
    out = []
    k = 0
    with cxx.connect() as H:
        for kind, surfaces in SURFACES.items():
            for surface in surfaces:
                engine = icap.engine_spec(kind, surface)
                for label, spec in INSTRUMENTS:
                    with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
                        expected = {tag: v.result(lambda tag=tag: H.call("YoYInflationCapFloorEngine::calculate",
                                                                         icap.INDEX, spec, engine, ic.NOMINAL,
                                                                         tag))
                                    for tag in TAGS}
                        out.append({"name": f"engine {kind} {surface['volatility']}, {label} / {k}",
                                    "inputs": {"settings": st, "kind": "engine", "index": icap.INDEX, "fixings": [],
                                               "nominalCurve": ic.NOMINAL, "instrument": spec,
                                               "engine": engine},
                                    "expected": expected})
                    k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/inflation/inflationcapfloorengines.hpp", cases)
