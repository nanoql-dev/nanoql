# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Libor::jointCalendar() (ql/indexes/ibor/libor.hpp), each output computed by the C++ harness: the wheel
doesn't bind it. Four Libor indexes built as tools/vectors/gen/indexes/ibor/libor.py builds them (CAD on Canada,
CHF on Switzerland, GBP and USD on TARGET), each with the joint calendar of the London exchange and its financial
center: its name and whether each day of 2025 is a business day on it.

Each case's inputs hold the Libor spec ("index", that generator's) and the dates ("dates").
tests/NanoQL.Tests/Vectors/Cxx/Indexes/Ibor/LiborCxxVectorTests.cs reads it back."""
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

_spec = importlib.util.spec_from_file_location("wheel_libor", os.path.join(v.GEN_ROOT, "indexes", "ibor", "libor.py"))
lib = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(lib)

TODAY = lib.eur.TODAY
FIRST = ql.Date(1, ql.January, 2025)

INDEXES = [
    lib.libor("CADLibor", (3, ql.Months), 2, "CAD", "Canada", "Actual365Fixed")[0],
    lib.libor("CHFLibor", (6, ql.Months), 2, "CHF", "Switzerland", "Actual360")[0],
    lib.libor("GBPLibor", (1, ql.Years), 0, "GBP", "TARGET", "Actual365Fixed")[0],
    lib.libor("USDLibor", (1, ql.Weeks), 2, "USD", "TARGET", "Actual360")[0],
]


def cases():
    with cxx.connect() as H:
        out = []
        dates = [v.enc(FIRST + n) for n in range(365)]
        for spec in INDEXES:
            with H.case_settings(v.enc(TODAY)) as st:
                joint = H.call("Libor::jointCalendar", spec, dates)
                out.append({"name": f"{spec['familyName']} {joint['name']}",
                            "inputs": {"settings": st, "index": spec, "dates": dates},
                            "expected": {"name": joint["name"], "isBusinessDay": joint["isBusinessDay"]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/libor.hpp", cases)
