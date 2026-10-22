# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DailyTenorEURLibor and EURLiborON (ql/indexes/ibor/eurlibor.hpp), each output computed by the
C++ harness: the wheel binds neither. DailyTenorEURLibor at 0 and 2 settlement days, and EURLiborON, each one case as
tools/vectors/gen/indexes/ibor/euribor.py's index_forecast_case writes it, with its DATES, TODAY and forecast_curves:
the name, conventions and fixing calendar, the fixing, value and maturity dates, and the forecasts on its two curves.

Each case's inputs hold the preset ("index": {"type", "settlementDays" for DailyTenorEURLibor}), the dates and the
curves. preset_case is shared by tools/vectors/gen/cxx/indexes/ibor/chflibor.py, which imports this file.
tests/NanoQL.Tests/Vectors/Cxx/Indexes/Ibor/EURLiborCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_euribor",
                                               os.path.join(v.GEN_ROOT, "indexes", "ibor", "euribor.py"))
eur = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eur)


def preset_case(H, spec, method, *args):
    """One preset's case through the harness's method, which takes args, then the dates and the curves; named by the
    constructor and the index's name, which two presets can share (DailyTenorEURLibor at 0 days is EURLiborON)."""
    dates = v.enc(eur.DATES)
    curves = eur.forecast_curves()
    with H.case_settings(v.enc(eur.TODAY)) as st:
        expected = H.call(method, *args, dates, curves)
    constructor = f"{method}({', '.join(str(a) for a in args)})"
    return {"name": f"{constructor}: {expected['name']}",
            "inputs": {"settings": st, "index": spec, "dates": dates, "curves": curves}, "expected": expected}


def cases():
    with cxx.connect() as H:
        out = [preset_case(H, {"type": "DailyTenorEURLibor", "settlementDays": n}, "DailyTenorEURLibor", n)
               for n in (0, 2)]
        out.append(preset_case(H, {"type": "EURLiborON"}, "EURLiborON"))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/eurlibor.hpp", cases)
