# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EUR LIBOR swap indexes (ql/indexes/swap/eurliborswap.hpp): EurLiborSwapIsdaFixA,
EurLiborSwapIsdaFixB and EurLiborSwapIfrFix, each at tools/vectors/gen/indexes/swap/euriborswap.py's 8 tenors and
alternating forms, through its preset_cases: EUR LIBOR 3M up to a year and 6M beyond, and 365 and 366 days refused, a
tenor QuantLib can't compare with a year; EurLiborSwapIfrFix 10Y on QuantLib's empty discounting handle. 24 cases on the
evaluation date 15 January 2025, with euriborswap.py's outputs and inputs. The wheel's classes are QuantLib-SWIG's
proxies of the C++ classes of those names, their constructors QuantLib's. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Swap/EurLiborSwapVectorTests.cs."""
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

PRESETS = {"EurLiborSwapIsdaFixA": ql.EurLiborSwapIsdaFixA, "EurLiborSwapIsdaFixB": ql.EurLiborSwapIsdaFixB,
           "EurLiborSwapIfrFix": ql.EurLiborSwapIfrFix}


def cases():
    out = esw.preset_cases(PRESETS, ("EurLiborSwapIfrFix", 5))
    by_name = {c["name"]: c["expected"] for c in out}
    # 365 and 366 days can't be compared with a year.
    assert by_name["EurLiborSwapIsdaFixA 365D (forwarding)"] == {"built": v.THROWS}
    assert by_name["EurLiborSwapIfrFix 366D (forwarding, discounting)"] == {"built": v.THROWS}
    # EUR LIBOR 3M up to a year, 6M beyond it.
    assert by_name["EurLiborSwapIsdaFixB 12M (forwarding)"]["iborIndex"] == "EURLibor3M Actual/360"
    assert by_name["EurLiborSwapIsdaFixB 13M (forwarding, discounting)"]["iborIndex"] == "EURLibor6M Actual/360"
    # Exogenous discounting on QuantLib's empty handle refuses every forecast.
    assert by_name["EurLiborSwapIfrFix 10Y (forwarding, discounting)"]["fixing"][0] == v.THROWS
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/swap/eurliborswap.hpp", cases)
