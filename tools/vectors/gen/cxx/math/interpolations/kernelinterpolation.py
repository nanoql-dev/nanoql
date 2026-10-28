# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for KernelInterpolation and KernelInterpolation2D (ql/math/interpolations/kernelinterpolation.hpp,
kernelinterpolation2d.hpp) and GaussianKernel (ql/math/kernelfunctions.hpp), which the wheel doesn't bind, each output
computed by the C++ harness: the one-dimensional interpolation of smooth and random data on 5 to 9 nodes under Gaussian
kernels of three widths and interpolations.cpp's Epanechnikov kernel, at the nodes, between them and beyond them
(extrapolation enabled), and at a tolerance the solve can't meet, which QuantLib refuses; the two-dimensional one on 4 x
5 and 6 x 3 grids; and the Gaussian kernel's value, derivative and primitive. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/KernelInterpolationCxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

KERNELS = [{"gaussian": [0.0, 1.0]}, {"gaussian": [0.0, 0.4]}, {"gaussian": [0.5, 2.0]}, "epanechnikov"]


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        data = [("sin on 5 nodes", [0.0, 0.5, 1.2, 2.0, 3.0], None),
                ("random on 9 nodes", sorted(rng.uniform(-3.0, 3.0) for _ in range(9)), None)]
        for name, x, _ in data:
            y = [math.sin(t) for t in x] if name.startswith("sin") else [rng.uniform(-1.0, 1.0) for _ in x]
            at = sorted(set(x + [(x[i] + x[i + 1]) / 2 for i in range(len(x) - 1)] + [x[0] - 0.7, x[-1] + 1.3]))
            for k in KERNELS:
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                out.append({"name": f"{name}, kernel {k}", "inputs": {"settings": st, "method": "KernelInterpolation",
                                                                      "x": v.enc(x), "y": v.enc(y), "kernel": k,
                                                                      "epsilon": 1e-7, "at": v.enc(at)},
                            "expected": {"values": v.attempt(lambda: H.call("KernelInterpolation", x, y, k, 1e-7,
                                                                            at))}})
        x = [0.0, 1.0, 2.0, 3.0, 4.0]
        y = [rng.uniform(-1.0, 1.0) for _ in x]
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out.append({"name": "an epsilon of 1e-300 (refused)",
                    "inputs": {"settings": st, "method": "KernelInterpolation", "x": x, "y": v.enc(y),
                               "kernel": {"gaussian": [0.0, 1.0]}, "epsilon": 1e-300, "at": [0.5]},
                    "expected": {"values": v.attempt(lambda: H.call("KernelInterpolation", x, y,
                                                                    {"gaussian": [0.0, 1.0]}, 1e-300, [0.5]))}})
        for xs, ys in (([0.0, 1.0, 2.0, 3.5], [-1.0, 0.0, 0.5, 1.5, 2.0]), ([-1.0, -0.2, 0.3, 1.0, 1.4, 2.0],
                                                                           [0.0, 1.0, 3.0])):
            z = [[math.exp(-0.1 * a * a) * math.cos(b) + rng.uniform(-0.05, 0.05) for b in ys] for a in xs]
            at = [[xs[0], ys[0]], [xs[-1], ys[-1]], [(xs[0] + xs[1]) / 2, (ys[1] + ys[2]) / 2], [xs[-1] + 0.5,
                                                                                               ys[0] - 0.4]]
            for k in KERNELS:
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                out.append({"name": f"{len(xs)} x {len(ys)} grid, kernel {k}",
                            "inputs": {"settings": st, "method": "KernelInterpolation2D", "x": xs, "y": ys,
                                       "z": v.enc(z), "kernel": k, "at": v.enc(at)},
                            "expected": {"values": v.attempt(lambda: H.call("KernelInterpolation2D", xs, ys, z, k,
                                                                            at))}})
        pts = [-3.0, -1.0, -0.25, 0.0, 0.4, 1.0, 2.5, 6.0]
        for average, sigma in ((0.0, 1.0), (0.5, 0.3), (-1.0, 2.0)):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = H.call("GaussianKernel", average, sigma, pts)
            out.append({"name": f"Gaussian kernel, average {average}, sigma {sigma}",
                        "inputs": {"settings": st, "method": "GaussianKernel", "average": average, "sigma": sigma,
                                   "at": pts},
                        "expected": {k: v.enc(r[k]) for k in ("value", "derivative", "primitive")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/kernelinterpolation.hpp", cases)
