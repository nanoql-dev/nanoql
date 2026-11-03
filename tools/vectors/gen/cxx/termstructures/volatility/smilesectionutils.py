# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SmileSectionUtils (ql/termstructures/volatility/smilesectionutils.hpp, .cpp), which the
wheel doesn't bind, each output computed by the C++ harness on utilities built inside the call. The sections are
tools/vectors/gen/cxx/termstructures/volatility/smilesection.py's harness sections (date and time forms, shifted
lognormal and normal, smiles with and without arbitrage), an unshifted section with a limited strike range, and two
SABR sections, unshifted and shifted; on each, the default moneyness grid and a grid of eight points, with the
section's level and with one given, and without and with the deletion of arbitrage points: the money and strike
grids, the call prices, the level, and the arbitrage-free region's strikes and indices. Then the constructor's
refusals: a negative moneyness for a shifted lognormal section, a grid not increasing, no level, and the at-the-money
point at the grid's end.

Left out, where nanoql computes a limited range's endpoints on the shifted level: a shifted lognormal section with a
limited strike range, whose endpoints' moneyness QuantLib divides by the unshifted level. A refused value is {"throws":
true}. Each case's input "utilities" is the spec tools/cxx/src/termstructures/volatility/smilesectionutils.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SmileSectionUtilsCxxVectorTests.cs reads it back."""
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

SL, N = hss.SL, hss.N
E1 = hss.E1
MAX = hss.MAX
MEMBERS = ["moneyGrid", "strikeGrid", "callPrices", "atmLevel", "arbitragefreeRegion", "arbitragefreeIndices"]
SABR = [
    {"type": "SabrSmileSection", "form": "time", "exerciseTime": 5.0, "forward": 0.03,
     "sabrParameters": [0.03, 0.6, 0.4, -0.3], "dayCounter": None, "volatilityType": SL, "shift": 0.0},
    {"type": "SabrSmileSection", "form": "time", "exerciseTime": 2.0, "forward": 0.005,
     "sabrParameters": [0.02, 0.5, 0.3, -0.2], "dayCounter": None, "volatilityType": SL, "shift": 0.02},
]
LIMITED = hss.harness_spec("time", 2.0, hss.A365, [0.25, -1.0, 8.0], 0.03, SL, 0.0, strikes=(0.006, 0.09))
GRIDS = {SL: [0.0, 0.1, 0.4, 0.8, 1.0, 1.3, 2.0, 4.0], N: [-0.03, -0.02, -0.01, -0.002, 0.0, 0.002, 0.01, 0.03]}


def limited_shifted(spec):
    """Whether the section is shifted lognormal with a limited strike range."""
    if spec["type"] != "HarnessSmileSection" or spec["volatilityType"] != SL:
        return False
    shift = spec["overriddenShift"] if spec.get("overriddenShift") is not None else spec["shift"]
    return shift != 0.0 and (spec["minStrike"] > -MAX or spec["maxStrike"] < MAX)


def case(H, st, name, spec):
    def call(member):
        return v.attempt(lambda: H.call("SmileSectionUtils::" + member, spec))

    return {"name": name, "inputs": {"settings": st, "utilities": spec}, "expected": {m: call(m) for m in MEMBERS}}


def cases():
    with cxx.connect() as H:
        out = []
        sources = [(e, s) for e, s, *_ in hss.SECTIONS if not limited_shifted(s)]
        sources += [(E1, LIMITED)] + [(E1, s) for s in SABR]
        for n, (evaluation_date, section) in enumerate(sources):
            with H.case_settings(v.enc(evaluation_date)) as st:
                kind = section["volatilityType"]
                for grid in ([], GRIDS[kind]):
                    for atm in (None, 0.025):
                        for delete in (False, True):
                            spec = {"section": section, "moneynessGrid": grid, "atm": atm,
                                    "deleteArbitragePoints": delete}
                            out.append(case(H, st, f"section {n}, {'default grid' if not grid else 'eight points'}, "
                                                   f"{'its level' if atm is None else 'a level'}"
                                                   f"{', deleting' if delete else ''}", spec))
        with H.case_settings(v.enc(E1)) as st:
            base = hss.harness_spec("time", 1.0, hss.A365, [0.2, 0.0, 0.0], 0.03)
            refusals = [
                ("a negative moneyness", {"section": base, "moneynessGrid": [-0.1, 0.5, 1.0, 2.0], "atm": None,
                                          "deleteArbitragePoints": False}),
                ("a grid not increasing", {"section": base, "moneynessGrid": [0.5, 1.0, 1.0, 2.0], "atm": None,
                                           "deleteArbitragePoints": False}),
                ("no level", {"section": hss.harness_spec("time", 1.0, hss.A365, [0.2, 0.0, 0.0], None),
                              "moneynessGrid": [], "atm": None, "deleteArbitragePoints": False}),
                ("the at-the-money point at the grid's end", {"section": base, "moneynessGrid": [0.5, 1.0],
                                                              "atm": None, "deleteArbitragePoints": False}),
            ]
            for name, spec in refusals:
                out.append(case(H, st, "refused: " + name, spec))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/smilesectionutils.hpp", cases)
