# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CallSpecifiedMultiProduct (ql/models/marketmodels/products/multistep/
callspecifiedmultiproduct.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the
callable product stepped on LMM curve states the harness sets, read per step, with its counts and times; and its
values through the accounting engine over a few paths (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; a receiver swap callable at every rate time but the last on
a swap-rate trigger, into an exercise adapter of nothing, into QuantLib's default rebate (a cash rebate of 0) and into
an adapter of a Bermudan swaption's exercise value; triggers at the forwards' mean and rising.

Left out: an underlying with no cash-flow times (QuantLib's MultiStepNothing), which nanoql doesn't end while an
exercise time remains, and exercise, strategy or rebate times off the underlying's, where QuantLib writes no counts at
an idle step (the S-mmp plan's MMPR4 (vii), option (1), and (xvi); tests/NanoQL.Tests/Divergences/
CallSpecifiedEndTests.cs, CallSpecifiedIdleStepTests.cs).

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CallSpecifiedMultiProductCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "callability"))

import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402


def callable_spec(times, triggers, rebate):
    exercises = times[:-1]
    return {"type": "CallSpecifiedMultiProduct", "underlying": cn.swap(times, False),
            "strategy": cn.trigger(times, triggers, exercises), "rebate": rebate}


def adapter(exercise):
    return {"type": "ExerciseAdapter", "exercise": exercise, "numberOfProducts": 1}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            n = len(times) - 1
            k = cn.mid(times)
            for tlabel, triggers in (("at the forwards' mean", [k] * n),
                                     ("rising", [k - 0.004 + 0.002 * i for i in range(n)])):
                for rlabel, rebate in (("into nothing", adapter(cn.nothing(times))), ("into QuantLib's default", None),
                                       ("into a Bermudan payer", adapter(cn.bermudan(times, 1, k)))):
                    out += ms.steps_and_engine(H, st, rnd, f"{label}: triggers {tlabel}, {rlabel}",
                                               callable_spec(times, triggers, rebate), times,
                                               engine=rebate is None or tlabel.startswith("at"))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/callspecifiedmultiproduct.hpp", cases)
