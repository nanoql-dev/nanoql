# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeCreditDefaultSwap (ql/instruments/makecds.hpp), through the wheel's
MakeCreditDefaultSwap: its tenor and term-date constructors and the nine setters the wheel binds besides the pricing
engine (upfrontRate, side, nominal, couponTenor, dayCounter, lastPeriodDayCounter, dateGenerationRule,
cashSettlementDays, tradeDate).

- "build" cases: on a Monday, a Saturday, a roll date and the last roll of a year, the builder from a tenor (0 months
  to 10 years) or a term date, alone and with each setter: the swap's inspectors, as
  tools/vectors/gen/instruments/creditdefaultswap.py writes them; refused where the 2015 rule gives a zero tenor no
  maturity, which the schedule refuses.

The schedule constructor and the setters the wheel lacks are in the harness's cxx/instruments/makecds. A case's inputs
hold its settings and the builder {"tenor" or "termDate", "runningSpread", "setters": [[name, value], ...]}, a day
counter as tools/vectors/gen/termstructures/defaulttermstructure.py's, a period as [length, unit]. Read back by
tests/NanoQL.Tests/Vectors/Instruments/MakeCreditDefaultSwapVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_creditdefaultswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "creditdefaultswap.py"))
cds_gen = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cds_gen)
dts = cds_gen.dts
M, Y = cds_gen.M, cds_gen.Y

EVALUATIONS = [ql.Date(15, ql.January, 2024), ql.Date(16, ql.March, 2024), ql.Date(20, ql.March, 2024),
               ql.Date(20, ql.December, 2024)]
BUILDERS = [
    ("5Y", {"tenor": [5, Y]}, []),
    ("1Y", {"tenor": [1, Y]}, []),
    ("0M", {"tenor": [0, M]}, []),
    ("0M, 2015 rule", {"tenor": [0, M]}, [["dateGenerationRule", int(ql.DateGeneration.CDS2015)]]),
    ("6M, 2009 rule", {"tenor": [6, M]}, [["dateGenerationRule", int(ql.DateGeneration.CDS)]]),
    ("3Y, old rule", {"tenor": [3, Y]}, [["dateGenerationRule", int(ql.DateGeneration.OldCDS)]]),
    ("10Y, forward rule", {"tenor": [10, Y]}, [["dateGenerationRule", int(ql.DateGeneration.Forward)]]),
    ("term date", {"termDate": v.enc(ql.Date(20, ql.June, 2029))}, []),
    ("5Y, upfront and seller", {"tenor": [5, Y]}, [["upfrontRate", 0.025], ["side", cds_gen.SELLER]]),
    ("5Y, nominal and semiannual", {"tenor": [5, Y]}, [["nominal", 1e7], ["couponTenor", [6, M]]]),
    ("5Y, day counters", {"tenor": [5, Y]}, [["dayCounter", dts.A365], ["lastPeriodDayCounter", dts.A360]]),
    ("5Y, cash settlement days", {"tenor": [5, Y]}, [["cashSettlementDays", 1]]),
    ("5Y, trade date", {"tenor": [5, Y]}, [["tradeDate", v.enc(ql.Date(10, ql.January, 2024))]]),
]


def apply(builder, setters):
    """Each setter on the builder itself: a setter returns a proxy that doesn't own the builder, so the caller keeps
    the builder alive and calls makeCDS on it."""
    for name, value in setters:
        if name == "couponTenor":
            value = ql.Period(value[0], value[1])
        elif name in ("dayCounter", "lastPeriodDayCounter"):
            value = dts.day_counter(value)
        elif name == "tradeDate":
            value = ql.Date(value)
        getattr(builder, "with" + name[0].upper() + name[1:])(value)


def make_cases():
    import QuantLib.QuantLib as qlm
    out = []
    k = 0
    for ev in EVALUATIONS:
        for label, start, setters in BUILDERS:
            with v.case_settings(ev) as st:
                first = ql.Period(start["tenor"][0], start["tenor"][1]) if "tenor" in start else ql.Date(start["termDate"])
                inputs = {"settings": st, "builder": dict(start, runningSpread=0.01, setters=setters)}
                try:
                    builder = qlm._MakeCreditDefaultSwap(first, 0.01)
                    apply(builder, setters)
                    cds = builder.makeCDS()
                    expected = cds_gen.inspectors(cds)
                except RuntimeError:
                    expected = {"construction": dict(v.THROWS)}
                out.append({"name": f"{label} / {ev.ISO()} / {k}", "inputs": inputs, "expected": expected})
            k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makecds.hpp", make_cases)
