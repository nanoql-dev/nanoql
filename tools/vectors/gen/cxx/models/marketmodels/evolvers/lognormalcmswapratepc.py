# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogNormalCmSwapRatePc (ql/models/marketmodels/evolvers/lognormalcmswapratepc.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: whole evolutions, step by step.

lognormalfwdratepc.py's FlatVol models, here of constant-maturity swap rates spanning 2 and 3 forwards: six
semi-annual and five irregular rates, an exponential correlation, the default evolution times and every other rate
time, full and reduced factors, undisplaced and displaced 1% (full factors, terminal measure); the Mersenne-twister
generator (seed 42) and Sobol's (diagonal ordering, seed 1234); the terminal and money-market measures; from step 0,
with the model's initial state and a set one, and from step 1 (Mersenne twister, the model's state). The set state is
a CMSwapCurveState of the evolver's span on the swap rates of an LMMCurveState on the given forwards, the only state
type QuantLib's setInitialState reads (the S-mm plan's MR3 (v)); nanoql reads the same rates from the LMM state
itself. Each of two paths: its weight and each step's weight, current step, the curve state's forwards, discount
ratios to the last bond and coterminal swap rates. Throws: incompatible numeraires.

The loop is lognormalfwdrateipc.py's; the harness unit tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LogNormalCmSwapRatePcCxxVectorTests.cs."""
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
SPANS = (2, 3)


def keep(elabel, factors, d, glabel, mlabel, initial, slabel):
    return d == 0.0 or mlabel == "terminal"


def cases():
    with cxx.connect() as H:
        st = H.settings(ipc.R)
        out = []
        for span in SPANS:
            evolver = {"type": "LogNormalCmSwapRatePc", "spanningForwards": span}
            extra = {"spanningForwards": span}
            for c in ipc.evolutions(H, st, evolver, keep, extra=extra):
                c["name"] = f"spanning {span}, {c['name']}"
                out.append(c)
            for c in ipc.refused(H, st, evolver, ipc.incompatible, "refused numeraires", extra=extra):
                c["name"] = f"spanning {span}, {c['name']}"
                out.append(c)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/lognormalcmswapratepc.hpp", cases)
