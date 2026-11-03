# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FastFourierTransform (ql/math/fastfouriertransform.hpp), which the wheel doesn't expose,
each output computed by the C++ harness: the minimum order of sizes 1 to 70, the output size of orders 0 to 12, and the
transform and inverse transform of complex and real inputs at orders 0 to 7, the inputs as many as the points or fewer
(the rest of the output zero, as the harness's output starts). An input longer than the transform, which QuantLib writes
past the output's end before refusing, is left out (the playbook's undefined-behaviour row).

The harness unit is tools/cxx/src/math/fastfouriertransform.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/FastFourierTransformCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        out.append({"name": "the minimum orders of 1 to 70", "inputs": {"settings": st, "call": "min_order", "sizes": list(range(1, 71))},
                    "expected": {"orders": [H.call("FastFourierTransform.min_order", n) for n in range(1, 71)]}})
        out.append({"name": "the output sizes of orders 0 to 12", "inputs": {"settings": st, "call": "output_size", "orders": list(range(13))},
                    "expected": {"sizes": [H.call("FastFourierTransform.output_size", o) for o in range(13)]}})
        for order in range(8):
            points = 1 << order
            for count in sorted({points, max(1, points // 2 + 1), 1}):
                z = [[round(rng.uniform(-10, 10), 6), round(rng.uniform(-10, 10), 6)] for _ in range(count)]
                x = [round(rng.uniform(-10, 10), 6) for _ in range(count)]
                for inverse in (False, True):
                    way = "inverse" if inverse else "direct"
                    out.append({"name": f"order {order}, {count} complex inputs, {way}",
                                "inputs": {"settings": st, "call": "transform", "order": order, "inputs": z, "inverse": inverse},
                                "expected": {"output": H.call("FastFourierTransform.transform", order, z, inverse)}})
                    out.append({"name": f"order {order}, {count} real inputs, {way}",
                                "inputs": {"settings": st, "call": "transformReal", "order": order, "inputs": x, "inverse": inverse},
                                "expected": {"output": H.call("FastFourierTransform.transformReal", order, x, inverse)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/fastfouriertransform.hpp", cases)
