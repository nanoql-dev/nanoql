# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IMM::isIMMdate (ql/time/imm.hpp): every day of 2026 to 2028, main cycle and any month."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for y in (2026, 2027, 2028):
        days = [ql.Date(1, 1, y) + i for i in range(366 if ql.Date.isLeap(y) else 365)]
        out.append({"name": f"isIMMdate {y}",
                    "inputs": {"settings": st, "dates": [d.serialNumber() for d in days]},
                    "expected": {"mainCycle": [ql.IMM.isIMMdate(d, True) for d in days],
                                 "anyMonth": [ql.IMM.isIMMdate(d, False) for d in days]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/imm.hpp", cases)
