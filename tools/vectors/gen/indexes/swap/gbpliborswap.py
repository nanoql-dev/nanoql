# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GBP LIBOR swap index (ql/indexes/swap/gbpliborswap.hpp): GbpLiborSwapIsdaFix, at
tools/vectors/gen/indexes/swap/euriborswap.py's 8 tenors and alternating forms, through its preset_cases: GBP LIBOR 3M
on an annual fixed leg up to a year and 6M on a semiannual one beyond, fixed on the day, and 365 and 366 days refused, a
tenor QuantLib can't compare with a year; GbpLiborSwapIsdaFix 10Y on QuantLib's empty discounting handle. 8 cases on the
evaluation date 15 January 2025, with euriborswap.py's outputs and inputs. The wheel's classes are QuantLib-SWIG's
proxies of the C++ classes of those names, their constructors QuantLib's. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Swap/GbpLiborSwapVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_euriborswap",
                                               os.path.join(v.GEN_ROOT, "indexes", "swap", "euriborswap.py"))
esw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(esw)

PRESETS = {"GbpLiborSwapIsdaFix": ql.GbpLiborSwapIsdaFix}


def cases():
    out = esw.preset_cases(PRESETS, ("GbpLiborSwapIsdaFix", 5))
    by_name = {c["name"]: c["expected"] for c in out}
    # 365 and 366 days can't be compared with a year.
    assert by_name["GbpLiborSwapIsdaFix 365D (forwarding)"] == {"built": v.THROWS}
    assert by_name["GbpLiborSwapIsdaFix 366D (forwarding, discounting)"] == {"built": v.THROWS}
    # GBP LIBOR 3M on an annual fixed leg up to a year, 6M on a semiannual one beyond it.
    assert by_name["GbpLiborSwapIsdaFix 12M (forwarding)"]["iborIndex"] == "GBPLibor3M Actual/365 (Fixed)"
    assert by_name["GbpLiborSwapIsdaFix 12M (forwarding)"]["fixedLegTenor"] == [1, int(ql.Years)]
    assert by_name["GbpLiborSwapIsdaFix 13M (forwarding, discounting)"]["iborIndex"] == "GBPLibor6M Actual/365 (Fixed)"
    assert by_name["GbpLiborSwapIsdaFix 13M (forwarding, discounting)"]["fixedLegTenor"] == [6, int(ql.Months)]
    # Fixed on the day.
    assert by_name["GbpLiborSwapIsdaFix 5Y (forwarding)"]["fixingDays"] == 0
    # Exogenous discounting on QuantLib's empty handle refuses every forecast.
    assert by_name["GbpLiborSwapIsdaFix 10Y (forwarding, discounting)"]["fixing"][0] == v.THROWS
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/swap/gbpliborswap.hpp", cases)
