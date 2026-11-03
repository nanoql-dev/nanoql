# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OISRateHelper (ql/termstructures/yield/oisratehelper.hpp), through the BootstrapHelper
interface it inherits: 60 helpers, each built on the three evaluation dates of termstructures/yield/ratehelpers.py
(Monday 15 January 2024; Thursday 29 February 2024, the end of a month; Saturday 30 March 2024, in TARGET's Easter
holidays), 180 cases; then 13 curves bootstrapped on them, 13 cases.

- 54 helpers from a tenor, the base 1 year on ESTR, 2 settlement days, an annual swap paid with no lag on Following:
  tenors from 1 week to 30 years; settlement days 0, 1 and 3; payment lags, conventions, frequencies (Once among them)
  and a payment calendar; forward starts, a negative one among them; a spread; the three pillar choices, a custom pillar
  too late and none at all refused; simple averaging; end of month either way; a fixed-leg frequency, calendar and day
  counter; an overnight calendar; lookback days with and without the observation shift, lockout days and both;
  telescopic value dates, alone, with simple averaging, with lookback days and no shift (refused, as QuantLib refuses
  telescopic dates with a lookback) and with a lockout and the shift; the averaging pricer with a convexity adjustment
  and the compounding pricer; the forward and zero date-generation rules; the schedules' conventions; a discounting
  curve; SOFR on the US calendars, with Good Friday a SOFR holiday but not a Federal Reserve one; SONIA; and settlement
  days of int's maximum, which QuantLib reads as none, left out: nanoql refuses them.
- 6 helpers between dates (built with forDates): a year, a short swap, a seasoned one, semiannual with a payment lag,
  a custom pillar, and reversed dates, which QuantLib refuses.
- 12 curves, one per case: 4 helper sets (a 1-day deposit and OIS helpers from 1 week to 30 years on ESTR, compounded,
  on a log-linear discount curve; the same averaged, on a backward-flat forward curve; with 2 lookback days, the
  observation shift and telescopic value dates, on a backward-flat forward curve, which on 30 March 2024 reads ESTR's
  fixings of 27 and 28 March, stored for it; and on SOFR with a payment lag of 2, pillars at maturity and the US
  calendars, on a linear zero curve) x the 3 evaluation dates, each curve's reference date the evaluation date; and
  the first set's curve built 15 TARGET days after the first evaluation date, on its helpers built on that date,
  which re-dates them ("kind": "moved", with "helpersEvaluationDate"; no quote errors, as for
  termstructures/yield/piecewiseyieldcurve.py's).

A helper's outputs are whether it builds, QuantLib's refusals recorded as its throw, its five dates, and its swap's
start and maturity dates and the payment dates of its fixed and overnight legs; a curve's are those of
termstructures/yield/piecewiseyieldcurve.py's cases ("kind": "curves"). Helpers with telescopic value dates take
QuantLib's values by the telescopic rule (termstructures/yield/ratehelpers.py's value_spec). Each case's inputs hold its
settings and its "helper" (a spec of termstructures/yield/ratehelpers.py), or its "kind" and the curve case's inputs.
Read back by tests/NanoQL.Tests/Vectors/TermStructures/Yield/OISRateHelperVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)
rh = pw.rh
fvf, irx = rh.fvf, rh.irx

D = rh.D
TARGET, US_SOFR, UK_EXCHANGE, A360, A365 = rh.TARGET, rh.US_SOFR, rh.UK_EXCHANGE, rh.A360, rh.A365
ESTR, SOFR, SONIA = rh.ESTR, fvf.preset("Sofr"), fvf.preset("Sonia")
US_FED = {"type": "UnitedStates", "market": int(ql.UnitedStates.FederalReserve)}
EVALUATION_DATES = rh.EVALUATION_DATES
OIS = rh.ois_tenor
OIS_DATES = rh.ois_dates


def helpers():
    """(name, spec) for the 60 OIS helpers."""
    W, M, Y, Dd = ql.Weeks, ql.Months, ql.Years, ql.Days
    MF, F, P, U = ql.ModifiedFollowing, ql.Following, ql.Preceding, ql.Unadjusted
    MAT, CUSTOM = ql.Pillar.MaturityDate, ql.Pillar.CustomDate
    SIMPLE = ql.RateAveraging.Simple
    averaging = rh.ocp.pricer_spec("ArithmeticAveraged", mean_reversion=0.02, volatility=0.15, by_approx=True)
    compounding = rh.ocp.pricer_spec("Compounding")
    discount = fvf.curve(0.031, reference=EVALUATION_DATES[0])
    out = [
        ("1W", OIS(0.0391, (1, W))),
        ("1M", OIS(0.0392, (1, M))),
        ("3M", OIS(0.0393, (3, M))),
        ("6M", OIS(0.0394, (6, M))),
        ("1Y", OIS(0.0395, (1, Y))),
        ("18M", OIS(0.0396, (18, M))),
        ("2Y", OIS(0.0397, (2, Y))),
        ("5Y", OIS(0.0398, (5, Y))),
        ("10Y", OIS(0.0399, (10, Y))),
        ("30Y", OIS(0.0400, (30, Y))),
        ("settlement days 0", OIS(0.0391, (1, Y), 0)),
        ("settlement days 1", OIS(0.0392, (1, Y), 1)),
        ("settlement days 3", OIS(0.0393, (1, Y), 3)),
        ("payment lag 2", OIS(0.0394, (2, Y), payment_lag=2)),
        ("payment lag 1, Preceding", OIS(0.0395, (2, Y), payment_lag=1, payment_convention=P)),
        ("payment lag 2, Modified Following", OIS(0.0396, (18, M), payment_lag=2, payment_convention=MF)),
        ("semiannual", OIS(0.0397, (2, Y), payment_frequency=ql.Semiannual)),
        ("quarterly", OIS(0.0398, (2, Y), payment_frequency=ql.Quarterly)),
        ("paying once", OIS(0.0399, (3, Y), payment_frequency=ql.Once)),
        ("payment calendar US, lag 2", OIS(0.0400, (1, Y), payment_lag=2, payment_calendar=rh.US_SETTLEMENT)),
        ("forward start 1M", OIS(0.0391, (1, Y), fwd_start=(1, M))),
        ("forward start 1Y", OIS(0.0392, (2, Y), fwd_start=(1, Y))),
        ("forward start -1W", OIS(0.0393, (1, Y), fwd_start=(-1, W))),
        ("spread 10bp", OIS(0.0394, (5, Y), spread=0.001)),
        ("pillar at maturity, payment lag 2", OIS(0.0395, (5, Y), payment_lag=2, pillar=MAT)),
        ("custom pillar", OIS(0.0396, (1, Y), pillar=CUSTOM, custom=D(1, 10, 2024))),
        ("custom pillar after the last relevant date", OIS(0.0397, (1, Y), pillar=CUSTOM, custom=D(1, 1, 2030))),
        ("custom pillar not given", OIS(0.0398, (1, Y), pillar=CUSTOM)),
        ("simple averaging", OIS(0.0399, (1, Y), averaging=SIMPLE)),
        ("end of month", OIS(0.0391, (1, Y), eom=True)),
        ("not end of month", OIS(0.0392, (1, Y), eom=False)),
        ("fixed leg semiannual", OIS(0.0393, (2, Y), fixed_payment_frequency=ql.Semiannual)),
        ("fixed leg on the UK calendar", OIS(0.0394, (2, Y), fixed_calendar=UK_EXCHANGE)),
        ("fixed leg 30/360", OIS(0.0395, (2, Y), fixed_dc=rh.THIRTY360_BB)),
        ("overnight leg on the US SOFR calendar", OIS(0.0396, (1, Y), overnight_calendar=US_SOFR)),
        ("lookback 2 with the shift", OIS(0.0397, (1, Y), lookback=2, shift=True)),
        ("lookback 2", OIS(0.0398, (1, Y), lookback=2)),
        ("lockout 2", OIS(0.0399, (1, Y), lockout=2)),
        ("lookback and lockout 2 with the shift", OIS(0.0391, (1, Y), lookback=2, lockout=2, shift=True)),
        ("telescopic", OIS(0.0392, (2, Y), telescopic=True)),
        ("telescopic, simple averaging", OIS(0.0393, (2, Y), telescopic=True, averaging=SIMPLE)),
        ("telescopic, lookback 2", OIS(0.0394, (1, Y), telescopic=True, lookback=2)),
        ("telescopic, lookback and lockout 2 with the shift",
         OIS(0.0395, (1, Y), telescopic=True, lookback=2, lockout=2, shift=True)),
        ("averaging pricer", OIS(0.0396, (1, Y), averaging=SIMPLE, pricer=averaging)),
        ("compounding pricer", OIS(0.0397, (1, Y), pricer=compounding)),
        ("forward rule", OIS(0.0398, (18, M), payment_frequency=ql.Semiannual, rule=ql.DateGeneration.Forward)),
        ("zero rule", OIS(0.0399, (3, Y), rule=ql.DateGeneration.Zero)),
        ("Following", OIS(0.0391, (1, Y), convention=F)),
        ("Unadjusted", OIS(0.0392, (1, Y), convention=U)),
        ("discounting curve", OIS(0.0393, (5, Y), discount=discount)),
        ("SOFR 5Y", OIS(0.0394, (5, Y), index=SOFR)),
        ("SOFR 3Y on the Federal Reserve calendar",
         OIS(0.0395, (3, Y), index=SOFR, payment_calendar=US_FED, fixed_calendar=US_FED, overnight_calendar=US_FED)),
        ("SONIA 2Y", OIS(0.0396, (2, Y), 0, index=SONIA)),
        ("SONIA 3M", OIS(0.0397, (3, M), 0, index=SONIA, payment_lag=1)),
        ("from 17 January 2024", OIS_DATES(0.0391, D(17, 1, 2024), D(17, 1, 2025))),
        ("from 17 January 2024 to 14 February", OIS_DATES(0.0392, D(17, 1, 2024), D(14, 2, 2024))),
        ("seasoned", OIS_DATES(0.0393, D(10, 1, 2024), D(10, 1, 2026))),
        ("from 2 April 2024, semiannual, lag 2",
         OIS_DATES(0.0394, D(2, 4, 2024), D(2, 4, 2027), payment_lag=2, payment_frequency=ql.Semiannual)),
        ("from 17 January 2024, custom pillar",
         OIS_DATES(0.0395, D(17, 1, 2024), D(17, 1, 2025), pillar=CUSTOM, custom=D(1, 12, 2024))),
        ("reversed dates", OIS_DATES(0.0396, D(17, 1, 2025), D(17, 1, 2024))),
    ]
    assert len(out) == 60
    return out


def outputs(h):
    out = {"built": True}
    out.update(rh.helper_dates(h))
    sw = h.swap()
    out.update({"swapStartDate": v.enc(sw.startDate()), "swapMaturityDate": v.enc(sw.maturityDate()),
                "fixedPaymentDates": [v.enc(cf.date()) for cf in sw.fixedLeg()],
                "overnightPaymentDates": [v.enc(cf.date()) for cf in sw.overnightLeg()]})
    return out


def helper_cases():
    out = []
    for today in EVALUATION_DATES:
        for name, spec in helpers():
            with v.case_settings(today) as st:
                try:
                    h = rh.make_value_helper(spec)
                except RuntimeError:
                    expected = {"built": dict(v.THROWS)}
                else:
                    expected = outputs(h)
                out.append({"name": f"{today.ISO()} {name}", "inputs": {"settings": st, "helper": spec},
                            "expected": expected})
    return out


ESTR_DATA = [((1, ql.Weeks), 0.03245), ((2, ql.Weeks), 0.03269), ((1, ql.Months), 0.03281), ((2, ql.Months), 0.0318),
             ((3, ql.Months), 0.03143), ((6, ql.Months), 0.03111), ((9, ql.Months), 0.03117), ((1, ql.Years), 0.03153),
             ((18, ql.Months), 0.03308), ((2, ql.Years), 0.0351), ((3, ql.Years), 0.03916), ((5, ql.Years), 0.04523),
             ((7, ql.Years), 0.04934), ((10, ql.Years), 0.0538), ((15, ql.Years), 0.05702), ((20, ql.Years), 0.05703),
             ((30, ql.Years), 0.05369)]


def estr_set(**kw):
    deposit = rh.deposit_tenor(0.0311, (1, ql.Days), 0, TARGET, ql.Following, False, A360)
    return [deposit] + [OIS(rate, tenor, payment_lag=2, **kw) for tenor, rate in ESTR_DATA]


def sofr_set():
    return [OIS(rate, tenor, index=SOFR, payment_lag=2, pillar=ql.Pillar.MaturityDate, payment_calendar=US_FED,
                fixed_calendar=US_FED, overnight_calendar=US_FED) for tenor, rate in ESTR_DATA[3:]]


# The helper sets: their names, specs, and the curve's traits, interpolator and day counter.
CURVE_SETS = [
    ("ESTR compounded", lambda: estr_set(), ("Discount", pw.LOG_LINEAR, A365)),
    ("ESTR averaged", lambda: estr_set(averaging=ql.RateAveraging.Simple), ("ForwardRate", pw.BACKWARD_FLAT, A365)),
    ("ESTR telescopic, lookback 2 with the shift", lambda: estr_set(telescopic=True, lookback=2, shift=True),
     ("ForwardRate", pw.BACKWARD_FLAT, A365)),
    ("SOFR, pillars at maturity", sofr_set, ("ZeroYield", pw.LINEAR, A365)),
]


# The ESTR fixings a 2-day lookback reads before Saturday 30 March 2024: its first value date, Wednesday 3 April,
# looks back past TARGET's Easter holidays to Thursday 28 March.
PAST_FIXINGS = [(D(27, 3, 2024), 0.03906), (D(28, 3, 2024), 0.03907)]


def curve_cases():
    out = []
    for today in EVALUATION_DATES:
        for name, specs_of, (traits, interpolator, dc) in CURVE_SETS:
            fixings = [(fvf.make_index(ESTR), PAST_FIXINGS)] if today == D(30, 3, 2024) and "lookback" in name else None
            with v.case_settings(today, fixings=fixings) as st:
                spec = {"traits": traits, "interpolator": interpolator, "form": "referenceDate",
                        "referenceDate": v.enc(today), "dayCounter": dc, "jumps": None, "jumpDates": None,
                        "bootstrap": None}
                out.append(pw.group_case(f"{today.ISO()} curve {name}", st, "curves", [spec], specs_of()))
    # a curve built 15 TARGET days after the evaluation date its helpers were built on, which re-dates them: QuantLib's
    # helpers re-date themselves when the evaluation date moves, so its values are fresh helpers' on the later date
    first = EVALUATION_DATES[0]
    later = ql.TARGET().advance(first, 15, ql.Days)
    with v.case_settings(later) as st:
        spec = {"traits": "Discount", "interpolator": pw.LOG_LINEAR, "form": "referenceDate",
                "referenceDate": v.enc(later), "dayCounter": A365, "jumps": None, "jumpDates": None,
                "bootstrap": None}
        out.append(pw.group_case(f"{later.ISO()} curve ESTR compounded built on {first.ISO()}", st, "moved", [spec],
                                 estr_set(), quote_errors=False, extra={"helpersEvaluationDate": first.serialNumber()}))
    return out


def cases():
    out = helper_cases() + curve_cases()
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    first = EVALUATION_DATES[0].ISO()
    for refused in ("custom pillar after the last relevant date", "custom pillar not given", "telescopic, lookback 2",
                    "reversed dates"):
        assert by_name[f"{first} {refused}"] == {"built": v.THROWS}, refused
    # the latest date stays the latest relevant date, whatever the pillar
    at_maturity = by_name[f"{first} pillar at maturity, payment lag 2"]
    assert at_maturity["pillarDate"] != at_maturity["latestDate"] == at_maturity["latestRelevantDate"]
    for c in out:
        if c["inputs"].get("kind") in ("curves", "moved"):
            assert c["expected"]["built"] == [True], c["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/oisratehelper.hpp", cases)
