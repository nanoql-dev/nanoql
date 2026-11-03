# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TreeVanillaSwapEngine (ql/pricingengines/swap/treeswapengine.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness, on the evaluation date 15 January 2025: the 28 distinct
swaps of tools/vectors/gen/pricingengines/swaption/treeswaptionengine.py's European swaptions (the swaptions' own exercises unused),
payers and receivers in, at and out of the money, with nominals of 1 to 10 million, starting from a month to ten years
after the evaluation date, and a swap that started three months before it, its current floating coupon's fixing
stored with the case's settings.

Each swap's outputs are its NPV, an amount compared at its nominal, on five engines: a HullWhite (a = 0.05,
sigma = 0.01, on the discount curve, a flat 2.7%, its fitted tree) with 30 and 100 time steps and on a TimeGrid through
the swap's times with 40 steps ("hullWhiteSteps", "hullWhiteGrid"), and a Vasicek (r0 = 0.03, a = 0.08, b = 0.035,
sigma = 0.009), which has no curve, with the discount curve given to the engine, with 50 steps and on the grid
("vasicekSteps", "vasicekGrid"). The grid's times are treeswaptionengine.py's for the swaption, a superset of the
swap's.

Generated and left out: swaps that start on the spot date, whose first floating coupon is fixed on the evaluation date
and starts after it: QuantLib's discretized swap knows a coupon by its start date, so it values that coupon as a forward
on the tree's bonds, ignoring its fixing, where nanoql's, behind this engine, knows it by its fixing date and pays its
amount (tests/NanoQL.Tests/Divergences/TreeKnownCouponTests.cs). Each case's inputs hold the swaption
(treeswaptionengine.py's spec), the discount curve, the models, the steps and the grid. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/TreeSwapEngineCxxVectorTests.cs."""
import importlib.util
import json
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


tse = _load("wheel_treeswaptionengine", "pricingengines/swaption/treeswaptionengine.py")
swp, fvf = tse.swp, tse.fvf

TODAY = tse.TODAY
DISCOUNT = tse.DISCOUNT
HULL_WHITE = {"type": "HullWhite", "curve": DISCOUNT, "a": 0.05, "sigma": 0.01}
VASICEK = {"type": "Vasicek", "r0": 0.03, "a": 0.08, "b": 0.035, "sigma": 0.009, "lambda": 0.0}
HULL_WHITE_STEPS = [30, 100]
VASICEK_STEPS = 50
GRID_STEPS = 40
SKIPPED = ("overnight-indexed", "expired", "American")


def rows():
    """(name, swaption spec, settings, kept): the swaptions' vanilla swaps, the started one among them, and spot-dated
    swaps generated and left out."""
    out = []
    seen = set()
    for name, spec, settings in tse.swaptions():
        if name.startswith("Bermudan") or name.startswith(SKIPPED):
            continue
        key = json.dumps(spec["swap"], sort_keys=True)
        if key in seen:
            continue
        seen.add(key)
        out.append((name, spec, settings, True))
    spot = swp.TARGET_CAL.advance(TODAY, 2, ql.Days)
    for swap_type in (swp.PAYER, swp.RECEIVER):
        swap = swp.vanilla(swap_type, 1e6, 0.03, spot, spot + ql.Period(5, ql.Years))
        out.append((f"left out: spot-dated / {swap_type}", swp.swaption_spec(swap, swp.european(spot)), {}, False))
    return out


def harness_settings(settings):
    """The wheel's case settings as the harness stores them: the fixings by the index's name."""
    if "fixings" not in settings:
        return settings
    fixings = {}
    for index, pairs in settings["fixings"]:
        fixings[index.name()] = [[v.enc(d), x] for d, x in pairs]
    return dict(settings, fixings=fixings)


def priced(H, spec, times):
    def npv(f):
        return fvf.ibc.scaled(lambda: f(), fvf.scale_of(spec["swap"]))

    return {"hullWhiteSteps": [npv(lambda n=n: H.call("TreeVanillaSwapEngine::calculate(Size)", spec, HULL_WHITE, n,
                                                       None)) for n in HULL_WHITE_STEPS],
            "hullWhiteGrid": npv(lambda: H.call("TreeVanillaSwapEngine::calculate(TimeGrid)", spec, HULL_WHITE, times,
                                                GRID_STEPS, None)),
            "vasicekSteps": npv(lambda: H.call("TreeVanillaSwapEngine::calculate(Size)", spec, VASICEK, VASICEK_STEPS,
                                               DISCOUNT)),
            "vasicekGrid": npv(lambda: H.call("TreeVanillaSwapEngine::calculate(TimeGrid)", spec, VASICEK, times,
                                              GRID_STEPS, DISCOUNT))}


def cases():
    out = []
    with cxx.connect() as H:
        for name, spec, settings, kept in rows():
            with H.case_settings(v.enc(TODAY), **harness_settings(settings)) as st:
                with v.case_settings(TODAY, **settings):
                    times = tse.grid_times(tse.make_swaption(spec))
                expected = priced(H, spec, times)
                if not kept:
                    continue
                out.append({"name": "swap of " + name,
                            "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT,
                                       "hullWhite": HULL_WHITE, "vasicek": VASICEK, "hullWhiteSteps": HULL_WHITE_STEPS,
                                       "vasicekSteps": VASICEK_STEPS, "gridTimes": times, "gridSteps": GRID_STEPS},
                            "expected": expected})
    assert len(out) == 28, len(out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swap/treeswapengine.hpp", cases)
