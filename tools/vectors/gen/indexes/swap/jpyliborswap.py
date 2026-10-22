# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's JPY LIBOR swap indexes (ql/indexes/swap/jpyliborswap.hpp): JpyLiborSwapIsdaFixAm and
JpyLiborSwapIsdaFixPm, each at tools/vectors/gen/indexes/swap/euriborswap.py's 8 tenors and alternating forms, through
its preset_cases: JPY LIBOR 6M at every tenor, 365 and 366 days among them, which these constructors never compare with
a year; JpyLiborSwapIsdaFixPm 10Y on QuantLib's empty discounting handle. 16 cases on the evaluation date 15 January
2025, with euriborswap.py's outputs and inputs. The wheel's classes are QuantLib-SWIG's proxies of the C++ classes of
those names, their constructors QuantLib's. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Swap/JpyLiborSwapVectorTests.cs."""
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

PRESETS = {"JpyLiborSwapIsdaFixAm": ql.JpyLiborSwapIsdaFixAm, "JpyLiborSwapIsdaFixPm": ql.JpyLiborSwapIsdaFixPm}


def cases():
    out = esw.preset_cases(PRESETS, ("JpyLiborSwapIsdaFixPm", 5))
    by_name = {c["name"]: c["expected"] for c in out}
    # These constructors never compare the tenor with a year.
    assert by_name["JpyLiborSwapIsdaFixAm 365D (forwarding)"]["built"] is True
    assert by_name["JpyLiborSwapIsdaFixPm 366D (forwarding, discounting)"]["built"] is True
    # JPY LIBOR 6M at every tenor.
    assert by_name["JpyLiborSwapIsdaFixAm 6M (forwarding)"]["iborIndex"] == "JPYLibor6M Actual/360"
    assert by_name["JpyLiborSwapIsdaFixPm 5Y (forwarding)"]["iborIndex"] == "JPYLibor6M Actual/360"
    # Exogenous discounting on QuantLib's empty handle refuses every forecast.
    assert by_name["JpyLiborSwapIsdaFixPm 10Y (forwarding, discounting)"]["fixing"][0] == v.THROWS
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/swap/jpyliborswap.hpp", cases)
