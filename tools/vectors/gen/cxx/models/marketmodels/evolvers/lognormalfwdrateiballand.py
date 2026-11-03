# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogNormalFwdRateiBalland (ql/models/marketmodels/evolvers/lognormalfwdrateiballand.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: whole evolutions of one step from time 0.

QuantLib's geometric mean takes each forward's time-0 value at every step, undisplaced, where nanoql takes the step's
start and end, displaced; the two agree on one step from time 0, undisplaced, the case QuantLib's one-step tests run,
and only there. So: lognormalfwdratepc.py's FlatVol models (six semi-annual and five irregular rates, an exponential
correlation) of one evolution step, to the first rate time and to the third, at full factors, undisplaced; on the
Mersenne-twister generator (seed 42) and Sobol's (diagonal ordering, seed 1234); under the terminal measure, the one the
evolver takes; from step 0 with the model's initial state. Each of two paths: its weight and the step's weight, current
step, the curve state's forwards, discount ratios to the last bond and coterminal swap rates. Throws: the money-market
measure, refused; incompatible numeraires.

The loop is lognormalfwdrateipc.py's; the harness unit tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LogNormalFwdRateiBallandCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ipc = _load("cxx_lognormalfwdrateipc", "cxx/models/marketmodels/evolvers/lognormalfwdrateipc.py")
TYPE = "LogNormalFwdRateiBalland"


def one_step(fix):
    return [("one step to the first rate time", [fix[0]]), ("one step to the third rate time", [fix[2]])]


def keep(elabel, factors, d, glabel, mlabel, initial, slabel):
    return initial == 0 and slabel == "model state" and mlabel == "terminal"


def cases():
    with cxx.connect() as H:
        st = H.settings(ipc.R)
        out = ipc.evolutions(H, st, TYPE, keep, evolution_sets=one_step, displacements=(0.0,))
        out += ipc.refused(H, st, TYPE,
                           lambda times, evol: [("the money-market measure", ipc.pc.measures(times, evol)[1][1])]
                           + ipc.incompatible(times, evol), "refused numeraires")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/lognormalfwdrateiballand.hpp", cases)
