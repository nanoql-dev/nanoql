# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's no-arbitrage SABR swaption volatility cube (ql/experimental/volatility/
noarbsabrswaptionvolatilitycube.hpp, the XabrSwaptionVolatilityCube template on SwaptionVolCubeNoArbSabrModel), which
the wheel doesn't expose, each output computed by the C++ harness: 2 calibrated cubes on the evaluation date Monday 16
June 2025, beta fixed, at-the-money calibrated and not.

The cubes are QuantLib's SABR cube fixture (tools/vectors/gen/termstructures/volatility/swaption/swaptionvolcube.py's,
whose functions build them: its 3 x 3 cube of option and swap tenors, its five strike spreads and their volatility
spreads, EuriborSwapIsdaFixA 2Y and 1Y on a flat 5% curve, the guesses alpha 0.2, beta 0.5, nu 0.4, rho 0) on options of
5, 10 and 20 years, the at-the-money matrix the fixture's 5Y, 10Y and 30Y rows at those options, floating on the
evaluation date. Each cube gives one case, as tools/vectors/gen/termstructures/volatility/swaption/
sabrswaptionvolatilitycube.py has the SABR cube's: its four matrices (sparseSabrParameters, denseSabrParameters where
at-the-money calibrated, marketVolCube and volCubeAtmCalibrated), each as its elements row by row, and their shapes; the
volatility at 72 points on the cube's grid and 72 off it, the option tenors 5Y, 10Y and 20Y on the swap tenors 2Y, 10Y
and 30Y, and 7Y, 15Y and 25Y on 5Y, 15Y and 20Y, each at the at-the-money strike plus the five strike spreads and -1%, 1%
and 3%, extrapolating; the volatility by option time and swap length at 4 points and 3 strikes; and the smile section
(a NoArbSabrSmileSection, whose parameters the matrices hold) by option and swap tenors at 8 points and by option time
and swap length at 4, each as its ATM level, exercise time, shift, volatility type and volatility at its ATM level and
1% on either side, past the cube's range only where the point extrapolates; the 35Y and 40Y points are past the model's
30 years, refused.

The outputs nanoql fixes are left out: every option is 5 years or more, so every fit's trial points and every section
have sigmaI sqrt(T) above 0.1 (the transformation keeps sigmaI in [0.05, 1]), where nanoql's integrals are QuantLib's
single intervals; the fixture's 1Y option, its 1M and 6M at-the-money options and its 30Y ones (past the model's 30
years) are left out.

The stability filter runs as on the SABR cube's file: each case is refitted with every input volatility (the
at-the-money matrix's and every spread) moved one ulp up, then down, and an output is written only where both refits
leave it within 1e-12 x max(|a|, |b|, 1); the indexes of each output's dropped elements are in the case's inputs
"dropped", and the generator prints the counts.

A refused value is {"throws": true}. A case's inputs hold its settings, its "cube" spec (swaptionvolcube.py's, of type
"NoArbSabrSwaptionVolatilityCube") and "dropped". The cube is tools/cxx/src/termstructures/volatility/swaption/
sabrswaptionvolatilitycube.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/NoArbSabrSwaptionVolatilityCubeCxxVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "swaptionvolcube", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionvolcube.py"))
svc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svc)

M, Y = svc.M, svc.Y
OPTIONS = [[5, Y], [10, Y], [20, Y]]
GRID_OPTIONS, GRID_SWAPS = OPTIONS, [[2, Y], [10, Y], [30, Y]]
OFF_OPTIONS, OFF_SWAPS = [[7, Y], [15, Y], [25, Y]], [[5, Y], [15, Y], [20, Y]]
OFFSETS = svc.STRIKE_SPREADS + [-0.01, 0.01, 0.03]
SECTION_TENORS = [[[5, Y], [2, Y], False], [[10, Y], [10, Y], False], [[20, Y], [30, Y], False],
                  [[7, Y], [7, Y], False], [[10, Y], [3, Y], False], [[15, Y], [20, Y], False],
                  [[25, Y], [5, Y], True], [[35, Y], [5, Y], True]]
SECTION_TIMES = [[5.5, 1.5, False], [8.0, 4.0, False], [15.0, 20.0, False], [40.0, 40.0, True]]
TIME_STRIKES = [0.03, 0.05, 0.07]


def spec(atm_calibrated):
    atm = svc.mat.floating_spec(svc.TARGET, 6, OPTIONS, svc.mat.ATM_SWAPS, svc.mat.ATM_VOLS[3:], svc.A365)
    s = svc.sabr_spec(atm, svc.flat_curve(svc.TODAY), fixed=svc.BETA_FIXED, atm_calibrated=atm_calibrated,
                      options=OPTIONS)
    s["type"] = "NoArbSabrSwaptionVolatilityCube"
    return s


def outputs(H, s):
    return H.call("NoArbSabrSwaptionVolatilityCube::outputs", s, GRID_OPTIONS, GRID_SWAPS, OFF_OPTIONS, OFF_SWAPS,
                  OFFSETS, SECTION_TIMES, TIME_STRIKES, SECTION_TENORS)


def cases():
    out = []
    dropped_all = total_all = 0
    with cxx.connect() as H:
        st = H.settings(svc.TODAY.serialNumber(), use_at_par_coupons=True)
        for name, s in [("beta fixed, at-the-money calibrated", spec(True)),
                        ("beta fixed, not at-the-money calibrated", spec(False))]:
            base = outputs(H, s)
            refits = [outputs(H, svc.moved(s, d)) for d in (math.inf, -math.inf)]
            kept, dropped, lost, total = svc.filtered(name, base, refits)
            dropped_all += lost
            total_all += total
            out.append({"name": name, "inputs": {"settings": st, "cube": s, "dropped": dropped}, "expected": kept})
    print(f"cxx/experimental/volatility/noarbsabrswaptionvolatilitycube: dropped {dropped_all} of {total_all} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/noarbsabrswaptionvolatilitycube.hpp", cases)
