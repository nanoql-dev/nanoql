# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SabrVolSurface (ql/experimental/volatility/sabrvolsurface.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness: surfaces on the evaluation date Friday 15 May 2026 on Euribor 6M and
3M forecast on flat rates of 3% and 2%, over abcd at-the-money curves on TARGET at 1M, 3M, 6M, 1Y and 2Y, with volatility
spreads flat in the tenors, rising toward the long tenors, and a skew; each surface's maximum date and strike range,
the index option dates of its tenors and its volatility spreads at each (the period overload), its volatility spreads
at dates before, between and past its tenors, and at times between its
first and last tenors its smile section's at-the-money level, exercise date and volatilities at the level and 0.25%,
0.5% and 1% on either side, and its at-the-money volatility. Then QuantLib's refusals: one strike spread, and
non-increasing strike spreads.

The outputs nanoql fixes are left out: each time's date, ⌊365t⌋ days after the reference date, is a TARGET business day,
where QuantLib's fixing read completes and nanoql's adjusted read is the identity; no time lies before the curve's first
tenor or past its last, where the abcd curve's factor refuses in both; the option dates are the surface's own, the
index's in both, not its at-the-money curve's by period.

**The stability filter**: every surface is refitted with every input moved one ulp up, then down, by math.nextafter,
together and each alone (the rate, each at-the-money volatility and each volatility spread); an output's element (a
time's level, exercise date, at-the-money volatility or row of section volatilities, a date's row of spreads, an option
date) is written only where every refit leaves it within 1e-12 x max(|base|, |moved|, 1) (equal, for the dates), the
indexes of each output's dropped elements in the case's inputs "dropped", as the swaption cube files have them; the
generator prints the counts per case and for the file. The section fits, four free parameters on five strikes, move
about 1e-9 under a one-ulp refit at nearly every time, and the abcd curve's fit under them moves its at-the-money
volatility as much, so the filter drops nearly every fitted volatility, as it drops the free fits of the SABR cube's
file.

The surface is tools/cxx/src/experimental/volatility/sabrvolsurface.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/SabrVolSurfaceCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.May, 2026)
R = TODAY.serialNumber()
TOLERANCE = 1e-12
M, Y = 2, 3
TENORS = [[1, M], [3, M], [6, M], [1, Y], [2, Y]]
SPREADS = [-0.01, -0.005, 0.0, 0.005, 0.01]
OFFSETS = [-0.01, -0.005, -0.0025, 0.0, 0.0025, 0.005, 0.01]
KEYS = ("indexMonths", "rate", "tenors", "atmVols", "atmRateSpreads", "volSpreads", "spreadDates", "queryTimes",
        "strikeOffsets")


def business_times(days):
    """The times (n + 0.5)/365 whose dates, n days after the reference date, are TARGET business days."""
    cal = ql.TARGET()
    out = []
    for n in days:
        while not cal.isBusinessDay(TODAY + n):
            n += 1
        out.append((n + 0.5) / 365.0)
    return out


QUERY_TIMES = business_times([40, 60, 100, 150, 250, 400, 550, 700])
SPREAD_DATES = [R + n for n in (1, 20, 45, 120, 300, 500, 800, 1000)]


def spec(index_months, rate, atm, vol_spreads, spreads=SPREADS):
    return dict(zip(KEYS, (index_months, rate, TENORS, atm, spreads, vol_spreads, SPREAD_DATES, QUERY_TIMES,
                           OFFSETS)))


def stable(a, b):
    if isinstance(a, list):
        return len(a) == len(b) and all(stable(x, y) for x, y in zip(a, b))
    if isinstance(a, int):
        return a == b
    return abs(a - b) <= TOLERANCE * max(abs(a), abs(b), 1.0)


def moved_specs(s):
    """The spec with every input moved one ulp up, then down, together, then each input alone."""
    def move(x, d):
        return math.nextafter(x, d)
    out = []
    for d in (math.inf, -math.inf):
        m = dict(s)
        m["rate"] = move(s["rate"], d)
        m["atmVols"] = [move(x, d) for x in s["atmVols"]]
        m["volSpreads"] = [[move(x, d) for x in r] for r in s["volSpreads"]]
        out.append(m)
    for d in (math.inf, -math.inf):
        m = dict(s)
        m["rate"] = move(s["rate"], d)
        out.append(m)
        for i in range(len(s["atmVols"])):
            m = dict(s)
            m["atmVols"] = list(s["atmVols"])
            m["atmVols"][i] = move(s["atmVols"][i], d)
            out.append(m)
        for i in range(len(s["volSpreads"])):
            for j in range(len(s["volSpreads"][i])):
                m = dict(s)
                m["volSpreads"] = [list(r) for r in s["volSpreads"]]
                m["volSpreads"][i][j] = move(s["volSpreads"][i][j], d)
                out.append(m)
    return out


def cases():
    out = []
    atm = [0.30, 0.28, 0.25, 0.22, 0.20]
    atm2 = [0.18, 0.21, 0.23, 0.22, 0.19]
    flat_smile = [[0.03, 0.012, 0.0, -0.005, -0.006]] * 5
    rising = [[0.02 + 0.004 * i, 0.008 + 0.002 * i, 0.0, -0.003 - 0.001 * i, -0.002 - 0.002 * i] for i in range(5)]
    skew = [[0.05, 0.022, 0.0, -0.015, -0.025]] * 5
    with cxx.connect() as H:
        st = H.settings(R)
        setups = [
            ("Euribor 6M at 3%, a smile flat in the tenors", spec(6, 0.03, atm, flat_smile)),
            ("Euribor 6M at 3%, a smile rising toward the long tenors", spec(6, 0.03, atm, rising)),
            ("Euribor 3M at 2%, a humped curve, a skew", spec(3, 0.02, atm2, skew)),
        ]
        dropped_all = total_all = 0
        for name, s in setups:
            base = H.call("SabrVolSurface::values", *[s[k] for k in KEYS])
            refits = [H.call("SabrVolSurface::values", *[m[k] for k in KEYS]) for m in moved_specs(s)]
            expected, dropped, lost, total = {}, {}, 0, 0
            for k, x in base.items():
                if isinstance(x, list):
                    out_k = [i for i, e in enumerate(x) if not all(stable(e, r[k][i]) for r in refits)]
                    if out_k:
                        dropped[k] = out_k
                    expected[k] = [e for i, e in enumerate(x) if i not in set(out_k)]
                    lost += len(out_k)
                    total += len(x)
                else:
                    if all(stable(x, r[k]) for r in refits):
                        expected[k] = x
                    else:
                        dropped[k] = [0]
                        lost += 1
                    total += 1
            detail = ", ".join(f"{k} {len(d)} of {len(base[k]) if isinstance(base[k], list) else 1}"
                               for k, d in dropped.items())
            print(f"{name}: dropped {lost} of {total} outputs" + (f" ({detail})" if detail else ""))
            dropped_all += lost
            total_all += total
            out.append({"name": name, "inputs": {"settings": st, "surface": s, "dropped": dropped},
                        "expected": expected})
        for name, s in [("one strike spread (refused)", spec(6, 0.03, atm, [[0.0]] * 5, spreads=[0.0])),
                        ("non-increasing strike spreads (refused)",
                         spec(6, 0.03, atm, flat_smile, spreads=[-0.01, -0.005, 0.0, 0.0, 0.01]))]:
            try:
                H.call("SabrVolSurface::values", *[s[k] for k in KEYS])
                raise SystemExit(f"{name}: QuantLib priced it")
            except RuntimeError:
                out.append({"name": name, "inputs": {"settings": st, "surface": s, "dropped": {}},
                            "expected": {"maxDate": {"throws": True}}})
        print(f"cxx/experimental/volatility/sabrvolsurface: dropped {dropped_all} of {total_all} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/sabrvolsurface.hpp", cases)
