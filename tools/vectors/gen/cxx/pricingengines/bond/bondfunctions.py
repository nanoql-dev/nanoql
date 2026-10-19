# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's BondFunctions (ql/pricingengines/bond/bondfunctions.hpp) that the wheel
doesn't bind, each output computed by the C++ harness: the reference period's start and end, the positions of the
previous and next cash flows, and the dirty price for a yield through the InterestRate and the rate forms.

The bonds are tools/vectors/gen/pricingengines/bond/bondfunctions.py's 11, imported (its floating-rate bond with the
fixings it stores), and 2 on QuantLib's empty calendar, which the wheel can't make (cxx/instruments/bond.py's
specs: Bonds from coupons and from a face amount), whose settlement date QuantLib can't
compute ("no calendar implementation provided"), so that only the members given a date work. A case is one bond under
one evaluation context drawn with the seed as the wheel generator draws it, with its rate, at 8 settlement dates (a
null one, which means the bond's settlement date, and 7 drawn as the wheel generator draws them, or as
cxx/instruments/bond.py does for the bonds the wheel can't build): at each, the reference period start and end of
the coupon the accrual functions read (null where there's none), previousCashFlow's position (its rend() as -1) and
nextCashFlow's (its end() as the number of cash flows), and the dirty price for the rate's yield in both forms (a
refused value at a date the bond isn't tradable).

A refused value is {"throws": true}. Each case's inputs hold its settings, "bond", "rate" and "dates". Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/BondFunctionsCxxVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bd = _load("wheel_bond", "instruments/bond.py")
frb = _load("wheel_floatingratebond", "instruments/bonds/floatingratebond.py")
wf = _load("wheel_bondfunctions", "pricingengines/bond/bondfunctions.py")
cb = _load("cxx_bond", "cxx/instruments/bond.py")

NAMES = ["referencePeriodStart", "referencePeriodEnd", "previousCashFlow", "nextCashFlow", "dirtyPriceRate",
         "dirtyPriceRateForm"]


def bonds():
    """(name, bond spec, whether the wheel builds it, its evaluation date or None for a drawn one) for the 13
    bonds."""
    out = [(name, spec, True, ev) for name, spec, _, ev in wf.bonds()]
    harness = {name: spec for name, spec, _, _ in cb.harness_bonds()}
    out += [(name, harness[name], False, None)
            for name in ("coupons on the empty calendar", "face amount on the empty calendar")]
    return out


def at(H, spec, d, r):
    rate_form = "BondFunctions::dirtyPrice(Bond,Rate,DayCounter,Compounding,Frequency,Date)"
    return {"referencePeriodStart": cb.call(H, "BondFunctions::referencePeriodStart", spec, d),
            "referencePeriodEnd": cb.call(H, "BondFunctions::referencePeriodEnd", spec, d),
            "previousCashFlow": cb.call(H, "BondFunctions::previousCashFlow", spec, d),
            "nextCashFlow": cb.call(H, "BondFunctions::nextCashFlow", spec, d),
            "dirtyPriceRate": cb.call(H, "BondFunctions::dirtyPrice(Bond,InterestRate,Date)", spec, r, d),
            "dirtyPriceRateForm": cb.call(H, rate_form, spec, r["rate"], r["dayCounter"], r["compounding"],
                                          r["frequency"], d)}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        for k, (name, spec, in_wheel, fixed_ev) in enumerate(bonds()):
            ev = fixed_ev if fixed_ev is not None else rnd.choice(wf.EVALUATION_DATES)
            ref_events, todays = rnd.choice([False, True]), rnd.choice([None, False, True])
            fixings, stored = None, None
            if spec["type"] == "FloatingRateBond":
                with v.case_settings(ev):
                    stored = frb.past_fixings(spec, ev)(bd.make_bond(spec))
                with v.case_settings(ev, fixings=stored) as wheel_settings:
                    fixings = wheel_settings.get("fixings")
            with H.case_settings(v.enc(ev), include_reference_date_events=ref_events,
                                 include_todays_cash_flows=todays, fixings=fixings) as st:
                rate = wf.RATES[k % len(wf.RATES)]
                if in_wheel:
                    with v.case_settings(ev, include_reference_date_events=ref_events,
                                         include_todays_cash_flows=todays, fixings=stored if fixings else None):
                        dates = bd.bond_dates(bd.make_bond(spec), ev, rnd, 8)
                else:
                    dates = cb.bond_dates(H, spec, ev, rnd, 8)
                expected = {m: [] for m in NAMES}
                for d in dates:
                    for m, value in at(H, spec, d, rate).items():
                        expected[m].append(value)
                out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                            "inputs": {"settings": st, "bond": spec, "rate": rate, "dates": dates},
                            "expected": expected})
    assert any(v.THROWS in c["expected"]["dirtyPriceRate"] for c in out)
    assert any(-1 in c["expected"]["previousCashFlow"] for c in out)
    assert any(None in c["expected"]["referencePeriodStart"] for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/bond/bondfunctions.hpp", cases)
