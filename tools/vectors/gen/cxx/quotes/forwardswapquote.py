# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardSwapQuote (ql/quotes/forwardswapquote.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness: the fair rate of a forward-starting swap of a swap index, plus a spread.

On EuriborSwapIsdaFixA indexes of 2, 5 and 10 years forecast on a flat 3% curve, discounted on it or on an exogenous
flat 2.5% curve, on Wednesday 15 January 2025 and Friday 31 January 2025: forward starts of 0 days, 1 month, 1 year and
5 years, and spreads of 0 (QuantLib's empty handle) and 10 basis points. Each quote's value and its value, start and
fixing dates. A case's inputs are the "swapIndex" ({"type": "EuriborSwapIsdaFixA", "tenor", "forwarding"}, with
"discounting" for exogenous discounting, as tools/cxx/src/cashflows/cmsspec.hpp documents it), the "spread" and the
"fwdStart" ([length, units]). The harness unit is tools/cxx/src/quotes/forwardswapquote.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Quotes/QuoteCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)


def cases():
    out = []
    with cxx.connect() as H:
        for today in (ql.Date(15, ql.January, 2025), ql.Date(31, ql.January, 2025)):
            st = H.settings(today.serialNumber())
            for years in (2, 5, 10):
                for exogenous in (False, True):
                    index = {"type": "EuriborSwapIsdaFixA", "tenor": [years, int(ql.Years)],
                             "forwarding": irx.curve_spec(today, 0.03)}
                    if exogenous:
                        index["discounting"] = irx.curve_spec(today, 0.025)
                    for start in ((0, ql.Days), (1, ql.Months), (1, ql.Years), (5, ql.Years)):
                        for spread in (0.0, 0.001):
                            fwd = [start[0], int(start[1])]
                            out.append({"name": f"{today.ISO()} {years}Y{' exogenous' if exogenous else ''} "
                                                f"from {start[0]} {start[1]} spread {spread}",
                                        "inputs": {"settings": st, "swapIndex": index, "spread": spread,
                                                   "fwdStart": fwd},
                                        "expected": v.enc(H.call("ForwardSwapQuote", index, spread, fwd))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/forwardswapquote.hpp", cases)
