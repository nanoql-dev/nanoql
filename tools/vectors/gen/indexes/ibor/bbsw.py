# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Bbsw, the Australian bank bill swap rate (ql/indexes/ibor/bbsw.hpp): Bbsw1M to Bbsw6M and Bbsw
at 2W, one case each, as tools/vectors/gen/indexes/ibor/euribor.py writes them, with its DATES and index_forecast_case:
the name, conventions and fixing calendar, the fixing, value and maturity dates, and the forecasts on its two curves.
Bbsw at a daily tenor is refused, its name's {"throws": true}. The wheel's classes are QuantLib-SWIG's proxies of the
C++ classes of those names, their constructors QuantLib's. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Ibor/BbswVectorTests.cs."""
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
    return [
        eur.index_forecast_case({"type": "Bbsw1M"}, lambda h: ql.Bbsw1M(h)),
        eur.index_forecast_case({"type": "Bbsw2M"}, lambda h: ql.Bbsw2M(h)),
        eur.index_forecast_case({"type": "Bbsw3M"}, lambda h: ql.Bbsw3M(h)),
        eur.index_forecast_case({"type": "Bbsw4M"}, lambda h: ql.Bbsw4M(h)),
        eur.index_forecast_case({"type": "Bbsw5M"}, lambda h: ql.Bbsw5M(h)),
        eur.index_forecast_case({"type": "Bbsw6M"}, lambda h: ql.Bbsw6M(h)),
        eur.index_forecast_case({"type": "Bbsw", "tenor": eur.tenor_spec((2, ql.Weeks))},
                                lambda h: ql.Bbsw(ql.Period(2, ql.Weeks), h)),
        eur.refused_case("Bbsw at a daily tenor", {"type": "Bbsw", "tenor": eur.tenor_spec((1, ql.Days))},
                         lambda: ql.Bbsw(ql.Period(1, ql.Days))),
    ]


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/bbsw.hpp", cases)
