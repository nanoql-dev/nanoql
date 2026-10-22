# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DailyTenorCHFLibor (ql/indexes/ibor/chflibor.hpp), each output computed by the C++ harness:
the wheel doesn't bind it. DailyTenorCHFLibor at 0, 1 and 2 settlement days, each one case through
tools/vectors/gen/cxx/indexes/ibor/eurlibor.py's preset_case, as tools/vectors/gen/indexes/ibor/euribor.py's
index_forecast_case writes it: the name, conventions and fixing calendar, the fixing, value and maturity dates, and the
forecasts on its two curves.

Each case's inputs hold the preset ("index": {"type": "DailyTenorCHFLibor", "settlementDays"}), the dates and the
curves. tests/NanoQL.Tests/Vectors/Cxx/Indexes/Ibor/CHFLiborCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("cxx_eurlibor",
                                               os.path.join(v.GEN_ROOT, "cxx", "indexes", "ibor", "eurlibor.py"))
eurlibor = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eurlibor)


def cases():
    with cxx.connect() as H:
        return [eurlibor.preset_case(H, {"type": "DailyTenorCHFLibor", "settlementDays": n}, "DailyTenorCHFLibor", n)
                for n in (0, 1, 2)]


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/chflibor.hpp", cases)
