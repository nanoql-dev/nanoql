# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AbcdAtmVolCurve (ql/experimental/volatility/abcdatmvolcurve.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: abcd curves fitted to at-the-money volatilities at option
tenors on TARGET and the null calendar, at settlement days of 0 and 2, every volatility fitted and some left out,
humped, rising and falling, the last two with a first fit whose b is below 0, which QuantLib refits; each
curve's fitted a, b, c and d after its first calculation (QuantLib's two fits), its errors and end criterion, its
adjustment factors and option times, its maximum date and strike range, its volatility, variance and factor at times
between its first and last fitted tenors, and its volatility and variance at its fitted tenors (BlackAtmVolCurve's
period overloads) and at the dates of those times. Then an empty tenor list and a decreasing tenor, which the curve refuses.

The outputs nanoql fixes are left out: no time lies outside the fitted tenors, where QuantLib's factor refuses; no
market's fit steps to a trial point the abcd function's validation refuses, where QuantLib's fit throws and nanoql's
guard fits on, as the curve's second fit does on falling volatilities with the 6M and 2Y left out and on rising ones
over the null calendar.

**The stability filter**: every curve is refitted with every volatility moved one ulp up, then down, by math.nextafter,
together and each alone; an output is written only where every refit leaves it within 1e-12 x max(|base|, |moved|, 1)
(equal, for the end criterion and the dates), the others dropped by leaving their keys out, an array only whole; the
generator prints the dropped outputs per case and for the file.

The curve is tools/cxx/src/experimental/volatility/abcdatmvolcurve.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/AbcdAtmVolCurveCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
TOLERANCE = 1e-12
DAYS, WEEKS, MONTHS, YEARS = 0, 1, 2, 3
TENORS = [[1, MONTHS], [3, MONTHS], [6, MONTHS], [1, YEARS], [2, YEARS], [5, YEARS], [10, YEARS]]
VOLS = [0.30, 0.285, 0.27, 0.25, 0.23, 0.21, 0.19]
KEYS = ("settlementDays", "calendar", "tenors", "vols", "inclusion", "times", "queryTenors", "queryDates")


def spec(settlement, calendar, tenors, vols, inclusion, times):
    """The curve, queried at its fitted tenors and at the dates ⌊365t⌋ days after the evaluation date of its times."""
    fitted = [p for p, f in zip(tenors, inclusion if inclusion is not None else [True] * len(tenors)) if f]
    dates = [R + int(t * 365) for t in times]
    return dict(zip(KEYS, (settlement, calendar, tenors, vols, inclusion, times, fitted, dates)))


def stable(a, b):
    if isinstance(a, list):
        return len(a) == len(b) and all(stable(x, y) for x, y in zip(a, b))
    if isinstance(a, int) or a is None:
        return a == b
    return abs(a - b) <= TOLERANCE * max(abs(a), abs(b), 1.0)


def moved(vols):
    sets = [[math.nextafter(x, math.inf) for x in vols], [math.nextafter(x, -math.inf) for x in vols]]
    for i in range(len(vols)):
        for d in (math.inf, -math.inf):
            m = list(vols)
            m[i] = math.nextafter(vols[i], d)
            sets.append(m)
    return sets


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        humped = [0.20, 0.24, 0.26, 0.255, 0.24, 0.22, 0.20]
        humped2 = [0.15, 0.18, 0.21, 0.22, 0.215, 0.20, 0.18]
        rising = [0.18, 0.19, 0.20, 0.21, 0.215, 0.22, 0.222]
        falling = [0.30, 0.285, 0.27, 0.25, 0.23, 0.21, 0.19]
        out_flags = [True, True, False, True, False, True, True]
        setups = [
            ("humped, TARGET", spec(0, "TARGET", TENORS, humped, None, [0.1, 0.25, 0.5, 0.75, 1.0, 2.5, 5.0])),
            ("humped, two settlement days", spec(2, "TARGET", TENORS, humped, None, [0.2, 1.5, 3.0, 7.0])),
            ("humped, the null calendar", spec(0, "Null", TENORS, humped2, None, [0.3, 1.0, 4.0])),
            ("humped, the 6M and 2Y volatilities left out", spec(0, "TARGET", TENORS, humped2, out_flags,
                                                                 [0.25, 1.0, 6.0])),
            ("rising, a first fit with b below 0", spec(0, "TARGET", TENORS, rising, None, [0.1, 1.0, 8.0])),
            ("falling, two settlement days", spec(2, "TARGET", TENORS, falling, None, [0.2, 2.0, 9.0])),
        ]
        dropped_all = total_all = 0
        for name, s in setups:
            base = H.call("AbcdAtmVolCurve", *[s[k] for k in KEYS])
            keep = {k: True for k in base}
            for vols in moved(s["vols"]):
                r = H.call("AbcdAtmVolCurve", *[vols if k == "vols" else s[k] for k in KEYS])
                for k in keep:
                    keep[k] = keep[k] and stable(base[k], r[k])
            expected = {k: base[k] for k in keep if keep[k]}
            dropped = sum(1 for k in keep if not keep[k])
            print(f"{name}: dropped {dropped} of {len(keep)} outputs")
            dropped_all += dropped
            total_all += len(keep)
            out.append({"name": name, "inputs": {"settings": st, "curve": s}, "expected": expected})
        for name, s in [("no tenors (refused)", spec(0, "TARGET", [], [], None, [])),
                        ("a decreasing tenor (refused)", spec(0, "TARGET", [[1, YEARS], [6, MONTHS], [2, YEARS],
                                                                             [5, YEARS]], [0.2, 0.2, 0.2, 0.2],
                                                              None, []))]:
            try:
                H.call("AbcdAtmVolCurve", *[s[k] for k in KEYS])
                raise SystemExit(f"{name}: QuantLib priced it")
            except RuntimeError:
                out.append({"name": name, "inputs": {"settings": st, "curve": s}, "expected": {"a": {"throws": True}}})
        print(f"cxx/experimental/volatility/abcdatmvolcurve: dropped {dropped_all} of {total_all} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/abcdatmvolcurve.hpp", cases)
