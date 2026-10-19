# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DigitalReplication and Replication::Type (ql/cashflows/replication.hpp), which the wheel
doesn't bind, each output computed by the C++ harness: the replication type and gap of a DigitalReplication built with
no argument (QuantLib's defaults, Central and 1e-4), with each type alone, and with a type and a gap, a zero and a
negative gap among them; and the stream text of each type.

Each case's inputs hold its settings (the evaluation date, which nothing here reads) and "arguments", the constructor's
given arguments in order, or "type" for a text. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Coupons/ReplicationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SUB, CENTRAL, SUPER = 0, 1, 2
ARGUMENTS = [[], [SUB], [CENTRAL], [SUPER], [SUB, 1e-3], [CENTRAL, 0.0], [SUPER, -1e-4], [CENTRAL, 0.25]]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for args in ARGUMENTS:
            r = H.call("DigitalReplication", *args)
            out.append({"name": f"DigitalReplication({', '.join(str(a) for a in args)})",
                        "inputs": {"settings": st, "arguments": args},
                        "expected": {"replicationType": r["replicationType"], "gap": r["gap"]}})
        for t in (SUB, CENTRAL, SUPER):
            out.append({"name": f"operator<<({t})", "inputs": {"settings": st, "type": t},
                        "expected": {"text": H.call("operator<<(Replication::Type)", t)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/replication.hpp", cases)
