# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AtmSmileSection and AtmAdjustedSmileSection (ql/termstructures/volatility/
atmsmilesection.hpp, .cpp and atmadjustedsmilesection.hpp, .cpp), which the wheel doesn't bind, each output computed
by the C++ harness on sections built inside the call. The sources are tools/vectors/gen/cxx/termstructures/
volatility/smilesection.py's harness sections (date and time forms, shifted lognormal and normal, with and without an
ATM level) and two SABR sections, unshifted and shifted. On each, AtmSmileSection with no level of its own and with
one: the inspectors, and at nine strikes around the level the volatility, the variance, the call and put prices, the
digital call and put prices, the vega and the density. Then AtmAdjustedSmileSection with a level, without and with
recentering: the inspectors, the volatility and the variance.

Left out, where nanoql prices the adjusted section on its own level: its option and digital prices, vega and density,
which QuantLib's take from the source at the adjusted strike. A refused value is {"throws": true}. Each case's input
"section" is the spec tools/cxx/src/termstructures/volatility/atmsmilesection.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/AtmSmileSectionCxxVectorTests.cs reads it back."""
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

SL, N, CALL, PUT = hss.SL, hss.N, hss.CALL, hss.PUT
E1 = hss.E1
INSPECTORS = ["minStrike", "maxStrike", "atmLevel", "exerciseDate", "exerciseTime", "dayCounter", "referenceDate",
              "volatilityType", "shift"]

SABR = [
    (E1, {"type": "SabrSmileSection", "form": "time", "exerciseTime": 5.0, "forward": 0.03,
          "sabrParameters": [0.03, 0.6, 0.4, -0.3], "dayCounter": None, "volatilityType": SL, "shift": 0.0}),
    (E1, {"type": "SabrSmileSection", "form": "time", "exerciseTime": 2.0, "forward": 0.005,
          "sabrParameters": [0.02, 0.5, 0.3, -0.2], "dayCounter": None, "volatilityType": SL, "shift": 0.02}),
]


def strikes(level, normal, shift):
    """Nine strikes around a level, above minus the shift for a shifted lognormal section."""
    if normal:
        return [level + d for d in (-0.02, -0.01, -0.005, -0.001, 0.0, 0.001, 0.005, 0.01, 0.02)]
    return [(level + shift) * m - shift for m in (0.2, 0.5, 0.8, 0.95, 1.0, 1.05, 1.3, 2.0, 3.0)]


def cases():
    with cxx.connect() as H:
        out = []
        sources = [(e, spec) for e, spec, *_ in hss.SECTIONS] + SABR
        for n, (evaluation_date, source) in enumerate(sources):
            with H.case_settings(v.enc(evaluation_date)) as st:
                source_atm = H.call("AtmSmileSection::atmLevel", {"source": source, "atm": None})
                normal = source["volatilityType"] == N
                shift = source.get("shift", 0.0)
                given = (source_atm if source_atm is not None else 0.02) + (0.003 if normal else 0.002)
                for atm in (None, given):
                    spec = {"source": source, "atm": atm}
                    level = atm if atm is not None else (source_atm if source_atm is not None else 0.02)
                    ks = strikes(level, normal, shift)

                    def call(member, *args, spec=spec):
                        return v.attempt(lambda: H.call("AtmSmileSection::" + member, spec, *args))

                    expected = {m: call(m) for m in INSPECTORS}
                    expected["volatility"] = [call("volatility", k) for k in ks]
                    expected["variance"] = [call("variance", k) for k in ks]
                    expected["call"] = [call("optionPrice", k, CALL, 0.97) for k in ks]
                    expected["put"] = [call("optionPrice", k, PUT, 0.97) for k in ks]
                    expected["digitalCall"] = [call("digitalOptionPrice", k, CALL, 0.97, 1.0e-5) for k in ks]
                    expected["digitalPut"] = [call("digitalOptionPrice", k, PUT, 0.97, 1.0e-5) for k in ks]
                    expected["vega"] = [call("vega", k, 0.97) for k in ks]
                    expected["density"] = [call("density", k, 0.97, 1.0e-4) for k in ks]
                    out.append({"name": f"AtmSmileSection {n}, {'its source level' if atm is None else 'a level'}",
                                "inputs": {"settings": st, "class": "AtmSmileSection", "section": spec, "strikes": ks},
                                "expected": expected})
                for recenter in (False, True):
                    spec = {"source": source, "atm": given, "recenterSmile": recenter}
                    ks = strikes(given, normal, shift)

                    def call(member, *args, spec=spec):
                        return v.attempt(lambda: H.call("AtmAdjustedSmileSection::" + member, spec, *args))

                    expected = {m: call(m) for m in INSPECTORS}
                    expected["volatility"] = [call("volatility", k) for k in ks]
                    expected["variance"] = [call("variance", k) for k in ks]
                    out.append({"name": f"AtmAdjustedSmileSection {n}, {'recentred' if recenter else 'not recentred'}",
                                "inputs": {"settings": st, "class": "AtmAdjustedSmileSection", "section": spec,
                                           "strikes": ks},
                                "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/atmsmilesection.hpp", cases)
