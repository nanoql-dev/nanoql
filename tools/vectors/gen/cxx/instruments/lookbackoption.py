# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's continuous lookback options (ql/instruments/lookbackoption.hpp, .cpp), whose argument
validation the wheel doesn't expose, each output computed by the C++ harness, on the evaluation date 15 October 2026,
each option European in a year: the floating and fixed options with a running extremum of 0, 100, -0.5 and -1
("nonnegative prior extremum required: ... not allowed"); the partial floating option's calls at lambda 1, 1.5 and 0.8
("lambda should be greater than or equal to 1 for calls") and puts at 1, 0.6 and 1.2 ("lambda should be smaller than
or equal to 1 for puts"), the lookback ending 180 and 365 days on, and 366 ("lookback start date must be earlier than
exercise date"); and the partial fixed option's lookback starting 0, 180, 365 and 366 days on.

Left out: QuantLib's partial validation dereferences a European exercise and a floating payoff unchecked, where nanoql
refuses before the read; and the null extremum, the harness's alone, nanoql's extremum a double the constructor takes.
The arguments are tools/cxx/src/instruments/lookbackoption.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/LookbackOptionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for kind in ("floating", "fixed"):
            method = f"Continuous{kind.capitalize()}LookbackOption::arguments::validate"
            for minmax in (0.0, 100.0, -0.5, -1.0):
                out.append({"name": f"the {kind} option, the running extremum at {minmax:g}",
                            "inputs": {"settings": st, "option": kind, "minmax": minmax},
                            "expected": {"valid": v.attempt(lambda: H.call(method, minmax))}})
        for tname, t, lambdas in (("call", CALL, (1.0, 1.5, 0.8)), ("put", PUT, (1.0, 0.6, 1.2))):
            for lam in lambdas:
                for days in (180, 365, 366):
                    out.append({"name": f"the partial floating {tname}, lambda {lam:g}, the lookback to day {days}",
                                "inputs": {"settings": st, "option": "partial floating", "optionType": t,
                                           "lambda": lam, "lookbackPeriodEnd": R + days},
                                "expected": {"valid": v.attempt(lambda: H.call(
                                    "ContinuousPartialFloatingLookbackOption::arguments::validate", t, lam,
                                    R + days))}})
        for days in (0, 180, 365, 366):
            out.append({"name": f"the partial fixed option, the lookback from day {days}",
                        "inputs": {"settings": st, "option": "partial fixed", "lookbackPeriodStart": R + days},
                        "expected": {"valid": v.attempt(lambda: H.call(
                            "ContinuousPartialFixedLookbackOption::arguments::validate", R + days))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/lookbackoption.hpp", cases)
