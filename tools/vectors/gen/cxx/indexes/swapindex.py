# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SwapIndex::exogenousDiscount() and SwapIndex::underlyingSwap(const Date&) (ql/indexes/swapindex.hpp),
each output computed by the C++ harness: the wheel binds neither, but for an OvernightIndexedSwapIndex's own
underlyingSwap, which hides the base's. 40 cases, the indexes of tools/vectors/gen/indexes/swapindex.py on its
evaluation date, 15 January 2025, with the at-par switch on: whether the index's discounting is exogenous (a swap
index built with a discounting curve, even an empty one), and the base's vanilla swap at 4 fixing dates (16 and 31
January and 30 June 2025, and Saturday 18 January, which isn't a valid fixing date, so QuantLib refuses the swap): its
start and maturity dates, its fixed rate, its fixed schedule's dates, its floating schedule's size and first and last 5
dates (an overnight index's schedule is daily), and each leg's number of cash flows. For an overnight-indexed swap
index the swap is the base's, a vanilla swap of daily coupons on the overnight index, which its forecasts use.

Each case's inputs hold its settings, the index ("index", a spec of that generator) and the fixing dates ("dates"). A
refused swap is {"throws": true} in each of its outputs. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Indexes/SwapIndexCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_swapindex", os.path.join(v.GEN_ROOT, "indexes", "swapindex.py"))
swx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(swx)

TODAY = swx.TODAY
DATES = [ql.Date(16, 1, 2025), ql.Date(31, 1, 2025), ql.Date(30, 6, 2025), ql.Date(18, 1, 2025)]
OUTPUTS = ["startDate", "maturityDate", "fixedRate", "fixedSchedule", "floatingScheduleSize", "floatingScheduleFront",
           "floatingScheduleBack", "fixedLegSize", "floatingLegSize"]


def swap_outputs(H, spec, d):
    """The swap's outputs by name, or a throw in each where QuantLib refuses the swap."""
    try:
        s = H.call("SwapIndex::underlyingSwap", spec, d)
    except RuntimeError:
        return {k: dict(v.THROWS) for k in OUTPUTS}
    floating = s["floatingSchedule"]
    return {"startDate": s["startDate"], "maturityDate": s["maturityDate"], "fixedRate": s["fixedRate"],
            "fixedSchedule": s["fixedSchedule"], "floatingScheduleSize": floating["size"],
            "floatingScheduleFront": floating["front"], "floatingScheduleBack": floating["back"],
            "fixedLegSize": s["fixedLegSize"], "floatingLegSize": s["floatingLegSize"]}


def cases():
    with cxx.connect() as H:
        out = []
        dates = v.enc(DATES)
        for k, (name, spec) in enumerate(swx.indexes()):
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=True) as st:
                swaps = [swap_outputs(H, spec, d) for d in dates]
                expected = {"exogenousDiscount": H.call("SwapIndex::exogenousDiscount", spec)}
                expected.update({key: [s[key] for s in swaps] for key in OUTPUTS})
                out.append({"name": f"{name} / {k}", "inputs": {"settings": st, "index": spec, "dates": dates},
                            "expected": expected})
        check_rows(out)
        return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert by_name["5Y exogenous on an empty handle / 10"]["exogenousDiscount"] is True
    assert by_name["5Y on Euribor 6M / 2"]["exogenousDiscount"] is False
    assert by_name["5Y on Euribor 6M / 2"]["startDate"][3] == v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/swapindex.hpp", cases)
