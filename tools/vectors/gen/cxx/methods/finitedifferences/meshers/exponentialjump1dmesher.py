# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ExponentialJump1dMesher's jump-size density and distribution
(ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp, .cpp), which the wheel's mesher doesn't expose, each
output computed by the C++ harness: the stationary density and distribution and those at a time t, for jump sizes from
1e-6 to 20, at the swing tests' two parameter sets and two others, with beta t from 0.01 to 100. There QuantLib's
integral from 1e-100 matches the closed form nanoql integrates to 5e-13, and its density at t, a difference of
incomplete gammas, nanoql's integral of the closed form's derivative to 2.2e-10 absolute (relative 1.1e-7 at x 20, where
both gammas near 1 cancel, and 0 for 4e-20 to 7e-18 where they underflow); from beta t about 230 its cut holds mass and
QuantLib's distribution falls off, below about 1e-4 its density cancels, and from about 750 it refuses (the S-core
plan's CR7 (xvii) and the code review's C4-O2; tests/NanoQL.Tests/Divergences/ExponentialJump1dMesherTests.cs), so those
times are left out, as is a jump intensity at or above beta, where QuantLib's density at t refuses Gamma(1 -
intensity/beta) and nanoql's routes compute. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Meshers/ExponentialJump1dMesherCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

XS = [1e-6, 0.01, 0.1, 0.5, 1.0, 2.0, 5.0, 20.0]
PARAMETERS = [(5.0, 1.0, 2.0), (100.0, 4.0, 2.5), (1.0, 0.5, 2.0), (10.0, 2.0, 1.0)]
BETA_T = [0.01, 0.5, 3.0, 20.0, 100.0]
FIELDS = ("density", "densityAt", "distribution", "distributionAt")


def cases():
    out = []
    with cxx.connect() as H:
        for beta, intensity, eta in PARAMETERS:
            for bt in BETA_T:
                t = bt / beta
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                r = H.call("ExponentialJump1dMesher::jumpSize", 10, beta, intensity, eta, 1e-3, XS, t)
                out.append({"name": f"beta {beta}, intensity {intensity}, eta {eta}, t {t!r}",
                            "inputs": {"settings": st, "beta": beta, "jumpIntensity": intensity, "eta": eta,
                                       "x": XS, "t": t},
                            "expected": {k: v.enc(r[k]) for k in FIELDS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp", cases)
