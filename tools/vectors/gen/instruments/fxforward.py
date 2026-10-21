# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FxForward (ql/instruments/fxforward.hpp, .cpp): its constructors, inspectors, settlement date
and expiry.

Forwards built from two nominals and from a nominal and a rate, paying either currency, with 0, 1, 2 and 3 settlement
days on no calendar (QuantLib's empty calendar, NullCalendar), TARGET and the United States' settlement calendar: their
nominals, currencies, maturity, side, contracted rate, settlement days and calendar's name, and their settlement date
and isExpired under evaluation dates on a Friday, a Saturday, before a holiday, and before, on and after the maturity.
The constructors' refusals: a nominal or rate of zero or below, the same currency twice, an empty currency.

A case holds {"forward": {"kind" ("nominals" or "rate"), "sourceNominal", "sourceCurrency", "targetNominal" or
"forwardRate", "targetCurrency" (codes, or null for the empty currency), "maturityDate", "paySourceCurrency",
"settlementDays", "calendar" (null, "TARGET" or "UnitedStates")}, "evaluationDates"} and its settings; "settlementDates"
and "isExpired" are arrays, one element per evaluation date. The wheel's calls, by QuantLib-SWIG 1.44's fxforward.i:
FxForward's two constructors, the calendar left to its default, Calendar(), for none, and its inspectors, settlementDate() and isExpired()
under each evaluation date. Read back by tests/NanoQL.Tests/Vectors/Instruments/FxForwardVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)  # a Thursday
CURRENCIES = {"EUR": ql.EURCurrency, "USD": ql.USDCurrency, "SGD": ql.SGDCurrency, "JPY": ql.JPYCurrency}
CALENDARS = {"TARGET": ql.TARGET, "UnitedStates": lambda: ql.UnitedStates(ql.UnitedStates.Settlement)}


def currency(code):
    return ql.Currency() if code is None else CURRENCIES[code]()


def build(spec):
    # no calendar: the constructor's default, QuantLib's empty Calendar(), which the wheel can't build
    calendar = [] if spec["calendar"] is None else [CALENDARS[spec["calendar"]]()]
    maturity = ql.Date(spec["maturityDate"])
    if spec["kind"] == "nominals":
        return ql.FxForward(spec["sourceNominal"], currency(spec["sourceCurrency"]), spec["targetNominal"],
                            currency(spec["targetCurrency"]), maturity, spec["paySourceCurrency"],
                            spec["settlementDays"], *calendar)
    return ql.FxForward(spec["sourceNominal"], currency(spec["sourceCurrency"]), currency(spec["targetCurrency"]),
                        spec["forwardRate"], maturity, spec["paySourceCurrency"], spec["settlementDays"], *calendar)


def case(name, spec, evaluations):
    with v.case_settings(R) as s:
        try:
            f = build(spec)
        except RuntimeError:
            f = None
        expected = {}
        for key, get in [("sourceNominal", lambda: f.sourceNominal()), ("targetNominal", lambda: f.targetNominal()),
                         ("sourceCurrency", lambda: f.sourceCurrency().code()),
                         ("targetCurrency", lambda: f.targetCurrency().code()),
                         ("maturityDate", lambda: f.maturityDate()), ("paySourceCurrency", lambda: f.paySourceCurrency()),
                         ("forwardRate", lambda: f.forwardRate()), ("settlementDays", lambda: f.settlementDays()),
                         ("calendar", lambda: f.settlementCalendar().name())]:
            expected[key] = dict(v.THROWS) if f is None else v.enc(get())
        settlement, expired = [], []
        for d in evaluations:
            ql.Settings.instance().evaluationDate = d
            settlement.append(dict(v.THROWS) if f is None else v.enc(f.settlementDate()))
            expired.append(dict(v.THROWS) if f is None else f.isExpired())
        ql.Settings.instance().evaluationDate = R
        expected["settlementDates"] = settlement
        expected["isExpired"] = expired
        return {"name": name, "inputs": {"forward": spec, "evaluationDates": [v.enc(d) for d in evaluations],
                                         "settings": s},
                "expected": expected}


def cases():
    out = []
    maturity = R + ql.Period(6, ql.Months)
    evaluations = [R, ql.Date(16, 10, 2026), ql.Date(17, 10, 2026), ql.Date(24, 12, 2026), ql.Date(31, 12, 2026),
                   maturity - 1, maturity, maturity + 1]
    for kind in ("nominals", "rate"):
        for pay in (True, False):
            for days in (0, 1, 2, 3):
                for cal in (None, "TARGET", "UnitedStates"):
                    spec = {"kind": kind, "sourceNominal": 1000000.0, "sourceCurrency": "USD",
                            "targetCurrency": "SGD", "maturityDate": maturity.serialNumber(),
                            "paySourceCurrency": pay, "settlementDays": days, "calendar": cal}
                    if kind == "nominals":
                        spec["targetNominal"] = 1350000.0
                    else:
                        spec["forwardRate"] = 1.36
                    out.append(case(f"{kind}, pay {'source' if pay else 'target'}, {days} days, {cal or 'none'}",
                                    spec, evaluations))
    refusals = [("nominals", -1.0, 1350000.0, "USD", "SGD"), ("nominals", 0.0, 1350000.0, "USD", "SGD"),
                ("nominals", 1000000.0, 0.0, "USD", "SGD"), ("nominals", 1000000.0, -5.0, "USD", "SGD"),
                ("nominals", 1000000.0, 1350000.0, "USD", "USD"), ("nominals", 1000000.0, 1350000.0, None, "SGD"),
                ("nominals", 1000000.0, 1350000.0, "USD", None), ("rate", 1000000.0, 0.0, "USD", "SGD"),
                ("rate", 1000000.0, -1.36, "USD", "SGD"), ("rate", -1.0, 1.36, "USD", "SGD"),
                ("rate", 1000000.0, 1.36, "EUR", "EUR"), ("rate", 1000000.0, 1.36, None, "EUR")]
    for kind, n, second, src, tgt in refusals:
        spec = {"kind": kind, "sourceNominal": n, "sourceCurrency": src, "targetCurrency": tgt,
                "maturityDate": maturity.serialNumber(), "paySourceCurrency": True, "settlementDays": 2,
                "calendar": None}
        spec["targetNominal" if kind == "nominals" else "forwardRate"] = second
        out.append(case(f"refused: {kind} {n!r} {second!r} {src} {tgt}", spec, [R]))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/fxforward.hpp", cases)
