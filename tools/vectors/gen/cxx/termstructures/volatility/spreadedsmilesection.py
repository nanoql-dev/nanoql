# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SpreadedSmileSection (ql/termstructures/volatility/spreadedsmilesection.hpp, .cpp), which the wheel
builds only inside a SpreadedSwaptionVolatility, each output computed by the C++ harness (docs/porting-playbook.md
§9.4): 8 sections over a flat or a SABR section, by date with a reference date, by date floating (the underlying's
reference date the evaluation date, which nanoql's Create factories fix) and by time, both volatility types, shifts,
and spreads from -0.15 to 0.02, the most negative one turning the volatility negative.

- "section" cases, one per section: every member QuantLib forwards to the underlying section, its minStrike,
  maxStrike, atmLevel, exerciseDate, exerciseTime, dayCounter (its name, null for the empty one), referenceDate
  (refused for a section by time), volatilityType and shift; and at the underlying's strikes (those of
  tools/vectors/gen/termstructures/volatility/flatsmilesection.py and sabrsmilesection.py) the spreaded volatility,
  the variance, the call and put prices and digital prices with the case's discount and QuantLib's gap, the vega
  (refused for a normal section) and the density. The volatility converted to another type or shift isn't an
  output: QuantLib's SpreadedSmileSection default-constructs its base, so the conversion compares the type wanted
  with an uninitialized one, where nanoql compares the underlying's.

A refused value is {"throws": true}. Each case's input "section" is {"underlying": <the section's spec, as
tools/cxx/src/termstructures/volatility/smilesectionspec.hpp reads it>, "spread"}, the spread QuantLib's quote's
value; the case's evaluation date sets a floating section's reference date. A null underlying section, which
QuantLib dereferences at the first call, is never a case.
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SpreadedSmileSectionCxxVectorTests.cs reads the file back."""
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


fss = _load("wheel_flatsmilesection", "termstructures", "volatility", "flatsmilesection.py")
sss = _load("wheel_sabrsmilesection", "termstructures", "volatility", "sabrsmilesection.py")

A360, A365, ACT_ISDA = fss.A360, fss.A365, fss.ACT_ISDA
SL, N, CALL, PUT = fss.SL, fss.N, fss.CALL, fss.PUT
E1 = ql.Date(15, ql.January, 2025)
E3 = ql.Date(30, ql.October, 2026)
GAP, DENSITY_GAP = 1.0e-5, 1.0e-4

# (evaluation date, underlying spec, spread, discount)
SECTIONS = [
    (E1, fss.date_spec(ql.Date(15, ql.January, 2026), E1, 0.2, A365, 0.03), 0.01, 1.0),
    (E1, fss.time_spec(2.0, 0.25, A360, 0.02, SL, 0.01), -0.05, 0.97),
    (E3, fss.floating_spec(ql.Date(30, ql.April, 2028), 0.008, ACT_ISDA, 0.025, N), 0.001, 1.0),
    (E1, fss.time_spec(1.0, 0.1, A365, 0.03), -0.15, 1.0),
    (E1, sss.time_spec(1.0, 0.03, [0.03, 0.5, 0.4, 0.0]), 0.02, 1.0),
    (E1, sss.date_spec(ql.Date(15, ql.July, 2025), E1, 0.02, [0.009, 0.0, 0.3, 0.1], A360, N), -0.002, 0.99),
    (E1, sss.floating_spec(ql.Date(16, ql.January, 2028), 0.045, [0.06, 0.65, 0.35, 0.15], A360, SL, 0.005), 0.0,
     0.95),
    (E3, sss.date_spec(ql.Date(30, ql.October, 2031), E3, 0.05, [0.12, 0.8, 0.25, 0.5], A365, SL, 0.01), 0.015,
     1.0),
]


def strikes(underlying):
    return sss.strikes(underlying) if underlying["type"] == "SabrSmileSection" else fss.strikes(underlying)


def section_case(H, k, evaluation_date, underlying, spread, discount):
    with H.case_settings(v.enc(evaluation_date)) as st:
        spec = {"underlying": v.enc(underlying), "spread": spread}
        ks = strikes(underlying)

        def call(method, *args):
            return v.attempt(lambda: H.call("SpreadedSmileSection::" + method, spec, *args))

        def at_strikes(method, *args):
            return [v.attempt(lambda x=x: H.call("SpreadedSmileSection::" + method, spec, x, *args)) for x in ks]

        expected = {m: call(m) for m in ("minStrike", "maxStrike", "atmLevel", "exerciseDate", "exerciseTime",
                                         "dayCounter", "referenceDate", "volatilityType", "shift")}
        expected.update({
            "volatility": at_strikes("volatility"),
            "variance": at_strikes("variance"),
            "callPrice": at_strikes("optionPrice", CALL, discount),
            "putPrice": at_strikes("optionPrice", PUT, discount),
            "digitalCallPrice": at_strikes("digitalOptionPrice", CALL, discount, GAP),
            "digitalPutPrice": at_strikes("digitalOptionPrice", PUT, discount, GAP),
            "vega": at_strikes("vega", discount),
            "density": at_strikes("density", discount, DENSITY_GAP)})
        kind = "Normal" if underlying["volatilityType"] == N else "ShiftedLognormal"
        return {"name": f"{underlying['type']} {underlying['form']} {kind} spread {spread!r} / {k}",
                "inputs": {"settings": st, "kind": "section", "section": spec, "strikes": ks, "discount": discount,
                           "gap": GAP, "densityGap": DENSITY_GAP},
                "expected": expected}


def cases():
    with cxx.connect() as H:
        return [section_case(H, k, *row) for k, row in enumerate(SECTIONS)]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/spreadedsmilesection.hpp", cases)
