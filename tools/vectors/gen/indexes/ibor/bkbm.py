# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Bkbm presets (ql/indexes/ibor/bkbm.hpp), New Zealand's bank bill benchmark rates on the
Wellington calendar: Bkbm1M to Bkbm6M, and Bkbm at 2W, one case each, as tools/vectors/gen/indexes/ibor/euribor.py
writes them, with its DATES and index_case; and Bkbm at a daily tenor, refused ("for daily tenors (1D) dedicated
DailyTenor constructor must be used"), its name's {"throws": true}. The wheel's presets are QuantLib-SWIG's proxies of
the C++ classes, their constructors QuantLib's with the default empty forwarding handle. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Ibor/BkbmVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_euribor",
                                               os.path.join(v.GEN_ROOT, "indexes", "ibor", "euribor.py"))
eur = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eur)


def cases():
    out = [eur.index_case({"type": cls}, getattr(ql, cls)) for cls in
           ["Bkbm1M", "Bkbm2M", "Bkbm3M", "Bkbm4M", "Bkbm5M", "Bkbm6M"]]
    out.append(eur.index_case({"type": "Bkbm", "tenor": eur.tenor_spec((2, ql.Weeks))},
                              lambda: ql.Bkbm(ql.Period(2, ql.Weeks))))
    out.append(eur.refused_case("Bkbm at a daily tenor", {"type": "Bkbm", "tenor": eur.tenor_spec((1, ql.Days))},
                                lambda: ql.Bkbm(ql.Period(1, ql.Days))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/bkbm.hpp", cases)
