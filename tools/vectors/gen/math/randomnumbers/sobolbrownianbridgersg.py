# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SobolBrownianBridgeRsg and Burley2020SobolBrownianBridgeRsg
(ql/math/randomnumbers/sobolbrownianbridgersg.hpp, .cpp), on the evaluation date 15 October 2026, which nothing reads:
each generator's dimension and its first five sequences, each the Gaussian variates of SobolBrownianGenerator's (or
Burley2020SobolBrownianGenerator's) path step after step, at factors and steps from 1 × 1 to 19 × 100, on the default
diagonal ordering and JoeKuoD7's direction integers. The Burley generator's wheel constructor takes the factors and
steps only too, so its seeds are QuantLib's defaults 42 and 43, which each Burley case records (with
"scrambleSeed"). The wheel's constructor takes the factors and steps
only, so its seed is QuantLib's default 0, which draws one from the clock; every case's dimension is within JoeKuoD7's
1,900 tabulated dimensions, where the seed is never read (tools/vectors/gen/math/randomnumbers/sobolrsg.py), and the
generator checks that two draws agree. nanoql's seed is explicit, in [1, 4294967295], so each case records 42. Each
case records every value of a sequence at dimensions to 60, and above that its last six and every 50th from the first.
The wheel's calls, by QuantLib-SWIG 1.44's randomnumbers.i: SobolBrownianBridgeRsg(factors, steps) and
Burley2020SobolBrownianBridgeRsg(factors, steps); dimension, nextSequence's value and weight. Read back by tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/SobolBrownianBridgeRsgVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SHAPES = [(1, 1), (1, 8), (3, 5), (2, 16), (5, 12), (4, 32), (19, 100)]
SEQUENCES = 5
FULL = 60
LAST = 6
STRIDE = 50
SEED = 42
SCRAMBLE_SEED = 43


def draws(factors, steps, burley=False):
    """Each sample copied as drawn: the wheel's nextSequence returns the generator's own sample, which the next draw
    overwrites."""
    rsg = ql.Burley2020SobolBrownianBridgeRsg(factors, steps) if burley else ql.SobolBrownianBridgeRsg(factors, steps)
    values, weights = [], []
    for _ in range(SEQUENCES):
        s = rsg.nextSequence()
        values.append(list(s.value()))
        weights.append(s.weight())
    return rsg.dimension(), values, weights


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for factors, steps in SHAPES:
        assert factors * steps <= 1900
        dimension, values, weights = draws(factors, steps)
        assert draws(factors, steps) == (dimension, values, weights), "the seed reached a tabulated dimension"
        expected = {"dimension": dimension, "weights": v.enc(weights)}
        if dimension <= FULL:
            expected["sequences"] = [v.enc(s) for s in values]
        else:
            expected["lastValues"] = [v.enc(s[-LAST:]) for s in values]
            expected["stridedValues"] = [v.enc(s[::STRIDE]) for s in values]
        out.append({"name": f"{factors} factors, {steps} steps",
                    "inputs": {"settings": st, "factors": factors, "steps": steps, "seed": SEED,
                               "sequences": SEQUENCES, "last": LAST, "stride": STRIDE},
                    "expected": expected})
    for factors, steps in SHAPES:
        dimension, values, weights = draws(factors, steps, burley=True)
        expected = {"dimension": dimension, "weights": v.enc(weights)}
        if dimension <= FULL:
            expected["sequences"] = [v.enc(s) for s in values]
        else:
            expected["lastValues"] = [v.enc(s[-LAST:]) for s in values]
            expected["stridedValues"] = [v.enc(s[::STRIDE]) for s in values]
        out.append({"name": f"Burley 2020, {factors} factors, {steps} steps",
                    "inputs": {"settings": st, "factors": factors, "steps": steps, "seed": SEED,
                               "scrambleSeed": SCRAMBLE_SEED, "sequences": SEQUENCES, "last": LAST, "stride": STRIDE},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/sobolbrownianbridgersg.hpp", cases)
