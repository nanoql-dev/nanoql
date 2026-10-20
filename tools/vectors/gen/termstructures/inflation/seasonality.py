# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's seasonalities (ql/termstructures/inflation/seasonality.hpp): MultiplicativePriceSeasonality
and KerkhofSeasonality.

- "factors" cases, one per seasonality: its base date, frequency and factors, and seasonalityFactor at 61 dates
  around its base date, from three years before to four years after, among them the base date itself, month ends and
  leap days; over monthly seasonalities of 12 and 24 factors (one based on a month end), quarterly ones of 4 and 8,
  every-fourth-month, bimonthly, semiannual, biweekly, weekly and daily ones, and Kerkhof's of 12 factors and of 24,
  which refuses every factor.
- "construction" cases: a seasonality built with no factors, with a number of factors that isn't a multiple of the
  frequency's, and with each frequency QuantLib refuses (annual, every fourth week, once, no frequency and other):
  {"throws": true}.
- "corrections" cases: a zero-inflation and a year-on-year curve of tools/vectors/gen/termstructures/
  inflationtermstructure.py (whose functions build them) with a seasonality: zeroRate or yoyRate at 37 dates, which the
  seasonality corrects (the wheel refuses correctZeroRate and correctYoYRate called directly); and the curve's
  construction where a multi-year seasonality is inconsistent with it, refused.

A seasonality spec is {"type": "MultiplicativePriceSeasonality", "baseDate", "frequency", "factors"} or {"type":
"KerkhofSeasonality", "baseDate", "factors"}, the frequency as QuantLib's integer. The wheel's classes are the C++
classes' proxies. Read back by tests/NanoQL.Tests/Vectors/TermStructures/SeasonalityVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# QuantLib's test-suite factors (inflation.cpp, checkSeasonality).
TEST_FACTORS = [1.003245, 1.000000, 0.999715, 1.000495, 1.000929, 0.998687,
                0.995949, 0.994682, 0.995949, 1.000519, 1.003705, 1.004186]


def multiplicative(base, frequency, factors):
    return {"type": "MultiplicativePriceSeasonality", "baseDate": v.enc(base), "frequency": int(frequency),
            "factors": list(factors)}


def kerkhof(base, factors):
    return {"type": "KerkhofSeasonality", "baseDate": v.enc(base), "factors": list(factors)}


def make_seasonality(spec):
    """The seasonality a spec describes, or None for a null spec."""
    if spec is None:
        return None
    if spec["type"] == "KerkhofSeasonality":
        return ql.KerkhofSeasonality(ql.Date(spec["baseDate"]), spec["factors"])
    return ql.MultiplicativePriceSeasonality(ql.Date(spec["baseDate"]), spec["frequency"], spec["factors"])


def ramp(n, start, step):
    """n distinct factors around 1."""
    return [start + step * ((7 * i) % n) for i in range(n)]


SEASONALITIES = [
    multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly, TEST_FACTORS),
    multiplicative(ql.Date(31, ql.January, 2007), ql.Monthly, TEST_FACTORS),
    multiplicative(ql.Date(15, ql.March, 2020), ql.Monthly, ramp(24, 0.994, 0.0005)),
    multiplicative(ql.Date(1, ql.February, 2023), ql.Quarterly, [1.002, 0.997, 1.001, 1.0]),
    multiplicative(ql.Date(30, ql.November, 2022), ql.Quarterly, ramp(8, 0.996, 0.001)),
    multiplicative(ql.Date(1, ql.April, 2021), ql.EveryFourthMonth, [1.003, 0.998, 0.999]),
    multiplicative(ql.Date(29, ql.February, 2024), ql.Bimonthly, ramp(6, 0.997, 0.001)),
    multiplicative(ql.Date(1, ql.July, 2019), ql.Semiannual, [1.004, 0.996]),
    multiplicative(ql.Date(6, ql.January, 2025), ql.Biweekly, ramp(26, 0.995, 0.0004)),
    multiplicative(ql.Date(3, ql.March, 2023), ql.Weekly, ramp(52, 0.99, 0.0004)),
    multiplicative(ql.Date(1, ql.January, 2022), ql.Daily, ramp(365, 0.999, 0.000005)),
    kerkhof(ql.Date(1, ql.June, 2023), TEST_FACTORS),
    kerkhof(ql.Date(15, ql.September, 2021), ramp(12, 0.997, 0.0006)),
    kerkhof(ql.Date(1, ql.June, 2023), ramp(24, 0.997, 0.0003)),
]

# Offsets in days from the base date: three years before to four years after.
OFFSETS = [0, 1, -1, 7, -7, 13, 14, 15, -14, 27, 28, 29, 30, 31, -30, -31, 59, 60, 61, 89, 90, 91, -90, 120, 121,
           151, 181, 182, 183, -183, 240, 270, 300, 330, 364, 365, 366, -365, -366, 400, 455, 500, 600, 700, 730, 731,
           -730, 800, 900, 1000, 1095, 1096, -1095, 1200, 1300, 1400, 1461, 1462, 1500, -1000, 1460]

REFUSED = [
    ("no factors", multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly, [])),
    ("13 monthly factors", multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly, [1.0] * 13)),
    ("5 quarterly factors", multiplicative(ql.Date(1, ql.January, 2024), ql.Quarterly, [1.0] * 5)),
    ("annual", multiplicative(ql.Date(1, ql.January, 2024), ql.Annual, [1.0])),
    ("every fourth week", multiplicative(ql.Date(1, ql.January, 2024), ql.EveryFourthWeek, [1.0] * 13)),
    ("once", multiplicative(ql.Date(1, ql.January, 2024), ql.Once, [1.0])),
    ("no frequency", multiplicative(ql.Date(1, ql.January, 2024), ql.NoFrequency, [1.0])),
    ("other frequency", multiplicative(ql.Date(1, ql.January, 2024), ql.OtherFrequency, [1.0])),
    ("Kerkhof, 11 factors", kerkhof(ql.Date(1, ql.January, 2024), [1.0] * 11)),
]


def factor_cases():
    out = []
    for k, spec in enumerate(SEASONALITIES):
        with v.case_settings(ql.Date(15, ql.June, 2024)) as st:
            s = make_seasonality(spec)
            base = s.seasonalityBaseDate()
            dates = [base + n for n in OFFSETS]
            out.append({
                "name": f"factors {spec['type']} {spec.get('frequency', 12)} x{len(spec['factors'])} "
                        f"{base.ISO()} / {k}",
                "inputs": {"settings": st, "kind": "factors", "seasonality": spec, "dates": v.enc(dates)},
                "expected": {
                    "seasonalityBaseDate": v.enc(s.seasonalityBaseDate()),
                    "frequency": int(s.frequency()),
                    "seasonalityFactors": list(s.seasonalityFactors()),
                    "seasonalityFactor": [v.attempt(lambda d=d: s.seasonalityFactor(d)) for d in dates]}})
    return out


def construction_cases():
    out = []
    for k, (label, spec) in enumerate(REFUSED):
        with v.case_settings(ql.Date(15, ql.June, 2024)) as st:
            out.append({
                "name": f"construction {label} / {k}",
                "inputs": {"settings": st, "kind": "construction", "seasonality": spec},
                "expected": {"construction": v.attempt(lambda spec=spec: make_seasonality(spec) and 0)}})
    return out


def correction_cases():
    import importlib.util
    spec_file = importlib.util.spec_from_file_location(
        "wheel_inflationtermstructure", os.path.join(v.GEN_ROOT, "termstructures", "inflationtermstructure.py"))
    its = importlib.util.module_from_spec(spec_file)
    spec_file.loader.exec_module(its)
    out = []
    for k, (label, evaluation_date, curve) in enumerate(its.seasonal_curves()):
        with v.case_settings(evaluation_date) as st:
            built = v.attempt(lambda curve=curve: its.make_curve(curve) and 0)
            expected = {"construction": built}
            if not isinstance(built, dict):
                c = its.make_curve(curve)
                dates = its.rate_dates(c)
                rate = c.zeroRate if curve["type"] == "ZeroInflationCurve" else c.yoyRate
                expected["rate"] = [v.attempt(lambda d=d: rate(d)) for d in dates]
            else:
                dates = []
            out.append({
                "name": f"corrections {label} / {k}",
                "inputs": {"settings": st, "kind": "corrections", "curve": curve, "dates": v.enc(dates)},
                "expected": expected})
    return out


def cases():
    return factor_cases() + construction_cases() + correction_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/inflation/seasonality.hpp", cases)
