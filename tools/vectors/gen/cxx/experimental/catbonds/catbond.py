# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's floating cat bond and its Monte Carlo engine (ql/experimental/catbonds/catbond.hpp,
montecarlocatbondengine.hpp), which the wheel doesn't expose, each output computed by the C++ harness
(tools/cxx/src/experimental/catbonds/catbond.cpp): QuantLib's test's bond (four years semiannual from 30 November 2004
on the US government bond calendar, USD LIBOR 6M forecast flat at 2.5%, Actual/Actual (ISMA), one fixing day) with
spreads and redemptions varied, under digital and proportional notional risks, on event sets of no event, a certain
event, one event in ten years and a mid-life event, and on a beta risk of a thousand-year return period whose 10,000
paths hold one event or none, so that QuantLib's draws are nanoql's (at five hundred years a path parts), discounted
flat at 3% or 2.5% on QuantLib's test's 22 November 2004: the bond's cash flows' dates and amounts, and the engine's
NPV, settlement value, clean price and loss statistics. A simulation of no path, where QuantLib 1.44 reads NaN and
nanoql refuses with QuantLib's later fix (#2839; tests/NanoQL.Tests/Divergences/MonteCarloCatBondEngineTests.cs),
isn't asked for. Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/CatBonds/CatBondCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_creditdefaultswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "creditdefaultswap.py"))
cds = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cds)
dts = cds.dts


def d(day, month, year):
    return ql.Date(day, month, year).serialNumber()


US_GOV = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
ISMA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISMA)}
SCHEDULE = cds.schedule_spec(ql.Date(30, 11, 2004), ql.Date(30, 11, 2008), (6, int(ql.Months)), US_GOV,
                             ql.DateGeneration.Backward, ql.ModifiedFollowing, ql.ModifiedFollowing)
RISKS = [
    ("no events", ["EventSet", [], d(1, 1, 2000), d(31, 12, 2010)]),
    ("doom", ["EventSet", [[d(30, 11, 2004), 1000]], d(30, 11, 2004), d(30, 11, 2008)]),
    ("doom once in ten years", ["EventSet", [[d(30, 11, 2008), 1000]], d(30, 11, 2004), d(30, 11, 2044)]),
    ("a mid-life event of 800", ["EventSet", [[d(15, 6, 2010), 800]], d(30, 11, 2004), d(30, 11, 2044)]),
    ("beta, a thousand-year return period", ["BetaRisk", 5000.0, 1000.0, 500.0, 500.0]),
]
NOTIONALS = [("digital 100", ["Digital", 100.0]), ("proportional 500 to 1500", ["Proportional", 500.0, 1500.0])]
BONDS = [("plain", [], 100.0), ("spreads", [0.001, 0.0012, 0.0014, 0.0016], 100.0), ("redemption 101", [], 101.0)]


def flat(result):
    """The harness's object result, one output per key; a refused call's one output, "values"."""
    return dict(result) if isinstance(result, dict) and "throws" not in result else {"values": result}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for ev in (d(22, 11, 2004),):
            with H.case_settings(ev) as st:
                for risk_label, risk in RISKS:
                    for notional_label, notional in NOTIONALS:
                        for bond_label, spreads, redemption in BONDS:
                            for rate in (0.03, 0.025):
                                args = [1, 1000000.0, SCHEDULE, 0.025, ISMA, notional, 1, spreads, redemption,
                                        d(30, 11, 2004), risk, dts.discount_spec(ql.Date(ev), rate, dts.A360)]
                                out.append({"name": f"{ql.Date(ev).ISO()} {risk_label} {notional_label} {bond_label} "
                                                    f"discount {rate}",
                                            "inputs": {"settings": st, "args": args},
                                            "expected": flat(call("FloatingCatBond::values", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/catbonds/catbond.hpp", cases)
