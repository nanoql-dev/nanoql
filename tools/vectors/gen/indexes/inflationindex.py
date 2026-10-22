# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's inflation indexes (ql/indexes/inflationindex.hpp): ZeroInflationIndex, YoYInflationIndex
(quoted and as a ratio) and CPI's lagged observations, on the presets of ql/indexes/inflation/ and on indexes of
custom frequencies.

- "inspectors" cases, one per index: name, familyName, the region's name and code, revised, frequency, the availability
  lag (length and units), the currency's code, the fixing calendar's name, isValidFixingDate on a weekend, and for a
  year-on-year index ratio and the underlying's name (null for a quoted one).
- "store" cases: fixings added one at a time through addFixing, which writes each on every day of its inflation
  period, the store read back whole (its dates and values), and lastFixingDate; with forceOverwrite, and with a
  fixing equal to one stored; and "refused" cases, whose last addFixing conflicts with a stored fixing: its throw only,
  the store after a refused batch differing by design (docs/porting-playbook.md §7.2).
- "reads" cases, on indexes with no curve: needsForecast, fixing and pastFixing at about 40 dates around the
  evaluation date less the availability lag, refused where a fixing is missing or would need a forecast; and
  lastFixingDate.
- "forecasts" cases, on indexes with a curve of tools/vectors/gen/termstructures/inflationtermstructure.py: needsForecast
  and fixing at about 40 dates, most of them forecast through the curve, and the same fixings through clone() of the
  index on a second curve, and that clone's name; one curve's base date has no fixing, so its forecasts are refused.
- "lagged" cases: CPI::laggedFixing on zero indexes and CPI::laggedYoYRate on year-on-year ones, at about 50 dates for
  lags of 0, 2 and 3 months (a quarter's indexes: 0, 3 and 6) and flat and linear interpolation, as
  [date][lag][interpolation], on stored fixings and on forecasts. For a ratio index under linear interpolation, QuantLib
  chooses between interpolating the underlying first and interpolating the ratios by whether the unlagged date needs a
  forecast, where the port asks whether the latest fixing the first form reads does (tests/NanoQL.Tests/Divergences/
  LaggedYoYRateBranchTests.cs): the dates where the two questions disagree are left out, the count printed per case.

The fixings of a case ("fixings", [[serial, value], ...], for the index or a ratio's underlying) are added through the
index's addFixing in that order, which the test repeats through IndexFixings.AddFixing. An index spec is {"type"} naming
a preset with its "curve"; {"type": "ZeroInflationIndex" or "YoYInflationIndex", "familyName", "region",
"revised", "frequency", "availabilityLag", "currency", "curve"}, the region {"type": "CustomRegion", "name", "code"}
or {"type"} naming a preset region, the availability lag [length, units] and the currency its code; or {"type":
"YoYRatio", "underlying", "curve"}. A preset taking arguments before its curve (AUCPI) has them in its spec too
("frequency", "revised"). A curve is a spec of inflationtermstructure.py or null. The wheel's indexes are the C++
classes' proxies. Read back by tests/NanoQL.Tests/Vectors/Indexes/InflationIndexVectorTests.cs. The inflation presets'
generators (tools/vectors/gen/indexes/inflation/) write their cases through the case families here, each on its own
table, their presets added to PRESETS of their own load of this module."""
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
    "wheel_inflationtermstructure", os.path.join(v.GEN_ROOT, "termstructures", "inflationtermstructure.py"))
its = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(its)

M, Y = int(ql.Months), int(ql.Years)
PRESETS = {"UKRPI": ql.UKRPI, "USCPI": ql.USCPI, "EUHICP": ql.EUHICP, "EUHICPXT": ql.EUHICPXT,
           "YYUKRPI": ql.YYUKRPI, "YYUSCPI": ql.YYUSCPI, "YYEUHICP": ql.YYEUHICP, "YYEUHICPXT": ql.YYEUHICPXT}
CURRENCIES = {"GBP": ql.GBPCurrency, "EUR": ql.EURCurrency, "USD": ql.USDCurrency}
REGIONS = {"UKRegion": lambda: ql.UKRPI().region(), "EURegion": lambda: ql.EUHICP().region(),
           "USRegion": lambda: ql.USCPI().region()}


def preset(name, curve=None, **args):
    return {"type": name, **args, "curve": curve}


def custom(kind, family, region, revised, frequency, lag, currency, curve=None):
    return {"type": kind, "familyName": family, "region": region, "revised": revised, "frequency": int(frequency),
            "availabilityLag": lag, "currency": currency, "curve": curve}


def ratio(underlying, curve=None):
    return {"type": "YoYRatio", "underlying": underlying, "curve": curve}


def is_yoy(spec):
    return spec["type"] in ("YoYInflationIndex", "YoYRatio") or spec["type"].startswith("YY")


def make_region(spec):
    if spec["type"] == "CustomRegion":
        return ql.CustomRegion(spec["name"], spec["code"])
    return REGIONS[spec["type"]]()


def make_index(spec):
    """The index a spec describes, forecasting through its curve when it has one."""
    yoy = is_yoy(spec)
    curve = spec["curve"]
    if yoy:
        h = ql.YoYInflationTermStructureHandle(its.make_curve(curve)) if curve else ql.YoYInflationTermStructureHandle()
    else:
        h = ql.ZeroInflationTermStructureHandle(its.make_curve(curve)) if curve else ql.ZeroInflationTermStructureHandle()
    t = spec["type"]
    if t in PRESETS:
        return PRESETS[t](*[spec[k] for k in ("frequency", "revised") if k in spec], h)
    if t == "YoYRatio":
        return ql.YoYInflationIndex(make_index(spec["underlying"]), h)
    cls = ql.YoYInflationIndex if t == "YoYInflationIndex" else ql.ZeroInflationIndex
    lag = ql.Period(spec["availabilityLag"][0], spec["availabilityLag"][1])
    return cls(spec["familyName"], make_region(spec["region"]), spec["revised"], spec["frequency"], lag,
               CURRENCIES[spec["currency"]](), h)


def store_owner(index, spec):
    """The index a case's fixings are added to: a ratio's underlying, or the index itself."""
    return index.underlyingIndex() if spec["type"] == "YoYRatio" else index


AU = {"type": "CustomRegion", "name": "Australia", "code": "AU"}
XX = {"type": "CustomRegion", "name": "Testland", "code": "XX"}
QUARTERLY_CPI = custom("ZeroInflationIndex", "QCPI", AU, False, ql.Quarterly, [2, M], "USD")
SEMIANNUAL_YY = custom("YoYInflationIndex", "YY_SCPI", XX, True, ql.Semiannual, [3, M], "EUR")
ANNUAL_CPI = custom("ZeroInflationIndex", "ACPI", {"type": "EURegion"}, True, ql.Annual, [1, Y], "EUR")

INDEXES = [preset(n) for n in PRESETS] + [
    QUARTERLY_CPI, SEMIANNUAL_YY, ANNUAL_CPI, ratio(preset("UKRPI")), ratio(QUARTERLY_CPI),
    custom("YoYInflationIndex", "YY_MCPI", {"type": "UKRegion"}, False, ql.Monthly, [15, int(ql.Days)], "GBP")]


def monthly(first, last, start, growth):
    """Monthly fixings on the 15th of each month from first to last, growing at a non-periodic rate."""
    out, d, value = [], first, start
    while d <= last:
        out.append([v.enc(d), value])
        value = value * (1.0 + growth * (1 + d.month() % 5) / 3.0) + 0.0011 * (d.year() % 3)
        d = d + ql.Period(1, ql.Months)
    return out


def apply(index, fixings):
    for serial, value in fixings:
        index.addFixing(ql.Date(serial), value)


def series(index):
    ts = index.timeSeries()
    return v.enc(list(ts.dates())), list(ts.values())


def inspector_cases(indexes=INDEXES):
    out = []
    for k, spec in enumerate(indexes):
        with v.case_settings(ql.Date(15, ql.June, 2024)) as st:
            index = make_index(spec)
            lag = index.availabilityLag()
            yoy = is_yoy(spec)
            out.append({
                "name": f"inspectors {index.name()} / {k}",
                "inputs": {"settings": st, "kind": "inspectors", "index": spec,
                           "weekend": v.enc(ql.Date(15, ql.June, 2024))},
                "expected": {
                    "name": index.name(),
                    "familyName": index.familyName(),
                    "regionName": index.region().name(),
                    "regionCode": index.region().code(),
                    "revised": index.revised(),
                    "frequency": int(index.frequency()),
                    "availabilityLagLength": lag.length(),
                    "availabilityLagUnits": int(lag.units()),
                    "currency": index.currency().code(),
                    "fixingCalendar": index.fixingCalendar().name(),
                    "isValidFixingDate": index.isValidFixingDate(ql.Date(15, ql.June, 2024)),
                    "ratio": index.ratio() if yoy else None,
                    "underlying": index.underlyingIndex().name() if yoy and index.ratio() else None}})
    return out


UK_FIXINGS = monthly(ql.Date(15, ql.January, 2022), ql.Date(15, ql.April, 2024), 300.0, 0.0012)
QUARTERLY_FIXINGS = [[v.enc(ql.Date(17, ql.February, 2022)), 101.2], [v.enc(ql.Date(30, ql.June, 2022)), 102.0],
                     [v.enc(ql.Date(1, ql.July, 2022)), 103.1], [v.enc(ql.Date(31, ql.December, 2022)), 103.9],
                     [v.enc(ql.Date(1, ql.January, 2023)), 104.4], [v.enc(ql.Date(15, ql.May, 2023)), 104.9],
                     [v.enc(ql.Date(29, ql.September, 2023)), 105.5], [v.enc(ql.Date(2, ql.November, 2023)), 106.0],
                     [v.enc(ql.Date(14, ql.February, 2024)), 106.8]]
YY_FIXINGS = [[s, round(0.02 + (val - 300.0) / 4000.0, 6)] for s, val in UK_FIXINGS]
SEMIANNUAL_FIXINGS = [[v.enc(ql.Date(1, ql.March, 2023)), 0.031], [v.enc(ql.Date(31, ql.December, 2023)), 0.027],
                      [v.enc(ql.Date(15, ql.April, 2024)), 0.024]]

# (label, evaluation date, index spec, fixings, then: further adds [(serial, value, force)], the last refused)
STORES = [
    ("UK RPI, 12 months", ql.Date(15, ql.June, 2024), preset("UKRPI"), UK_FIXINGS[-12:], [], False),
    ("quarterly", ql.Date(15, ql.June, 2024), QUARTERLY_CPI, QUARTERLY_FIXINGS[:5], [], False),
    ("quoted year-on-year", ql.Date(15, ql.June, 2024), preset("YYEUHICP"), YY_FIXINGS[-6:], [], False),
    ("semiannual year-on-year", ql.Date(15, ql.June, 2024), SEMIANNUAL_YY, SEMIANNUAL_FIXINGS, [], False),
    ("annual", ql.Date(15, ql.June, 2024), ANNUAL_CPI, [[v.enc(ql.Date(30, ql.June, 2023)), 117.5]], [], False),
    ("overwrite", ql.Date(15, ql.June, 2024), preset("UKRPI"), UK_FIXINGS[-3:],
     [(UK_FIXINGS[-2][0] + 3, 999.5, True)], False),
    ("equal value again", ql.Date(15, ql.June, 2024), preset("UKRPI"), UK_FIXINGS[-3:],
     [(UK_FIXINGS[-2][0] - 10, UK_FIXINGS[-2][1], False)], False),
    ("duplicate refused", ql.Date(15, ql.June, 2024), preset("UKRPI"), UK_FIXINGS[-3:],
     [(UK_FIXINGS[-2][0] + 5, 1.0, False)], True),
    ("duplicate refused, quarterly", ql.Date(15, ql.June, 2024), QUARTERLY_CPI, QUARTERLY_FIXINGS[:3],
     [(QUARTERLY_FIXINGS[1][0] - 40, 50.0, False)], True),
]


def store_cases(stores=STORES):
    out = []
    for k, (label, evaluation_date, spec, fixings, adds, refused) in enumerate(stores):
        with v.case_settings(evaluation_date) as st:
            index = make_index(spec)
            apply(index, fixings)
            expected = {}
            for serial, value, force in adds[:-1] if refused else adds:
                index.addFixing(ql.Date(serial), value, force)
            if refused:
                serial, value, force = adds[-1]
                expected["refusedAdd"] = v.attempt(lambda: index.addFixing(ql.Date(serial), value, force) and 0)
            else:
                dates, values = series(index)
                expected["storedDates"] = dates
                expected["storedValues"] = values
                expected["lastFixingDate"] = v.enc(index.lastFixingDate())
            out.append({"name": f"store {label} / {k}",
                        "inputs": {"settings": st, "kind": "store", "index": spec, "fixings": fixings,
                                   "adds": [[s, val, f] for s, val, f in adds]},
                        "expected": expected})
    return out


def around(center, before, after, step):
    return [center + n for n in range(-before, after + 1, step)]


# (label, evaluation date, index spec, fixings)
READS = [
    ("UK RPI", ql.Date(15, ql.June, 2024), preset("UKRPI"), UK_FIXINGS),
    ("UK RPI, May published", ql.Date(28, ql.June, 2024), preset("UKRPI"),
     UK_FIXINGS + [[v.enc(ql.Date(1, ql.May, 2024)), 330.1]]),
    ("quarterly", ql.Date(10, ql.April, 2024), QUARTERLY_CPI, QUARTERLY_FIXINGS),
    ("quoted year-on-year", ql.Date(15, ql.June, 2024), preset("YYUKRPI"), YY_FIXINGS),
    ("semiannual year-on-year", ql.Date(15, ql.August, 2024), SEMIANNUAL_YY, SEMIANNUAL_FIXINGS),
    ("ratio of UK RPI", ql.Date(15, ql.June, 2024), ratio(preset("UKRPI")), UK_FIXINGS),
    ("ratio of a quarterly index", ql.Date(10, ql.April, 2024), ratio(QUARTERLY_CPI), QUARTERLY_FIXINGS),
    ("no fixings", ql.Date(15, ql.June, 2024), preset("USCPI"), []),
]


def read_cases(reads=READS):
    out = []
    for k, (label, evaluation_date, spec, fixings) in enumerate(reads):
        with v.case_settings(evaluation_date) as st:
            index = make_index(spec)
            apply(store_owner(index, spec), fixings)
            dates = around(evaluation_date - index.availabilityLag(), 400, 120, 13)
            out.append({"name": f"reads {label} / {k}",
                        "inputs": {"settings": st, "kind": "reads", "index": spec, "fixings": fixings,
                                   "dates": v.enc(dates)},
                        "expected": {
                            "lastFixingDate": v.attempt(index.lastFixingDate),
                            "needsForecast": [v.attempt(lambda d=d: index.needsForecast(d)) for d in dates],
                            "fixing": [v.attempt(lambda d=d: index.fixing(d)) for d in dates],
                            "pastFixing": [v.attempt(lambda d=d: index.pastFixing(d)) for d in dates]}})
    return out


ZERO_UK = its.curve_spec("ZeroInflationCurve", ql.Date(15, ql.June, 2024),
                         its.monthly_nodes(ql.Date(1, ql.April, 2024), (1, 2, 3, 5, 10, 20)),
                         [0.035, 0.035, 0.034, 0.033, 0.032, 0.031, 0.0305], ql.Monthly, its.A365)
ZERO_UK_SEASONAL = dict(ZERO_UK, seasonality=its.seas.multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly,
                                                                     its.FACTORS), enableExtrapolation=True)
ZERO_UK_LATE = dict(ZERO_UK, dates=v.enc(its.monthly_nodes(ql.Date(1, ql.May, 2024), (1, 2, 5, 10))),
                    rates=[0.03, 0.03, 0.031, 0.032, 0.033])
ZERO_QUARTERLY = its.curve_spec("ZeroInflationCurve", ql.Date(10, ql.April, 2024),
                                its.monthly_nodes(ql.Date(1, ql.January, 2024), (1, 2, 5, 10)),
                                [0.021, 0.022, 0.023, 0.025, 0.026], ql.Quarterly, its.THIRTY360_BB)
YOY_UK = its.curve_spec("YoYInflationCurve", ql.Date(15, ql.June, 2024),
                        its.monthly_nodes(ql.Date(1, ql.April, 2024), (1, 2, 3, 5, 10)),
                        [0.031, 0.0305, 0.03, 0.029, 0.028, 0.027], ql.Monthly, its.A365, extrapolate=True)
YOY_UK_SEASONAL = dict(YOY_UK, seasonality=its.seas.multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly,
                                                                   its.FACTORS))

# (label, evaluation date, index spec, fixings, the clone's curve)
FORECASTS = [
    ("UK RPI", ql.Date(15, ql.June, 2024), preset("UKRPI", ZERO_UK), UK_FIXINGS, ZERO_UK_SEASONAL),
    ("UK RPI, seasonal", ql.Date(15, ql.June, 2024), preset("UKRPI", ZERO_UK_SEASONAL), UK_FIXINGS, ZERO_UK),
    ("UK RPI, base fixing missing", ql.Date(15, ql.June, 2024), preset("UKRPI", ZERO_UK_LATE), UK_FIXINGS, ZERO_UK),
    ("quarterly", ql.Date(10, ql.April, 2024), dict(QUARTERLY_CPI, curve=ZERO_QUARTERLY), QUARTERLY_FIXINGS,
     dict(ZERO_QUARTERLY, enableExtrapolation=True)),
    ("quoted year-on-year", ql.Date(15, ql.June, 2024), preset("YYUKRPI", YOY_UK), YY_FIXINGS, YOY_UK_SEASONAL),
    ("ratio of UK RPI", ql.Date(15, ql.June, 2024), ratio(preset("UKRPI"), YOY_UK), UK_FIXINGS, YOY_UK_SEASONAL),
]


def forecast_cases(forecasts=FORECASTS):
    out = []
    for k, (label, evaluation_date, spec, fixings, clone_curve) in enumerate(forecasts):
        with v.case_settings(evaluation_date) as st:
            index = make_index(spec)
            apply(store_owner(index, spec), fixings)
            yoy = is_yoy(spec)
            h = (ql.YoYInflationTermStructureHandle if yoy else ql.ZeroInflationTermStructureHandle)(
                its.make_curve(clone_curve))
            clone = index.clone(h)
            dates = around(evaluation_date, 90, 3700, 97)
            out.append({"name": f"forecasts {label} / {k}",
                        "inputs": {"settings": st, "kind": "forecasts", "index": spec, "fixings": fixings,
                                   "dates": v.enc(dates), "cloneCurve": clone_curve},
                        "expected": {
                            "needsForecast": [v.attempt(lambda d=d: index.needsForecast(d)) for d in dates],
                            "fixing": [v.attempt(lambda d=d: index.fixing(d)) for d in dates],
                            "cloneName": clone.name(),
                            "cloneFixing": [v.attempt(lambda d=d: clone.fixing(d)) for d in dates]}})
    return out


# (label, evaluation date, index spec, fixings, lags)
LAGGED = [
    ("UK RPI, past", ql.Date(15, ql.June, 2024), preset("UKRPI"), UK_FIXINGS, [[0, M], [2, M], [3, M]]),
    ("UK RPI, forecast", ql.Date(15, ql.June, 2024), preset("UKRPI", ZERO_UK_SEASONAL), UK_FIXINGS,
     [[0, M], [2, M], [3, M]]),
    ("quarterly", ql.Date(10, ql.April, 2024), dict(QUARTERLY_CPI, curve=dict(ZERO_QUARTERLY, enableExtrapolation=True)),
     QUARTERLY_FIXINGS, [[0, M], [3, M], [6, M]]),
    ("quoted year-on-year", ql.Date(15, ql.June, 2024), preset("YYUKRPI", YOY_UK), YY_FIXINGS,
     [[0, M], [2, M], [3, M]]),
    ("ratio of UK RPI", ql.Date(15, ql.June, 2024), ratio(preset("UKRPI"), YOY_UK), UK_FIXINGS,
     [[0, M], [2, M], [3, M]]),
    ("ratio of a quarterly index", ql.Date(10, ql.April, 2024), ratio(QUARTERLY_CPI), QUARTERLY_FIXINGS,
     [[0, M], [3, M], [6, M]]),
]
INTERPOLATIONS = [ql.CPI.Flat, ql.CPI.Linear]


def lagged_cases(lagged=LAGGED):
    out = []
    for k, (label, evaluation_date, spec, fixings, lags) in enumerate(lagged):
        with v.case_settings(evaluation_date) as st:
            index = make_index(spec)
            apply(store_owner(index, spec), fixings)
            yoy = is_yoy(spec)
            observe = ql.CPI.laggedYoYRate if yoy else ql.CPI.laggedFixing
            candidates = around(evaluation_date, 420, 1500, 37)
            dates, dropped = [], 0
            for d in candidates:
                if spec["type"] == "YoYRatio" and _branches_differ(index, d, lags):
                    dropped += 1
                    continue
                dates.append(d)
            if dropped:
                print(f"lagged {label}: {dropped} of {len(candidates)} dates left out (the linear ratio branch)")
            out.append({"name": f"lagged {label} / {k}",
                        "inputs": {"settings": st, "kind": "lagged", "index": spec, "fixings": fixings,
                                   "dates": v.enc(dates), "lags": lags,
                                   "interpolations": [int(i) for i in INTERPOLATIONS]},
                        "expected": {"lagged": [
                            [[v.attempt(lambda d=d, lag=lag, i=i: observe(index, d, ql.Period(lag[0], lag[1]), i))
                              for i in INTERPOLATIONS] for lag in lags] for d in dates]}})
    return out


def _branches_differ(index, d, lags):
    """Whether QuantLib's choice of the linear ratio branch, by the unlagged date, differs from the port's, by the latest
    fixing the branch reads, for any of the lags."""
    for length, units in lags:
        lag = ql.Period(length, units)
        fixing_period = ql.inflationPeriod(d - lag, index.frequency())
        interpolation_period = ql.inflationPeriod(d, index.frequency())
        latest = fixing_period[0] if d == interpolation_period[0] else fixing_period[1] + 1
        if index.needsForecast(d) != index.needsForecast(latest):
            return True
    return False


def cases():
    return inspector_cases() + store_cases() + read_cases() + forecast_cases() + lagged_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/inflationindex.hpp", cases)
