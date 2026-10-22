# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Euribor swap indexes (ql/indexes/swap/euriborswap.hpp): EuriborSwapIsdaFixA,
EuriborSwapIsdaFixB and EuriborSwapIfrFix, each at 6 tenors (6M, 1Y, 12M, 13M, 5Y and 10Y, Euribor 3M up to a year and
6M beyond), alternating QuantLib's two constructors: on a forwarding curve, and on forwarding and discounting curves,
which is exogenous discounting, once on QuantLib's empty discounting handle; and each at 365 and 366 days, a tenor
QuantLib can't compare with a year, which it refuses at construction. 24 cases on the evaluation date 15 January 2025.

A case's outputs are whether the index builds, QuantLib's refusal recorded as its throw; then its name, the family
name, the fixing days, the fixed leg's tenor and convention, the day counter's, the fixing calendar's, the currency's
and the Ibor index's names, whether the discounting curve is empty, and its fixings at 5 later dates, a Saturday
among them, which isn't a valid fixing date. A refused value is {"throws": true}.

Each case's inputs hold the preset's class ("preset"), its tenor ([length, units]), its form ("forwarding" or
"forwarding, discounting") and its curves ("forwarding", "discounting"), flat forward specs of
indexes/interestrateindex.py, null for QuantLib's empty handle. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Swap/EuriborSwapVectorTests.cs. The LIBOR swap presets' generators in this folder
write their cases through preset_cases."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

TODAY = irx.TODAY
PRESETS = {"EuriborSwapIsdaFixA": ql.EuriborSwapIsdaFixA, "EuriborSwapIsdaFixB": ql.EuriborSwapIsdaFixB,
           "EuriborSwapIfrFix": ql.EuriborSwapIfrFix}
TENORS = [(6, ql.Months), (1, ql.Years), (12, ql.Months), (13, ql.Months), (5, ql.Years), (10, ql.Years),
          (365, ql.Days), (366, ql.Days)]
# Later dates: Thursday 16 January, Saturday 18 January, Friday 31 January, Monday 30 June 2025 and Friday 30 January
# 2026.
DATES = [ql.Date(16, 1, 2025), ql.Date(18, 1, 2025), ql.Date(31, 1, 2025), ql.Date(30, 6, 2025), ql.Date(30, 1, 2026)]
FORWARDING = irx.curve_spec(TODAY, 0.029, irx.A365, ql.Continuous, ql.Annual)
DISCOUNTING = irx.curve_spec(TODAY, 0.024, irx.A360, ql.Simple, ql.Annual)


def specs(presets, empty_at):
    """Each preset at each tenor, alternating the two forms; the preset and tenor index empty_at on the empty
    discounting handle."""
    out = []
    for preset in presets:
        for k, tenor in enumerate(TENORS):
            form = "forwarding" if k % 2 == 0 else "forwarding, discounting"
            discounting = None if (form == "forwarding" or (preset, k) == empty_at) else DISCOUNTING
            out.append({"preset": preset, "tenor": [tenor[0], int(tenor[1])], "form": form,
                        "forwarding": FORWARDING, "discounting": discounting})
    return out


def make(spec, presets):
    cls = presets[spec["preset"]]
    tenor = ql.Period(*spec["tenor"])
    if spec["form"] == "forwarding":
        return cls(tenor, irx.handle(spec["forwarding"]))
    return cls(tenor, irx.handle(spec["forwarding"]), irx.handle(spec["discounting"]))


def empty(handle):
    try:
        handle.currentLink()
    except RuntimeError:
        return True
    return False


def preset_cases(presets, empty_at):
    """The cases of a family's presets ({name: class}), on specs' tenors and forms."""
    out = []
    for spec in specs(presets, empty_at):
        with v.case_settings(TODAY) as st:
            try:
                idx = make(spec, presets)
            except RuntimeError:
                expected = {"built": dict(v.THROWS)}
            else:
                expected = {
                    "built": True,
                    "name": idx.name(),
                    "familyName": idx.familyName(),
                    "fixingDays": idx.fixingDays(),
                    "fixedLegTenor": [idx.fixedLegTenor().length(), int(idx.fixedLegTenor().units())],
                    "fixedLegConvention": int(idx.fixedLegConvention()),
                    "dayCounter": idx.dayCounter().name(),
                    "fixingCalendar": idx.fixingCalendar().name(),
                    "currency": idx.currency().code(),
                    "iborIndex": idx.iborIndex().name(),
                    "discountingTermStructureEmpty": empty(idx.discountingTermStructure()),
                    "fixing": [v.attempt(lambda d=d: idx.fixing(d)) for d in DATES]}
            tenor = f"{spec['tenor'][0]}{'DWMY'[spec['tenor'][1]]}"
            out.append({"name": f"{spec['preset']} {tenor} ({spec['form']})",
                        "inputs": {"settings": st, **spec, "dates": v.enc(DATES)}, "expected": expected})
    return out


def cases():
    out = preset_cases(PRESETS, ("EuriborSwapIfrFix", 5))
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # 365 and 366 days can't be compared with a year.
    assert by_name["EuriborSwapIsdaFixA 365D (forwarding)"] == {"built": v.THROWS}
    assert by_name["EuriborSwapIsdaFixA 366D (forwarding, discounting)"] == {"built": v.THROWS}
    # Euribor 3M up to a year, 6M beyond it.
    assert by_name["EuriborSwapIsdaFixA 12M (forwarding)"]["iborIndex"] == "Euribor3M Actual/360"
    assert by_name["EuriborSwapIsdaFixA 13M (forwarding, discounting)"]["iborIndex"] == "Euribor6M Actual/360"
    # Exogenous discounting on QuantLib's empty handle refuses every forecast.
    assert by_name["EuriborSwapIfrFix 10Y (forwarding, discounting)"]["fixing"][0] == v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/swap/euriborswap.hpp", cases)
