# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogNormalCotSwapRatePc (ql/models/marketmodels/evolvers/lognormalcotswapratepc.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: whole evolutions, step by step.

lognormalfwdratepc.py's FlatVol models, here of coterminal swap rates: six semi-annual and five irregular rates, an
exponential correlation, the default evolution times and every other rate time, full and reduced factors, undisplaced
and displaced 1% (full factors, terminal measure); the Mersenne-twister generator (seed 42) and Sobol's (diagonal
ordering, seed 1234); the terminal and money-market measures; from step 0, with the model's initial state and a set one,
and from step 1 (Mersenne twister, the model's state). The set state is a CoterminalSwapCurveState on the coterminal
swap rates of an LMMCurveState on the given forwards, the only state type QuantLib's setInitialState reads; nanoql reads
the same rates from the LMM state itself. Each of two paths: its weight and each step's weight, current step, the curve
state's forwards, discount ratios to the last bond and coterminal swap rates. Throws: incompatible numeraires.

The loop is lognormalfwdrateipc.py's; the harness unit tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LogNormalCotSwapRatePcCxxVectorTests.cs."""
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
TYPE = "LogNormalCotSwapRatePc"


def keep(elabel, factors, d, glabel, mlabel, initial, slabel):
    return d == 0.0 or mlabel == "terminal"


def cases():
    with cxx.connect() as H:
        st = H.settings(ipc.R)
        out = ipc.evolutions(H, st, TYPE, keep)
        out += ipc.refused(H, st, TYPE, ipc.incompatible, "refused numeraires")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/lognormalcotswapratepc.hpp", cases)
