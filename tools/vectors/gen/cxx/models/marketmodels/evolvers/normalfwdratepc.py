# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NormalFwdRatePc (ql/models/marketmodels/evolvers/normalfwdratepc.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: whole evolutions, step by step.

lognormalfwdratepc.py's grids and loop on FlatVol models of normal volatilities (0.8% less 0.03% a rate), undisplaced,
since the normal evolver reads no displacement: six semi-annual and five irregular rates, an exponential correlation,
the default evolution times and every other rate time, full and reduced factors; the Mersenne-twister generator (seed
42) and Sobol's (diagonal ordering, seed 1234); the terminal and money-market measures; from step 0 and step 1
(Mersenne twister), with the model's initial state only: QuantLib's setInitialState keeps the model's forwards (the
S-mm plan's MR3 (iv)), so a set state is left to the Divergences record. Each of two paths: its weight and each step's
weight, current step, the curve state's forwards, discount ratios to the last bond and coterminal swap rates. Throws:
incompatible numeraires.

The loop is lognormalfwdrateipc.py's; the harness unit tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/NormalFwdRatePcCxxVectorTests.cs."""
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
TYPE = "NormalFwdRatePc"


def model(times, evol, factors, d):
    m = ipc.pc.model(times, evol, factors, d)
    m["vols"] = [0.008 - 0.0003 * i for i in range(len(times) - 1)]
    return m


def keep(elabel, factors, d, glabel, mlabel, initial, slabel):
    return slabel == "model state"


def cases():
    with cxx.connect() as H:
        st = H.settings(ipc.R)
        out = ipc.evolutions(H, st, TYPE, keep, model=model, displacements=(0.0,))
        out += ipc.refused(H, st, TYPE, ipc.incompatible, "refused numeraires", model=model)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/normalfwdratepc.hpp", cases)
