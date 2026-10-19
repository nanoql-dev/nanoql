# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AmortizingFixedRateBond and its free functions sinkingSchedule and sinkingNotionals
(ql/instruments/bonds/amortizingfixedratebond.hpp).

The bonds are 10, each under one evaluation context drawn with the seed from tools/vectors/gen/instruments/bond.py's
24: French amortizations from sinkingSchedule and sinkingNotionals (annual, semiannual, quarterly and monthly, at
rates of 0% (linear), 3% and 6%), notionals given (fewer than the coupons, the last repeating, and more, which the
leg ignores), redemptions of one value, of two (whose first QuantLib never reads, every step taking the second) and
of three, a payment lag, an ex-coupon period, an issue date, and a schedule built from dates, with no tenor, which the
bond refuses ("full interface (tenor) not available") before anything else. A case's outputs are bond.py's: the
inspectors with no date (an amortizing bond has several redemptions, so its single redemption is refused), the
inspectors and the prices and yield at 14 dates (a null one, the settlement date and 12 drawn), and the discounting
engine's results with the members that read them; with the bond's frequency and its day counter's name. A refused
construction is the case's one output, "construct".

Then one case per bond length (1, 2, 3 and 5 years and 18 months), for each of the frequencies Annual, Semiannual,
EveryFourthMonth, Quarterly, Bimonthly and Monthly: sinkingNotionals' notionals for an initial notional of 100 at the
rates 0% and 5%, or its refusal where the length isn't a whole number of the frequency's periods ("Bond frequency is
incompatible with the maturity tenor"), and sinkingSchedule's dates from 15 January 2025 on TARGET. Notionals are
compared at 100. The lengths and frequencies QuantLib reads undefined memory for (a length that isn't positive, Once and
NoFrequency) are Divergences/SinkingNotionalsTests.cs' facts. Read back by
tests/NanoQL.Tests/Vectors/Instruments/AmortizingFixedRateBondVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_bond", os.path.join(v.GEN_ROOT, "instruments", "bond.py"))
bd = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bd)

D = ql.Date
TARGET, NULL_CALENDAR, US = bd.TARGET, bd.NULL_CALENDAR, bd.US_GOVERNMENT
A360, A365, BB, ISMA = bd.A360, bd.A365, bd.THIRTY360_BB, bd.ACT_ISMA
START = D(15, 1, 2025)


def sinking(length, freq, rate, cal=TARGET, start=START):
    """A French amortization's schedule spec and notionals, from the wheel's sinkingSchedule and sinkingNotionals."""
    sch = ql.sinkingSchedule(start, length, freq, bd.calendar(cal))
    dates = list(sch.dates())
    spec = bd.schedule_spec(start, start + length, (ql.Period(freq).length(), ql.Period(freq).units()), cal,
                            ql.Unadjusted, ql.DateGeneration.Backward, False)
    assert list(bd.make_schedule(spec).dates()) == dates
    return spec, list(ql.sinkingNotionals(length, freq, rate, 100.0))


def bonds():
    """(name, bond spec, face amount) for the 10 bonds."""
    a5, n_a5 = sinking(ql.Period(5, ql.Years), ql.Annual, 0.05)
    s3, n_s3 = sinking(ql.Period(3, ql.Years), ql.Semiannual, 0.06, NULL_CALENDAR)
    q2, n_q2 = sinking(ql.Period(2, ql.Years), ql.Quarterly, 0.0)
    m1, n_m1 = sinking(ql.Period(1, ql.Years), ql.Monthly, 0.03)
    s6m = bd.schedule_spec(D(19, 5, 2024), D(19, 5, 2029), (6, ql.Months), US, ql.Unadjusted)
    dates = bd.dates_schedule_spec([D(15, 1, 2025), D(15, 7, 2025), D(15, 1, 2026)])
    return [
        ("annual 5% French", bd.amortizing_bond_spec(0, n_a5, a5, [0.05], ISMA), 100.0),
        ("semiannual 6% French", bd.amortizing_bond_spec(2, n_s3, s3, [0.06], BB, ql.Following, D(15, 1, 2025)),
         100.0),
        ("quarterly 0% linear", bd.amortizing_bond_spec(1, n_q2, q2, [0.0001], A360), 100.0),
        ("monthly 3% French, payment lag", bd.amortizing_bond_spec(0, n_m1, m1, [0.03], A365, ql.Following, None,
                                                                   None, [100.0], 2), 100.0),
        ("notionals given, fewer than the coupons", bd.amortizing_bond_spec(3, [100.0, 75.0, 50.0], s6m, [0.042],
                                                                            A360, ql.ModifiedFollowing), 100.0),
        ("notionals given, more than the coupons", bd.amortizing_bond_spec(
            3, [1e6, 9e5, 8e5, 7e5, 6e5, 5e5, 4e5, 3e5, 2e5, 1e5, 5e4, 1e4], s6m, [0.03, 0.035], A365), 1e6),
        ("two redemptions",
         bd.amortizing_bond_spec(2, n_a5, a5, [0.05], ISMA, ql.Following, None, None, [101.0, 102.0]),
         100.0),
        ("three redemptions", bd.amortizing_bond_spec(2, n_a5, a5, [0.05], ISMA, ql.Following, None, None,
                                                      [101.0, 102.0, 103.0]), 100.0),
        ("ex-coupon", bd.amortizing_bond_spec(2, n_s3, s3, [0.06], BB, ql.Following, None,
                                              bd.ex_coupon_spec(10, ql.Days, TARGET, ql.Preceding)), 100.0),
        ("schedule of dates", bd.amortizing_bond_spec(2, [100.0, 50.0], dates, [0.04], A365), 100.0),
    ]


def extra(bond):
    return {"frequency": int(bond.frequency()), "dayCounter": bond.dayCounter().name()}


LENGTHS = [ql.Period(n, ql.Years) for n in (1, 2, 3, 5)] + [ql.Period(18, ql.Months)]
FREQUENCIES = [ql.Annual, ql.Semiannual, ql.EveryFourthMonth, ql.Quarterly, ql.Bimonthly, ql.Monthly]
SINKING_RATES = [0.0, 0.05]


def sinking_cases():
    out = []
    st = v.settings(START)
    for length in LENGTHS:
        notionals, schedules = [], []
        for freq in FREQUENCIES:
            notionals.append([[v.amount(x, 100.0) for x in ns] if isinstance(ns, list) else ns
                              for ns in (v.attempt(lambda: list(ql.sinkingNotionals(length, freq, r, 100.0)))
                                         for r in SINKING_RATES)])
            schedules.append(v.attempt(lambda: [d.serialNumber() for d in ql.sinkingSchedule(
                START, length, freq, ql.TARGET()).dates()]))
        out.append({"name": f"sinking {length}",
                    "inputs": {"settings": st, "bondLength": [length.length(), int(length.units())],
                               "frequencies": [int(f) for f in FREQUENCIES], "rates": SINKING_RATES,
                               "startDate": v.enc(START), "calendar": TARGET},
                    "expected": {"sinkingNotionals": notionals, "sinkingSchedule": schedules}})
    return out


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale) in enumerate(bonds()):
        context = rnd.choice(bd.contexts())
        rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
        out.append(bd.run_case(name, spec, scale, context, rate, price, bd.CURVE_RATE, rnd, 14, extra))
    out += sinking_cases()
    assert sum("construct" in c["expected"] for c in out) == 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/amortizingfixedratebond.hpp", cases)
