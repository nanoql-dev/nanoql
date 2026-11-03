# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's KahaleSmileSection (ql/termstructures/volatility/kahalesmilesection.hpp,
.cpp) that the wheel doesn't bind, its core's indices and strikes, and for its prices and volatilities on the harness's
own sources, each output computed by the C++ harness on sections built inside the call, each case on a harness of its
own. The sources are the unshifted shifted-lognormal harness sections of
tools/vectors/gen/cxx/termstructures/volatility/smilesection.py with an at-the-money level, and an unshifted SABR
section; on each, three settings (plain; exponential extrapolation; the deletion of arbitrage points), with the default
moneyness grid and a grid of nine points: the core's indices, its first and last strikes, the level, and at nine
strikes the volatility and the call and put prices.

Left out, where nanoql's section differs (and in its at-the-money level and segment fits): shifted sources, on which
QuantLib's put-branch volatility adds the unshifted strike, its left wing's check reads a digital in the core, and its
level is the shifted forward (the fixed and QuantLib's expressions agree at a shift of 0); and interpolated fits, which
on these smiles run to forwards of 1e14 and more, whose Black calls and constants cancel, and which nanoql solves again
without the cancellation (and on one of which QuantLib's harness exits, its erase past its functions' end); the wheel's
file keeps the interpolated fits of QuantLib's own test smiles. A case on which the harness exits is left out, its name
printed. A refused value is {"throws": true}. Each case's input "kahale" is the spec
tools/cxx/src/termstructures/volatility/kahalesmilesection.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/KahaleSmileSectionCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "cxx_smilesection", os.path.join(v.GEN_ROOT, "cxx", "termstructures", "volatility", "smilesection.py"))
hss = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hss)

SL, CALL, PUT = hss.SL, hss.CALL, hss.PUT
E1 = hss.E1
SABR = {"type": "SabrSmileSection", "form": "time", "exerciseTime": 5.0, "forward": 0.03,
        "sabrParameters": [0.03, 0.6, 0.4, -0.3], "dayCounter": None, "volatilityType": SL, "shift": 0.0}
# (interpolate, exponentialExtrapolation, deleteArbitragePoints)
SETTINGS = [(False, False, False), (False, True, False), (False, False, True)]
GRID = [0.0, 0.05, 0.2, 0.5, 0.8, 1.0, 1.25, 1.75, 3.0]


def unshifted(spec):
    if spec["volatilityType"] != SL or spec["shift"] != 0.0:
        return False
    if spec["type"] == "HarnessSmileSection":
        return spec["atmLevel"] is not None and not spec.get("overriddenShift") and spec.get("overriddenType") is None
    return True


def one_case(H, n, evaluation_date, source, interpolate, exponential, delete, grid):
    with H.case_settings(v.enc(evaluation_date)) as st:
        atm = source.get("atmLevel", source.get("forward"))
        ks = [atm * m for m in (0.1, 0.3, 0.6, 0.9, 1.0, 1.1, 1.5, 2.5, 6.0)]
        spec = {"source": source, "atm": None, "interpolate": interpolate, "exponentialExtrapolation": exponential,
                "deleteArbitragePoints": delete, "moneynessGrid": grid, "gap": 1.0e-5, "forcedLeftIndex": -1,
                "forcedRightIndex": 2147483647}

        def call(member, *args):
            return v.attempt(lambda: H.call("KahaleSmileSection::" + member, spec, *args))

        expected = {m: call(m) for m in ("coreIndices", "leftCoreStrike", "rightCoreStrike", "atmLevel")}
        expected["volatility"] = [call("volatility", k) for k in ks]
        expected["call"] = [call("optionPrice", k, CALL, 1.0) for k in ks]
        expected["put"] = [call("optionPrice", k, PUT, 1.0) for k in ks]
        label = "exponential" if exponential else "deleting" if delete else "plain"
        return {"name": f"source {n}, {label}, {'default grid' if not grid else 'nine points'}",
                "inputs": {"settings": st, "kahale": spec, "strikes": ks}, "expected": expected}


def cases():
    """Each case on a harness of its own: where QuantLib's fit reads or erases past its functions' end the harness may
    exit, and the case is left out, its name printed."""
    out = []
    sources = [(e, s) for e, s, *_ in hss.SECTIONS if unshifted(s) and s.get("exerciseTime", 1.0) > 0.0]
    sources.append((E1, SABR))
    for n, (evaluation_date, source) in enumerate(sources):
        for interpolate, exponential, delete in SETTINGS:
            for grid in ([], GRID):
                H = cxx.connect()
                try:
                    out.append(one_case(H, n, evaluation_date, source, interpolate, exponential, delete, grid))
                except (cxx.HarnessFault, OSError):
                    print(f"left out: source {n}, interpolate {interpolate}, exponential {exponential}, "
                          f"delete {delete}, {'default grid' if not grid else 'nine points'}: the harness exited")
                finally:
                    try:
                        H.close()
                    except OSError:
                        pass
    return out

if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/kahalesmilesection.hpp", cases)
