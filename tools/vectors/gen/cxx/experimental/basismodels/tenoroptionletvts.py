# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's tenor optionlet volatility (ql/experimental/basismodels/tenoroptionletvts.hpp, .cpp), which
the wheel doesn't bind, each output computed by the C++ harness (tools/cxx/src/experimental/basismodels/basismodels.cpp,
"TenorOptionletVTS::volatility").

- "volatility" cases: test-suite/basismodels.cpp's normal caplet volatilities on Euribor 3M (its strikes and ten
  terms from 15 June 2026), transformed to a target index a spread of 1% above: QuantLib's case's indexes (Euribor 6M
  on both curves) and Euribor 3M to Euribor 6M, at its two correlations (ρ∞ 0.3, β 0.9; and 0, 0): the volatility at
  each term to 25 years and each strike, the strikes shifted by the spread too, and the smile section's at-the-money
  level and strike range at each term.

Left out: 30-year caplets, past the base structure's last date, which QuantLib's structure refuses on most evaluation
dates even when asked to extrapolate (the S-xrt run's R2-4, tests/NanoQL.Tests/Divergences/TenorOptionletExtrapolationTests.cs).
Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/BasisModels/TenorOptionletVTSCxxVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cf = _load("cxx_swaptioncfs", "cxx/experimental/basismodels/swaptioncfs.py")
EVAL = cf.EVAL
Y = cf.Y
SPREAD = 0.01
CAPLET_TERMS = [1, 2, 3, 5, 7, 10, 15, 20, 25, 30]
CAPLET_STRIKES = [-0.0050, 0.0000, 0.0050, 0.0100, 0.0150, 0.0200, 0.0300, 0.0500]
CAPLET_VOLS = [
    [0.003010094, 0.002628065, 0.00456118, 0.006731268, 0.008678572, 0.010570881, 0.014149552, 0.021000638],
    [0.004173715, 0.003727039, 0.004180263, 0.005726083, 0.006905876, 0.008263514, 0.010555395, 0.014976523],
    [0.005870143, 0.005334526, 0.005599775, 0.006633987, 0.007773317, 0.009036581, 0.011474391, 0.016277549],
    [0.007458597, 0.007207522, 0.007263995, 0.007308727, 0.007813586, 0.008274858, 0.009743988, 0.012555171],
    [0.007711531, 0.007608826, 0.007572816, 0.007684107, 0.007971932, 0.008283118, 0.009268828, 0.011574083],
    [0.007619605, 0.007639059, 0.007719825, 0.007823373, 0.00800813, 0.008113384, 0.008616374, 0.009785436],
    [0.007312199, 0.007352993, 0.007369116, 0.007468333, 0.007515657, 0.00767695, 0.008020447, 0.009072769],
    [0.006905851, 0.006966315, 0.007056413, 0.007116494, 0.007259661, 0.00733308, 0.007667563, 0.008419696],
    [0.006529553, 0.006630731, 0.006749022, 0.006858027, 0.007001959, 0.007139097, 0.007390404, 0.008036255],
    [0.006225482, 0.006404012, 0.00651594, 0.006642273, 0.006640887, 0.006885713, 0.007093024, 0.00767373]]


def base_spec():
    proj3m = cf.curve_spec(cf.PROJ3M_RATES)
    dates = [v.enc(ql.TARGET().advance(EVAL, ql.Period(n, ql.Years), ql.Following)) for n in CAPLET_TERMS]
    return {"curve": proj3m, "dates": dates, "strikes": CAPLET_STRIKES, "volatilities": CAPLET_VOLS}


def cases():
    out = []
    proj3m = cf.curve_spec(cf.PROJ3M_RATES)
    proj6m = cf.curve_spec(cf.PROJ3M_RATES, SPREAD)
    tenors = [[n, Y] for n in CAPLET_TERMS if n < 30]
    strikes = sorted(set(CAPLET_STRIKES + [k + SPREAD for k in CAPLET_STRIKES]))
    with cxx.connect() as H:
        st = H.settings(v.enc(EVAL))
        for label, base_index in (("QuantLib's indexes", "6M"), ("3M to 6M", "3M")):
            bi = cf.index_spec(base_index, proj3m)
            ti = cf.index_spec("6M", proj6m)
            for rho, beta in ((0.3, 0.9), (0.0, 0.0)):
                corr = {"rhoInf": rho, "beta": beta}
                out.append({"name": f"{label}, correlation {rho} {beta}",
                            "inputs": {"settings": st, "base": base_spec(), "baseIndex": bi, "targIndex": ti,
                                       "correlation": corr, "tenors": tenors, "strikes": strikes},
                            "expected": H.call("TenorOptionletVTS::volatility", base_spec(), bi, ti, corr, tenors,
                                               strikes)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/basismodels/tenoroptionletvts.hpp", cases)
