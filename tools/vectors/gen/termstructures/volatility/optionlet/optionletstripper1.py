# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OptionletStripper1 (ql/termstructures/volatility/optionlet/optionletstripper1.hpp), with the
members of OptionletStripper and StrippedOptionletBase the wheel binds: a fresh surface and stripper per case.

The cases strip the surfaces of QuantLib's own stripper tests (test-suite/optionletstripper.cpp: the flat 10 x 10, the
16 x 13 market surface and its real negative-rate one) and the cap survey's 4 x 4 one, on flat and zero curves, with
both volatility types, a displacement, dontThrow on a surface that fails, a given switch strike, the at-par switch off,
and SOFR with a 3M optionlet frequency and a payment lag on its own surfaces (the wheel binds a payment lag
only with an optionlet frequency: the Ibor lag is the harness's); and QuantLib's
refusals (a displacement with normal volatilities, a surface shorter than the first cap, a 6M index on a 3M optionlet
frequency, an index without a curve, a strip that fails without dontThrow), each "built": {"throws": true}, built and
queried once, since QuantLib's strip runs at the first query. The outputs are the maturities, the fixing dates and
times, the at-the-money rates, every date's strikes and volatilities, the cap and floor prices and volatilities and the
optionlet prices (each matrix as rows), the switch strike, the settlement days (or {"throws": true} on a surface built
on a fixed date), the calendar's and day counter's names, the business-day convention, the volatility type, the
displacement, and the volatilities at the date past the last ({"throws": true}).

The stability filter runs on the implied volatilities: the strip is redone with every surface quote moved one ulp up and
then down, together, and, on a surface of at most 32 quotes, each quote alone up and down; a volatility is written only
where every move leaves it within 1e-12 x max(|a|, |b|, 1), a dropped one as null in its row and its [date, strike]
index listed in the case's "dropped" input, and the generator prints "dropped n of m outputs" per case and for the file.

A case's inputs are {"settings", "surface" (a surface spec of termstructures/volatility/capfloor/capfloortermvolsurface.py),
"curve" (the index's forwarding curve), "index" ({"type": "Euribor6M" | "Euribor3M" | "Sofr"}), "discount" (null,
QuantLib's empty handle, or a curve), "switchStrike" (null for Null<Rate>()), "accuracy", "maxIter", "type",
"displacement", "dontThrow", "optionletFrequency" (null or [length, units]), "paymentLag"}; a curve is a flat forward
of indexes/interestrateindex.py or {"type": "ZeroCurve", "dates", "rates", "dayCounter", "calendar"}, QuantLib's
ZeroCurve (InterpolatedZeroCurve<Linear>). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/OptionletStripper1VectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


irx = _load("interestrateindex", "indexes/interestrateindex.py")
cfs = _load("capfloortermvolsurface", "termstructures/volatility/capfloor/capfloortermvolsurface.py")

A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
TARGET = {"type": "TARGET"}
US_FED = {"type": "UnitedStates", "market": int(ql.UnitedStates.FederalReserve)}
D, M, Y = int(ql.Days), int(ql.Months), int(ql.Years)
STABLE = 1e-12
FEW = 32


def curve(spec):
    if spec is None:
        return ql.YieldTermStructureHandle()
    if spec.get("type") == "ZeroCurve":
        return ql.YieldTermStructureHandle(ql.ZeroCurve(
            [ql.Date(d) for d in spec["dates"]], spec["rates"], irx.day_counter(spec["dayCounter"]),
            irx.calendar(spec["calendar"])))
    return irx.handle(spec)


def index(spec, handle):
    return {"Euribor6M": ql.Euribor6M, "Euribor3M": ql.Euribor3M, "Sofr": ql.Sofr}[spec["type"]](handle)


def make_surface(spec):
    """cfs.make's surface, on the calendars of indexes/interestrateindex.py (the SOFR surfaces' Federal Reserve one
    among them)."""
    tenors = [ql.Period(n, u) for n, u in spec["optionTenors"]]
    first = spec["settlementDays"] if "settlementDays" in spec else ql.Date(spec["settlementDate"])
    s = ql.CapFloorTermVolSurface(first, irx.calendar(spec["calendar"]), spec["convention"], tenors, spec["strikes"],
                                  cfs.matrix(spec["volatilities"]), irx.day_counter(spec["dayCounter"]))
    if spec["enableExtrapolation"]:
        s.enableExtrapolation()
    return s


def make(case, quotes=None):
    """The stripper of a case, on the surface's quotes or on the given moved ones."""
    surface = dict(case["surface"])
    if quotes is not None:
        surface["volatilities"] = quotes
    s = make_surface(surface)
    idx = index(case["index"], curve(case["curve"]))
    args = [s, idx, v.NULL_REAL if case["switchStrike"] is None else case["switchStrike"], case["accuracy"],
            case["maxIter"], curve(case["discount"]), case["type"], case["displacement"], case["dontThrow"]]
    # The wheel's optional frequency takes no None, so a payment lag goes only with a frequency.
    freq = case["optionletFrequency"]
    if freq is not None:
        args += [ql.Period(freq[0], freq[1]), case["paymentLag"]]
    else:
        assert case["paymentLag"] == 0, "the wheel binds a payment lag only with an optionlet frequency"
    return ql.OptionletStripper1(*args)


def volatilities(s):
    return [list(s.optionletVolatilities(i)) for i in range(s.optionletMaturities())]


def matrix(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def moves(quotes):
    """The moves of the surface's quotes: all up, all down; then, on a small surface, each alone."""
    flat = [x for row in quotes for x in row]
    width = len(quotes[0])

    def shaped(xs):
        return [xs[i:i + width] for i in range(0, len(xs), width)]

    out = [shaped([math.nextafter(x, math.inf) for x in flat]), shaped([math.nextafter(x, -math.inf) for x in flat])]
    if len(flat) <= FEW:
        for i in range(len(flat)):
            for direction in (math.inf, -math.inf):
                moved = list(flat)
                moved[i] = math.nextafter(flat[i], direction)
                out.append(shaped(moved))
    return out


def stable(a, b):
    return a == b or abs(a - b) <= STABLE * max(abs(a), abs(b), 1.0)


def filtered(case, base):
    """The base volatilities, each kept only where every move leaves it stable; and the count dropped."""
    keep = [[True] * len(row) for row in base]
    for quotes in moves(case["surface"]["volatilities"]):
        try:
            moved = volatilities(make(case, quotes))
        except RuntimeError:
            return [[None] * len(row) for row in base], [[i, j] for i, row in enumerate(base) for j in range(len(row))]
        for i, row in enumerate(base):
            for j, x in enumerate(row):
                keep[i][j] = keep[i][j] and stable(x, moved[i][j])
    dropped = [[i, j] for i, row in enumerate(keep) for j, k in enumerate(row) if not k]
    return [[x if keep[i][j] else None for j, x in enumerate(row)] for i, row in enumerate(base)], dropped


def stripper_case(dropped_total, name, settings_kw, case):
    with v.case_settings(case["today"], **settings_kw) as st:
        inputs = {k: x for k, x in case.items() if k != "today"}
        s = make(case)
        base = volatilities(s)
        vols, dropped = filtered(case, base)
        outputs = sum(len(row) for row in base)
        dropped_total[0] += len(dropped)
        dropped_total[1] += outputs
        print(f"{name}: dropped {len(dropped)} of {outputs} outputs")
        n = s.optionletMaturities()
        expected = {
            "built": True,
            "optionletMaturities": n,
            "optionletFixingDates": v.enc(list(s.optionletFixingDates())),
            "optionletFixingTimes": list(s.optionletFixingTimes()),
            "atmOptionletRates": list(s.atmOptionletRates()),
            "optionletStrikes": [list(s.optionletStrikes(i)) for i in range(n)],
            "optionletVolatilities": v.enc(vols),
            "capFloorPrices": matrix(s.capFloorPrices()),
            "capFloorVolatilities": matrix(s.capFloorVolatilities()),
            "optionletPrices": matrix(s.optionletPrices()),
            "switchStrike": s.switchStrike(),
            "settlementDays": v.attempt(s.settlementDays),
            "calendar": s.calendar().name(),
            "dayCounter": s.dayCounter().name(),
            "businessDayConvention": s.businessDayConvention(),
            "volatilityType": s.volatilityType(),
            "displacement": s.displacement(),
            "pastLast": v.attempt(lambda: s.optionletVolatilities(n)),
        }
        return {"name": name, "inputs": {"settings": st, **inputs, "dropped": dropped}, "expected": expected}


def refused_case(name, settings_kw, case):
    with v.case_settings(case["today"], **settings_kw) as st:
        inputs = {k: x for k, x in case.items() if k != "today"}
        return {"name": f"refused: {name}", "inputs": {"settings": st, **inputs},
                "expected": {"built": v.attempt(lambda: make(case).optionletVolatilities(0) and True)}}


def stripper(today, surface, crv, idx="Euribor6M", discount=None, switch=None, accuracy=1e-6, max_iter=100,
             vol_type=ql.ShiftedLognormal, displacement=0.0, dont_throw=False, frequency=None, lag=0):
    return {"today": today, "surface": surface, "curve": crv, "index": {"type": idx}, "discount": discount,
            "switchStrike": switch, "accuracy": accuracy, "maxIter": max_iter, "type": int(vol_type),
            "displacement": displacement, "dontThrow": dont_throw, "optionletFrequency": frequency, "paymentLag": lag}


# The surfaces of QuantLib's stripper tests (optionletstripper.cpp:206-213, 258-308, 312-391) and the survey's.
MARKET_TENORS = [(1, Y), (18, M), (2, Y), (3, Y), (4, Y), (5, Y), (6, Y), (7, Y), (8, Y), (9, Y), (10, Y), (12, Y),
                 (15, Y), (20, Y), (25, Y), (30, Y)]
MARKET_STRIKES = [0.015, 0.0175, 0.02, 0.0225, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06, 0.07, 0.08, 0.1]
MARKET_VOLS = [
    [0.287, 0.274, 0.256, 0.245, 0.227, 0.148, 0.096, 0.09, 0.11, 0.139, 0.166, 0.19, 0.214],
    [0.303, 0.258, 0.22, 0.203, 0.19, 0.153, 0.126, 0.118, 0.147, 0.165, 0.18, 0.192, 0.212],
    [0.303, 0.257, 0.216, 0.196, 0.182, 0.154, 0.134, 0.127, 0.149, 0.166, 0.18, 0.192, 0.212],
    [0.305, 0.266, 0.226, 0.203, 0.19, 0.167, 0.151, 0.144, 0.16, 0.172, 0.183, 0.193, 0.209],
    [0.294, 0.261, 0.216, 0.201, 0.19, 0.171, 0.158, 0.151, 0.163, 0.172, 0.181, 0.188, 0.201],
    [0.276, 0.248, 0.212, 0.199, 0.189, 0.172, 0.16, 0.155, 0.162, 0.17, 0.177, 0.183, 0.195],
    [0.26, 0.237, 0.21, 0.198, 0.188, 0.172, 0.161, 0.156, 0.161, 0.167, 0.173, 0.179, 0.19],
    [0.25, 0.231, 0.208, 0.196, 0.187, 0.172, 0.162, 0.156, 0.16, 0.165, 0.17, 0.175, 0.185],
    [0.244, 0.226, 0.206, 0.195, 0.186, 0.171, 0.161, 0.156, 0.158, 0.162, 0.166, 0.171, 0.18],
    [0.239, 0.222, 0.204, 0.193, 0.185, 0.17, 0.16, 0.155, 0.156, 0.159, 0.163, 0.168, 0.177],
    [0.235, 0.219, 0.202, 0.192, 0.183, 0.169, 0.159, 0.154, 0.154, 0.156, 0.16, 0.164, 0.173],
    [0.227, 0.212, 0.197, 0.187, 0.179, 0.166, 0.156, 0.151, 0.149, 0.15, 0.153, 0.157, 0.165],
    [0.22, 0.206, 0.192, 0.183, 0.175, 0.162, 0.153, 0.147, 0.144, 0.144, 0.147, 0.151, 0.158],
    [0.211, 0.197, 0.185, 0.176, 0.168, 0.156, 0.147, 0.142, 0.138, 0.138, 0.14, 0.144, 0.151],
    [0.204, 0.192, 0.18, 0.171, 0.164, 0.152, 0.143, 0.138, 0.134, 0.134, 0.137, 0.14, 0.148],
    [0.2, 0.187, 0.176, 0.167, 0.16, 0.148, 0.14, 0.135, 0.131, 0.132, 0.135, 0.139, 0.146],
]
REAL_STRIKES = [-0.005, -0.0025, -0.00125, 0.0, 0.00125, 0.0025, 0.005, 0.01, 0.015, 0.02, 0.03, 0.05, 0.1]
REAL_RAW = [
    [0.49, 0.39, 0.34, 0.31, 0.34, 0.37, 0.50, 0.75, 0.99, 1.21, 1.64, 2.44, 4.29],
    [0.44, 0.36, 0.33, 0.31, 0.33, 0.35, 0.45, 0.65, 0.83, 1.00, 1.32, 1.93, 3.30],
    [0.40, 0.35, 0.33, 0.31, 0.33, 0.34, 0.41, 0.55, 0.69, 0.82, 1.08, 1.56, 2.68],
    [0.42, 0.39, 0.38, 0.37, 0.38, 0.39, 0.43, 0.54, 0.64, 0.74, 0.94, 1.31, 2.18],
    [0.46, 0.43, 0.42, 0.41, 0.42, 0.43, 0.47, 0.56, 0.66, 0.75, 0.93, 1.28, 2.07],
    [0.49, 0.47, 0.46, 0.45, 0.46, 0.47, 0.51, 0.59, 0.68, 0.76, 0.93, 1.25, 1.99],
    [0.51, 0.49, 0.49, 0.48, 0.49, 0.50, 0.54, 0.62, 0.70, 0.78, 0.94, 1.24, 1.94],
    [0.52, 0.51, 0.51, 0.51, 0.52, 0.53, 0.56, 0.63, 0.71, 0.79, 0.94, 1.23, 1.89],
    [0.53, 0.52, 0.52, 0.52, 0.53, 0.54, 0.57, 0.65, 0.72, 0.79, 0.94, 1.21, 1.83],
    [0.55, 0.54, 0.54, 0.54, 0.55, 0.56, 0.59, 0.66, 0.72, 0.79, 0.91, 1.15, 1.71],
    [0.56, 0.56, 0.56, 0.56, 0.57, 0.58, 0.61, 0.67, 0.72, 0.78, 0.89, 1.09, 1.59],
    [0.59, 0.58, 0.58, 0.59, 0.59, 0.60, 0.63, 0.68, 0.73, 0.78, 0.86, 1.03, 1.45],
    [0.61, 0.61, 0.61, 0.61, 0.62, 0.62, 0.64, 0.69, 0.73, 0.77, 0.85, 1.02, 1.44],
    [0.62, 0.62, 0.63, 0.63, 0.64, 0.64, 0.65, 0.69, 0.72, 0.76, 0.82, 0.96, 1.32],
    [0.62, 0.63, 0.63, 0.63, 0.65, 0.66, 0.66, 0.68, 0.72, 0.74, 0.80, 0.93, 1.25],
    [0.62, 0.62, 0.62, 0.62, 0.66, 0.67, 0.67, 0.67, 0.72, 0.72, 0.78, 0.90, 1.25],
]
DISCOUNTING = {"type": "ZeroCurve", "dayCounter": A365, "calendar": TARGET, "dates": [
    42124, 42129, 42143, 42221, 42254, 42282, 42313, 42345, 42374, 42405, 42465, 42495, 42587, 42681, 42772, 42860,
    43227, 43956, 44321, 44686, 45051, 45418, 45782, 46147, 46512, 47609, 49436, 51263, 53087, 56739, 60392], "rates": [
    -0.00292, -0.00292, -0.001441, -0.00117, -0.001204, -0.001212, -0.001223, -0.001236, -0.001221, -0.001238,
    -0.001262, -0.00125, -0.001256, -0.001233, -0.00118, -0.001108, -0.000619, 0.000833, 0.001617, 0.002414, 0.003183,
    0.003883, 0.004514, 0.005074, 0.005606, 0.006856, 0.00813, 0.008709, 0.009136, 0.009601, 0.009384]}
FORWARDING = {"type": "ZeroCurve", "dayCounter": A365, "calendar": TARGET, "dates": [
    42124, 42313, 42436, 42556, 42618, 42800, 42830, 42860, 43227, 43591, 43956, 44321, 44686, 45051, 45418, 45782,
    46147, 46512, 46878, 47245, 47609, 47973, 48339, 48704, 49069, 49436, 49800, 50165, 50530, 50895, 51263, 51627,
    51991, 52356, 52722, 53087, 54913, 56739, 60392, 64045], "rates": [
    0.000649, 0.000649, 0.000684, 0.000717, 0.000745, 0.000872, 0.000905, 0.000954, 0.001532, 0.002319, 0.003147,
    0.003949, 0.004743, 0.00551, 0.006198, 0.006798, 0.007339, 0.007832, 0.008242, 0.008614, 0.008935, 0.009205,
    0.009443, 0.009651, 0.009818, 0.009952, 0.010054, 0.010146, 0.010206, 0.010266, 0.010315, 0.010365, 0.010416,
    0.010468, 0.010519, 0.010571, 0.010757, 0.010806, 0.010423, 0.010217]}
SURVEY_TENORS = [(1, Y), (2, Y), (3, Y), (5, Y)]
SURVEY_STRIKES = [0.02, 0.03, 0.04, 0.05]
SURVEY_VOLS = [[0.30, 0.25, 0.22, 0.21], [0.28, 0.24, 0.21, 0.20], [0.26, 0.23, 0.20, 0.19], [0.24, 0.21, 0.19, 0.18]]
SOFR_TENORS = [(i, Y) for i in range(1, 11)]
SOFR_STRIKES = [0.03, 0.035, 0.04]
SOFR_CURVE = {"type": "ZeroCurve", "dayCounter": A365, "calendar": US_FED, "dates": [
    v.enc(ql.Date(*d)) for d in ((15, 4, 2025), (16, 4, 2025), (28, 4, 2025), (21, 5, 2025), (21, 7, 2025),
                                 (21, 10, 2025), (21, 4, 2026), (21, 4, 2027), (19, 4, 2028), (22, 4, 2030),
                                 (21, 4, 2032), (19, 4, 2035), (21, 4, 2037), (19, 4, 2040), (19, 4, 2045),
                                 (20, 4, 2050), (21, 4, 2055))], "rates": [
    x / 100.0 for x in (3.039872, 3.082092, 3.67902, 3.791077, 4.147655, 4.498917, 4.688082, 4.486636, 4.228873,
                        3.949601, 3.814579, 3.731412, 3.718794, 3.704788, 3.599069, 3.401666, 3.221372)]}


def cases_list():
    oct13, apr15, jun25, apr25 = ql.Date(28, 10, 2013), ql.Date(30, 4, 2015), ql.Date(16, 6, 2025), ql.Date(15, 4, 2025)
    flat4_13 = irx.curve_spec(oct13, 0.04)
    flat4_25 = irx.curve_spec(jun25, 0.04)
    flat_surface = cfs.surface_spec([(i, Y) for i in range(1, 11)], [(j + 1) / 100.0 for j in range(10)],
                                    [[0.18] * 10 for _ in range(10)], 0)
    market = cfs.surface_spec(MARKET_TENORS, MARKET_STRIKES, MARKET_VOLS, 0)
    real = cfs.surface_spec(MARKET_TENORS, REAL_STRIKES, [[x / 100 for x in row] for row in REAL_RAW], 0)
    survey = cfs.surface_spec(SURVEY_TENORS, SURVEY_STRIKES, SURVEY_VOLS, 0)
    survey2 = cfs.surface_spec(SURVEY_TENORS, SURVEY_STRIKES, SURVEY_VOLS, 2)
    survey_fixed = cfs.surface_spec(SURVEY_TENORS, SURVEY_STRIKES, SURVEY_VOLS, settlement_date=jun25)
    survey_normal = cfs.surface_spec(SURVEY_TENORS, SURVEY_STRIKES, [[0.0080, 0.0075, 0.0070, 0.0068]] * 4, 0)
    survey_bad = cfs.surface_spec(SURVEY_TENORS, SURVEY_STRIKES, [[0.60] * 4, [0.10] * 4, [0.10] * 4, [0.10] * 4], 0)
    sofr_flat = cfs.surface_spec(SOFR_TENORS, SOFR_STRIKES, [[30.0 / 10000.0] * 3 for _ in SOFR_TENORS], 2, cal=US_FED,
                                 convention=ql.ModifiedFollowing, dc=A360)
    sofr_skew = cfs.surface_spec(SOFR_TENORS, SOFR_STRIKES,
                                 [[(25.0 + 1.5 * (i + 1) + s) / 10000.0 for s in (4.0, 2.0, 0.0)] for i in range(10)],
                                 2, cal=US_FED, convention=ql.ModifiedFollowing, dc=A360)
    sofr_fixings = lambda: [(ql.Sofr(), [(ql.Date(15, 4, 2025), 3.04 / 100.0)])]  # noqa: E731
    built = [
        ("flat surface", {}, stripper(oct13, flat_surface, flat4_13)),
        ("market surface", {}, stripper(oct13, market, flat4_13)),
        ("market surface, indexed coupons", {"use_at_par_coupons": False}, stripper(oct13, market, flat4_13)),
        ("market surface, a 3% curve", {}, stripper(oct13, market, irx.curve_spec(oct13, 0.03))),
        ("real surface, normal", {}, stripper(apr15, real, FORWARDING, discount=DISCOUNTING, vol_type=ql.Normal)),
        ("real surface, shifted 3%, dontThrow", {},
         stripper(apr15, real, FORWARDING, discount=DISCOUNTING, displacement=0.03, dont_throw=True)),
        ("survey surface", {}, stripper(jun25, survey, flat4_25)),
        ("survey surface, two settlement days", {}, stripper(jun25, survey2, flat4_25)),
        ("survey surface on a fixed date", {}, stripper(jun25, survey_fixed, flat4_25)),
        ("survey surface, switch strike 3.5%", {}, stripper(jun25, survey, flat4_25, switch=0.035)),
        ("survey surface, normal", {}, stripper(jun25, survey_normal, flat4_25, vol_type=ql.Normal)),
        ("survey surface, dontThrow", {}, stripper(jun25, survey_bad, flat4_25, dont_throw=True)),
        ("survey surface, at par on 26 June", {}, stripper(ql.Date(26, 6, 2025), survey, irx.curve_spec(ql.Date(26, 6, 2025), 0.04))),
        ("survey surface, indexed on 26 June", {"use_at_par_coupons": False},
         stripper(ql.Date(26, 6, 2025), survey, irx.curve_spec(ql.Date(26, 6, 2025), 0.04))),
        ("survey surface, Euribor 3M", {}, stripper(jun25, survey, flat4_25, idx="Euribor3M")),
        ("survey surface, accuracy 1e-10", {}, stripper(jun25, survey, flat4_25, accuracy=1e-10, max_iter=200)),
        ("SOFR, flat, payment lag 2", {"fixings": sofr_fixings},
         stripper(apr25, sofr_flat, SOFR_CURVE, idx="Sofr", discount=SOFR_CURVE, vol_type=ql.Normal, frequency=[3, M],
                  lag=2)),
        ("SOFR, skewed, payment lag 2", {"fixings": sofr_fixings},
         stripper(apr25, sofr_skew, SOFR_CURVE, idx="Sofr", discount=SOFR_CURVE, vol_type=ql.Normal, frequency=[3, M],
                  lag=2)),
    ]
    refused = [
        ("normal with a displacement", {}, stripper(jun25, survey, flat4_25, vol_type=ql.Normal, displacement=0.01)),
        ("a surface to 6M", {}, stripper(jun25, cfs.surface_spec([(3, M), (6, M)], SURVEY_STRIKES, SURVEY_VOLS[:2], 0),
                                         flat4_25)),
        ("a 6M index on a 3M frequency", {}, stripper(jun25, survey, flat4_25, frequency=[3, M])),
        ("an index without a curve", {}, stripper(jun25, survey, None, discount=flat4_25)),
        ("a failed strip", {}, stripper(jun25, survey_bad, flat4_25)),
        ("SOFR without a frequency", {"fixings": sofr_fixings},
         stripper(apr25, sofr_flat, SOFR_CURVE, idx="Sofr", discount=SOFR_CURVE, vol_type=ql.Normal)),
    ]
    return built, refused


def settings_of(kw):
    out = dict(kw)
    if "fixings" in out:
        out["fixings"] = out["fixings"]()
    return out


def cases():
    built, refused = cases_list()
    out = []
    total = [0, 0]
    for name, kw, case in built:
        out.append(stripper_case(total, name, settings_of(kw), case))
    for name, kw, case in refused:
        out.append(refused_case(name, settings_of(kw), case))
    print(f"termstructures/volatility/optionlet/optionletstripper1: dropped {total[0]} of {total[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/optionletstripper1.hpp", cases)
