# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CotSwapToFwdAdapter and CotSwapToFwdAdapterFactory
(ql/models/marketmodels/models/cotswaptofwdadapter.hpp, .cpp), which the wheel doesn't expose, each output computed by
the C++ harness.

fwdtocotswapadapter.py's cases with the coterminal adapter, its models here of coterminal swap rates: FlatVol and
AbcdVol on eight semi-annual and six irregular rates, an exponential correlation, the default evolution times and finer
ones, full and reduced factors, undisplaced and displaced 1%: the adapter's reads (its counts, initial forwards and
displacements, each step's pseudo-root, covariance and total covariance, and each rate's volatility over the steps); the
factory on FlatVolFactory: the adapter it creates and its reads. Throws: unequal displacements, an evolution skipping a
rate time.

The harness unit is tools/cxx/src/models/marketmodels/models/adapters.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CotSwapToFwdAdapterCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fwd = _load("cxx_fwdtocotswapadapter", "cxx/models/marketmodels/models/fwdtocotswapadapter.py")


def cases():
    return fwd.cases("CotSwapToFwdAdapter")


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/cotswaptofwdadapter.hpp", cases)
