# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VannaVolgaInterpolation and its VannaVolga factory
(ql/experimental/barrieroption/vannavolgainterpolation.hpp), which the wheel doesn't bind, computed by the C++ harness
(tools/cxx/src/experimental/barrieroption/vannavolgainterpolation.cpp).

Three-volatility smiles (a put skew, a call skew, a smile, a flat one) on FX-like and equity-like markets at a month, a
year and three years: the interpolation's values at its three strikes, between them and beyond them, through the
interpolation and through the factory; the factory's requiredPoints; and QuantLib's refusals of two and four strikes
("Vanna Volga Interpolator only interpolates 3 volatilities in strike space").

**The stability filter**: each smile is interpolated again with its volatilities moved one ulp up, then down, by
math.nextafter, every one together and each alone; a value is written only where every move leaves it within 1e-12 ×
max(|base|, |moved|, 1), its point dropped from "points" otherwise. The value solves Black's formula for a standard
deviation (blackFormulaImpliedStdDev). The generator prints the dropped points.

A case's inputs are the "x" and "y" (the strikes and volatilities, the second at the money), the "spot", "dDiscount",
"fDiscount" and "t", and the "points"; its output the "values" there, or for the factory "factoryValues". Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/BarrierOption/VannaVolgaInterpolationCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TOLERANCE = 1e-12
MARKETS = [("FX", 1.3, 0.97, 0.99), ("equity", 100.0, 0.95, 0.98)]
SHAPES = [("put skew", (0.13, 0.12, 0.115)), ("call skew", (0.115, 0.12, 0.135)), ("smile", (0.14, 0.12, 0.13)),
          ("flat", (0.12, 0.12, 0.12))]
TIMES = [1.0 / 12.0, 1.0, 3.0]


def moved_sets(vols):
    out = [[math.nextafter(x, math.inf) for x in vols], [math.nextafter(x, -math.inf) for x in vols]]
    for i in range(len(vols)):
        for d in (math.inf, -math.inf):
            moved = list(vols)
            moved[i] = math.nextafter(vols[i], d)
            out.append(moved)
    return out


def stable(base, moved):
    if math.isnan(base) or math.isnan(moved):
        return math.isnan(base) and math.isnan(moved)
    return abs(base - moved) <= TOLERANCE * max(abs(base), abs(moved), 1.0)


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out, dropped, total = [], 0, 0
        for mname, spot, d_df, f_df in MARKETS:
            forward = spot * f_df / d_df
            for t in TIMES:
                spread = 0.12 * math.sqrt(t)
                x = [forward * math.exp(-spread), forward, forward * math.exp(spread)]
                points = sorted({*x, forward * math.exp(-0.5 * spread), forward * math.exp(0.5 * spread),
                                 forward * math.exp(-2.0 * spread), forward * math.exp(2.0 * spread)})
                for sname, y in SHAPES:
                    def values(vols, method, args_first):
                        if args_first:
                            return H.call(method, spot, d_df, f_df, t, x, list(vols), points)
                        return H.call(method, x, list(vols), spot, d_df, f_df, t, points)
                    for method, key, first in (("VannaVolgaInterpolation::VannaVolgaInterpolation", "values", False),
                                               ("VannaVolga::interpolate", "factoryValues", True)):
                        base = values(y, method, first)
                        refits = [values(m, method, first) for m in moved_sets(y)]
                        kept = [i for i in range(len(points)) if all(stable(base[i], r[i]) for r in refits)]
                        dropped += len(points) - len(kept)
                        total += len(points)
                        out.append({"name": f"{mname}, {t:g} years, {sname}, {'factory' if first else 'interpolation'}",
                                    "inputs": {"settings": st, "x": x, "y": list(y), "spot": spot,
                                               "dDiscount": d_df, "fDiscount": f_df, "t": t,
                                               "points": [points[i] for i in kept]},
                                    "expected": {key: [base[i] for i in kept]}})
        out.append({"name": "factory trait / requiredPoints", "inputs": {"settings": st},
                    "expected": {"requiredPoints": H.call("VannaVolga::requiredPoints")}})
        for n in (2, 4):
            x = [1.2, 1.3, 1.4, 1.5][:n]
            y = [0.13, 0.12, 0.115, 0.11][:n]
            out.append({"name": f"refused / {n} strikes",
                        "inputs": {"settings": st, "x": x, "y": y, "spot": 1.3, "dDiscount": 0.97, "fDiscount": 0.99,
                                   "t": 1.0, "points": [1.3]},
                        "expected": {"values": v.attempt(lambda x=x, y=y: H.call(
                            "VannaVolgaInterpolation::VannaVolgaInterpolation", x, y, 1.3, 0.97, 0.99, 1.0, [1.3]))}})
        print(f"dropped {dropped} of {total} points")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/vannavolgainterpolation.hpp", cases)
