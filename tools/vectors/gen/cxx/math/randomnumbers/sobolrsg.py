# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SobolRsg without the Gray code (ql/math/randomnumbers/sobolrsg.hpp), which the wheel can't build, each
output computed by the C++ harness. For the ten sets of direction integers, an 8-dimensional generator, seed 1: its
dimension, its first 20 nextInt32Sequence draws (a fresh generator's first two are the same point, as each draw
recomputes the point at its counter before advancing it), skipTo's integer sequences for 1,023, 1,024, 2^20 and
10^6 + 3 in turn, one nextSequence draw after skipTo(1023), and the lastSequence after two nextSequence draws. The
constructor's refusals, with and without the Gray code: dimension 0 and 21,201, one above the primitive polynomials'.
The period's end, where QuantLib's result without the Gray code is defined: skipTo(4294967294) and then
skipTo(4294967295), whose index + 1 wraps to 0 and sets every integer to 0; the draws after skipTo(4294967294) until
one throws "period exceeded" (the counter, advanced past 4294967295, wraps to 0), and the same after skipTo(4294967295).
Each draw of those is a fresh generator's last, the earlier ones replayed, so that a throw records its own output. With
the Gray code QuantLib's skipTo(4294967295) and its draw at counter 4294967295 are undefined behaviour, which no vector
reaches. Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/SobolRsgCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SETS = list(range(10))  # Unit, Jaeckel, SobolLevitan, SobolLevitanLemieux, JoeKuoD5, JoeKuoD6, JoeKuoD7, Kuo, Kuo2, Kuo3
JAECKEL = 1
DIMENSION = 8
SEED = 1
DRAWS = 20
SKIPS = [1023, 1024, 2**20, 10**6 + 3]
AFTER_SKIP = 1023
LAST_AFTER = 2
REFUSED = [0, 21201]
END = [4294967294, 4294967295]


def spec(directions, dimension=DIMENSION, gray=False):
    return {"dimension": dimension, "seed": SEED, "directionIntegers": directions, "useGrayCode": gray}


def until_throw(H, s, skip):
    """The draws after skipTo(skip), each a fresh generator's last, up to and with the first that throws."""
    out = []
    while True:
        k = len(out) + 1
        # Not through vectors.attempt, whose encoding reads the integer 2147483647 as QuantLib's Null<Integer>.
        try:
            out.append(H.call("SobolRsg::nextInt32Sequence", s, skip, k)[-1])
        except RuntimeError:
            out.append(dict(v.THROWS))
            return out
        assert k < 10, "no draw threw"


def cases():
    out = []
    with cxx.connect() as H:
        def context():
            return H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        for directions in SETS:
            st = context()
            s = spec(directions)
            out.append({"name": f"directionIntegers {directions}",
                        "inputs": {"settings": st, "dimension": DIMENSION, "seed": SEED,
                                   "directionIntegers": directions, "useGrayCode": False, "draws": DRAWS,
                                   "skips": SKIPS, "afterSkip": AFTER_SKIP, "lastAfter": LAST_AFTER},
                        "expected": {"dimension": H.call("SobolRsg::dimension", s),
                                     "nextInt32Sequence": H.call("SobolRsg::nextInt32Sequence", s, None, DRAWS),
                                     "skipTo": H.call("SobolRsg::skipTo", s, SKIPS),
                                     "nextSequenceAfterSkip": v.enc(H.call("SobolRsg::nextSequence", s, AFTER_SKIP,
                                                                           1)[0]),
                                     "lastSequence": v.enc(H.call("SobolRsg::lastSequence", s, LAST_AFTER))}})
        st = context()
        refused = [(d, gray) for gray in (False, True) for d in REFUSED]
        out.append({"name": "refused dimensions",
                    "inputs": {"settings": st, "dimensions": [d for d, _ in refused],
                               "useGrayCode": [g for _, g in refused], "seed": SEED, "directionIntegers": JAECKEL},
                    "expected": {"dimension": [v.attempt(lambda d=d, g=g: H.call("SobolRsg::dimension",
                                                                                 spec(JAECKEL, d, g)))
                                               for d, g in refused]}})
        st = context()
        s = spec(JAECKEL)
        out.append({"name": "period end without the Gray code",
                    "inputs": {"settings": st, "dimension": DIMENSION, "seed": SEED, "directionIntegers": JAECKEL,
                               "useGrayCode": False, "skips": END},
                    "expected": {"skipTo": H.call("SobolRsg::skipTo", s, END),
                                 "drawsAfterSkip": until_throw(H, s, END[0]),
                                 "drawsAfterEnd": until_throw(H, s, END[1])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/sobolrsg.hpp", cases)
