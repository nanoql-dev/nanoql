# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepInverseFloater (ql/models/marketmodels/products/multistep/
multistepinversefloater.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the
inverse floater stepped on LMM curve states the harness sets, read per step, with the product's counts and times; and
its values through the accounting engine over a few paths (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; payer and receiver on QuantLib's testInverseFloater terms
(strike 15%, multiplier 2, no spread); a strike low enough to floor coupons at zero; spreads and accruals off the
rate taus; payment times off the rate times.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): accruals, strikes, multipliers, spreads or payment times not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepInverseFloaterCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402


def spec(kind, times, strike, multiplier, spread, payer, fixed_accruals=None, payment_times=None):
    n = len(times) - 1
    return {"type": kind, "rateTimes": times, "fixedAccruals": fixed_accruals or mf.accruals_of(times),
            "floatingAccruals": mf.accruals_of(times), "fixedStrikes": [strike] * n,
            "fixedMultipliers": [multiplier] * n, "floatingSpreads": [spread] * n,
            "paymentTimes": payment_times or times[1:], "payer": payer}


def floater_cases(H, st, rnd, kind, engine):
    """The floater's cases, shared with the pathwise floater's generator, which steps them its own way."""
    out = []
    for label, times in ms.GRIDS:
        for payer in (True, False):
            side = "payer" if payer else "receiver"
            out += engine(H, st, rnd, f"{label}: {side}, QuantLib's test's terms",
                          spec(kind, times, 0.15, 2.0, 0.0, payer), times, True)
        out += engine(H, st, rnd, f"{label}: coupons floored at zero",
                      spec(kind, times, 0.05, 2.0, 0.0, True), times, False)
        fixed = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
        pays = [t - 0.1 for t in times[1:]]
        out += engine(H, st, rnd, f"{label}: a spread, own accruals and payment times",
                      spec(kind, times, 0.15, 2.0, 0.002, False, fixed, pays), times, False)
    return out


def cases():
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        return floater_cases(H, st, rnd, "MultiStepInverseFloater",
                             lambda H, st, rnd, label, s, times, e: ms.steps_and_engine(H, st, rnd, label, s, times, e))


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepinversefloater.hpp", cases)
