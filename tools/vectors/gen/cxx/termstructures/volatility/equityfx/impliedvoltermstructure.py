# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ImpliedVolTermStructure (ql/termstructures/volatility/equityfx/impliedvoltermstructure.hpp),
each output computed by the C++ harness, since the wheel doesn't bind it: the structure another implies at a later
date, over a BlackVarianceSurface from 15 October 2026 (five dates from three months to five years, five strikes from 70
to 130, a smile rising in time), with QuantLib's default interpolation and extrapolation.

On Actual/365 (Fixed), Actual/360 and Actual/Actual (ISDA), at four implied reference dates (the original's, a month,
200 days and 400 days after it): its maximum date and strike range; at ten dates from the reference date to past the
surface's last, at five strikes inside and outside the surface's: its variance and volatility, extrapolating, and its
forward variance between consecutive dates; at six times from 0 to past the last date, its variance and volatility.

Left out (the S-van plan's §2.10): a day counter whose year fractions don't add (Thirty360), where nanoql reads a date
by date and QuantLib by its sum of times (VR4 (xxii)), and an implied reference date before the original's, which
accrues nothing before the original's in nanoql and QuantLib's variance at a negative time
(tests/NanoQL.Tests/Divergences/ImpliedVolTermStructureDateTests.cs).

Each case's inputs hold its settings, "originalReference", "dayCounter", "dates", "strikes", "vols" ([strike][date]),
"referenceDate", "queryDates", "queryTimes" and "queryStrikes". Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/ImpliedVolTermStructureCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R0 = ql.Date(15, 10, 2026)
DAY_COUNTERS = [("Actual/365 (Fixed)", {"type": "Actual365Fixed"}), ("Actual/360", {"type": "Actual360"}),
                ("Actual/Actual (ISDA)", {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)})]
DATES = [R0 + n for n in [91, 182, 365, 730, 1826]]
STRIKES = [70.0, 85.0, 100.0, 115.0, 130.0]
VOLS = [[0.18 + 0.01 * j + 0.15 * ((k - 100.0) / 100.0) ** 2 for j in range(len(DATES))] for k in STRIKES]
OFFSETS = [0, 30, 200, 400]
QUERY_STRIKES = [60.0, 85.0, 100.0, 122.5, 140.0]
QUERY_TIMES = [0.0, 0.01, 0.25, 1.0, 3.0, 6.0]
KEYS = ["maxDate", "minStrike", "maxStrike", "varianceAtDate", "volAtDate", "forwardVariance", "varianceAtTime",
        "volAtTime"]


def queries(reference):
    return [reference + n for n in [0, 1, 45, 91, 180, 366, 731, 1100, 1826, 2600]]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R0.serialNumber())
        for dname, dc in DAY_COUNTERS:
            for offset in OFFSETS:
                reference = R0 + offset
                inputs = {"settings": st, "originalReference": R0.serialNumber(), "dayCounter": dc,
                          "dates": v.enc(DATES), "strikes": STRIKES, "vols": VOLS,
                          "referenceDate": reference.serialNumber(), "queryDates": v.enc(queries(reference)),
                          "queryTimes": QUERY_TIMES, "queryStrikes": QUERY_STRIKES}
                values = H.call("ImpliedVolTermStructure::values", R0.serialNumber(), dc, v.enc(DATES), STRIKES,
                                VOLS, reference.serialNumber(), v.enc(queries(reference)), QUERY_TIMES,
                                QUERY_STRIKES)
                out.append({"name": f"{dname}, implied {offset} days on", "inputs": inputs,
                            "expected": {k: v.enc(values[k]) for k in KEYS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/impliedvoltermstructure.hpp", cases)
