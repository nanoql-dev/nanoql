# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's tenor swaption volatility (ql/experimental/basismodels/tenorswaptionvts.hpp, .cpp), which
the wheel doesn't bind, each output computed by the C++ harness (tools/cxx/src/experimental/basismodels/basismodels.cpp,
"TenorSwaptionVTS::volatility").

- "volatility" cases: test-suite/basismodels.cpp's normal Euribor 6M swaption volatilities on 15 June 2026,
  transformed to its Euribor 3M index (Euribor 6M on the unspread curve, as QuantLib's case builds it), to Euribor 6M
  itself, and to a true Euribor 3M: the volatility at option tenors of one to twenty years, swap tenors of one to
  twenty years and strikes of 0 to 2%, and the smile section's at-the-money level at each.

Left out: swap tenors that aren't whole years, whose swaps QuantLib truncates to whole years (the S-xrt plan's XRR5
(xv), tests/NanoQL.Tests/Divergences/TenorSwaptionLengthTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/BasisModels/TenorSwaptionVTSCxxVectorTests.cs."""
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


cf = _load("cxx_swaptioncfs", "cxx/experimental/basismodels/swaptioncfs.py")
EVAL = cf.EVAL
Y = cf.Y
SPREAD = 0.01
TERMS = [[1, Y], [5, Y], [10, Y], [20, Y], [30, Y]]
VOLS = [[0.002616, 0.00468, 0.0056, 0.005852, 0.005823],
        [0.006213, 0.00643, 0.006622, 0.006124, 0.005958],
        [0.006658, 0.006723, 0.006602, 0.005802, 0.005464],
        [0.005728, 0.005814, 0.005663, 0.004689, 0.004276],
        [0.005041, 0.005059, 0.004746, 0.003927, 0.003608]]


def cases():
    out = []
    base = {"optionTenors": TERMS, "swapTenors": TERMS, "volatilities": VOLS}
    proj3m = cf.curve_spec(cf.PROJ3M_RATES)
    proj6m = cf.curve_spec(cf.PROJ3M_RATES, SPREAD)
    tenors = [[1, Y], [5, Y], [10, Y], [20, Y]]
    strikes = [0.0, 0.01, 0.02]
    with cxx.connect() as H:
        st = H.settings(v.enc(EVAL))
        for label, targ in (("QuantLib's Euribor 3M", cf.index_spec("6M", proj3m)),
                            ("Euribor 6M itself", cf.index_spec("6M", proj6m)),
                            ("a true Euribor 3M", cf.index_spec("3M", proj3m))):
            bi = cf.index_spec("6M", proj6m)
            out.append({"name": label,
                        "inputs": {"settings": st, "base": base, "discount": cf.DISC, "baseIndex": bi,
                                   "targIndex": targ, "optionTenors": tenors, "swapTenors": tenors,
                                   "strikes": strikes},
                        "expected": H.call("TenorSwaptionVTS::volatility", base, cf.DISC, bi, targ, tenors, tenors,
                                           strikes)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/basismodels/tenorswaptionvts.hpp", cases)
