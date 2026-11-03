# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's StrippedOptionletAdapter (ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp),
with the OptionletVolatilityStructure members the wheel binds: adapters on 4 strippers and 3 stripped optionlets of
termstructures/volatility/optionlet/optionletstripper1.py and strippedoptionlet.py, each read at about 30 points, and
the adapter QuantLib refuses (a stripper on a surface built on a fixed date, which has no settlement days).

Each adapter is read at its fixing times and strikes, between them, before the first time and past the last (refused
unless the call or the adapter extrapolates), outside the strikes (likewise) and by date, and its Black variance at the
same points by time; once with extrapolation enabled. A refused value is {"throws": true}. Its own outputs are its
reference date, maximum date and time, minimum and maximum strikes, volatility type, displacement, day counter's and
calendar's names and business-day convention. The volatility by tenor and the smile sections, which the wheel doesn't
bind, are the harness's. An input of another adapter QuantLib refuses only at its first volatility (one strike or one
date) or reads past (no strikes) is left out here: nanoql refuses it when the adapter is built, a convention and a
divergence of their own.

A case's inputs are {"settings", "source" ({"kind": "stripper", ...optionletstripper1's case} or {"kind":
"optionlet", "optionlet": ...strippedoptionlet's spec}), "enableExtrapolation", "byTime", "byDate"}; a point is [time,
strike, extrapolate] or [date, strike, extrapolate]. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/StrippedOptionletAdapterVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


os1 = _load("optionletstripper1", "termstructures/volatility/optionlet/optionletstripper1.py")
sol = _load("strippedoptionlet", "termstructures/volatility/optionlet/strippedoptionlet.py")
irx = os1.irx
cfs = os1.cfs


def source(spec):
    if spec["kind"] == "stripper":
        return os1.make({k: x for k, x in spec.items() if k != "kind"})
    return sol.make(spec["optionlet"])


def points(rnd, s):
    """About thirty points by time and by date, with their extrapolation flags (the wheel binds no tenor form)."""
    times = list(s.optionletFixingTimes())
    dates = list(s.optionletFixingDates())
    ks = list(s.optionletStrikes(0))
    t0, t1, k0, k1 = times[0], times[-1], ks[0], ks[-1]
    by_time = [[t, k, False] for t, k in zip(times, ks * (len(times) // len(ks) + 1))]
    by_time += [[rnd.uniform(t0, t1), rnd.uniform(k0, k1), False] for _ in range(12)]
    by_time += [[0.5 * t0, rnd.uniform(k0, k1), False], [t1 + 1.0, rnd.uniform(k0, k1), False],
                [t1 + 1.0, rnd.uniform(k0, k1), True], [rnd.uniform(t0, t1), k1 + 0.01, False],
                [rnd.uniform(t0, t1), k1 + 0.01, True], [rnd.uniform(t0, t1), k0 - 0.005, True], [-0.1, k0, True]]
    by_date = [[v.enc(d), rnd.uniform(k0, k1), False] for d in dates[:6]]
    by_date += [[v.enc(dates[-1] + 400), k0, False], [v.enc(dates[-1] + 400), k0, True]]
    return by_time, by_date


def volatility(a, point):
    x, k, extrapolate = point
    if isinstance(x, int):
        return v.attempt(lambda: a.volatility(ql.Date(x), k, extrapolate))
    return v.attempt(lambda: a.volatility(x, k, extrapolate))


def variance(a, point):
    x, k, extrapolate = point
    return v.attempt(lambda: a.blackVariance(x, k, extrapolate))


def adapter_case(rnd, st, name, spec, extrapolate):
    s = source(spec)
    a = ql.StrippedOptionletAdapter(s)
    if extrapolate:
        a.enableExtrapolation()
    by_time, by_date = points(rnd, s)
    return {"name": name,
            "inputs": {"settings": st, "source": spec, "enableExtrapolation": extrapolate, "byTime": by_time,
                       "byDate": by_date},
            "expected": {"built": True, "referenceDate": v.enc(a.referenceDate()), "maxDate": v.enc(a.maxDate()),
                         "maxTime": a.maxTime(), "minStrike": a.minStrike(), "maxStrike": a.maxStrike(),
                         "volatilityType": a.volatilityType(), "displacement": a.displacement(),
                         "dayCounter": a.dayCounter().name(), "calendar": a.calendar().name(),
                         "businessDayConvention": a.businessDayConvention(),
                         "volatilityByTime": [volatility(a, p) for p in by_time],
                         "volatilityByDate": [volatility(a, p) for p in by_date],
                         "blackVarianceByTime": [variance(a, p) for p in by_time]}}


def refused_case(st, name, spec):
    return {"name": f"refused: {name}", "inputs": {"settings": st, "source": spec},
            "expected": {"built": v.attempt(lambda: ql.StrippedOptionletAdapter(source(spec)) and True)}}


def sources():
    jun25, oct13 = ql.Date(16, 6, 2025), ql.Date(28, 10, 2013)
    flat4_25 = irx.curve_spec(jun25, 0.04)
    survey = cfs.surface_spec(os1.SURVEY_TENORS, os1.SURVEY_STRIKES, os1.SURVEY_VOLS, 0)
    survey2 = cfs.surface_spec(os1.SURVEY_TENORS, os1.SURVEY_STRIKES, os1.SURVEY_VOLS, 2)
    market = cfs.surface_spec(os1.MARKET_TENORS[:8], os1.MARKET_STRIKES, os1.MARKET_VOLS[:8], 0)
    normal = cfs.surface_spec(os1.SURVEY_TENORS, os1.SURVEY_STRIKES, [[0.0080, 0.0075, 0.0070, 0.0068]] * 4, 0)
    fixed = cfs.surface_spec(os1.SURVEY_TENORS, os1.SURVEY_STRIKES, os1.SURVEY_VOLS, settlement_date=jun25)
    rnd = random.Random(v.SEED)
    optionlets = {name: spec for name, spec in sol.optionlets(rnd)}

    def stripper(today, surface, crv, **kw):
        spec = os1.stripper(today, surface, crv, **kw)
        return today, {"kind": "stripper", **{k: x for k, x in spec.items() if k != "today"}}

    def optionlet(name):
        return sol.TODAY, {"kind": "optionlet", "optionlet": optionlets[name]}

    built = [
        ("survey stripper", stripper(jun25, survey, flat4_25)),
        ("survey stripper, two settlement days", stripper(jun25, survey2, flat4_25)),
        ("market stripper to 7Y", stripper(oct13, market, irx.curve_spec(oct13, 0.04))),
        ("normal stripper", stripper(jun25, normal, flat4_25, vol_type=ql.Normal)),
        ("stripped optionlet, three dates", optionlet("three dates, two strikes")),
        ("stripped optionlet, twelve dates", optionlet("twelve dates, six strikes")),
        ("stripped optionlet, displacement 1%", optionlet("shifted lognormal, displacement 1%")),
    ]
    refused = [("a stripper on a fixed-date surface", stripper(jun25, fixed, flat4_25))]
    return built, refused


def cases():
    rnd = random.Random(v.SEED + 1)
    built, refused = sources()
    out = []
    for name, (today, spec) in built:
        for extrapolate in (False, True):
            with v.case_settings(today) as st:
                out.append(adapter_case(rnd, st, name + (", extrapolating" if extrapolate else ""), spec, extrapolate))
    for name, (today, spec) in refused:
        with v.case_settings(today) as st:
            out.append(refused_case(st, name, spec))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp", cases)
