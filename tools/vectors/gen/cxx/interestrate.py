# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The InterestRate twin: tools/vectors/gen/interestrate.py's 700 cases, every output computed by the C++ harness
(ql/interestrate.hpp). The case design is the wheel generator's own, imported, so the two can't drift.
tools/vectors/agree.py checks it against tests/vectors/interestrate.json, and
tests/NanoQL.Tests/Vectors/Cxx/InterestRateCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrate", os.path.join(v.GEN_ROOT, "interestrate.py"))
wheel = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(wheel)


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        d1, ends = v.enc(wheel.START), v.enc(wheel.ENDS)
        out = []
        for r in wheel.RATES:
            for dcs in wheel.DAY_COUNTERS:
                dc_name = H.call("DayCounter::name", dcs)
                for cn, c in wheel.COMPOUNDINGS.items():
                    for fn, f in wheel.FREQUENCIES.items():
                        name = f"{r} {dc_name} {cn} {fn}"
                        inputs = {"settings": st, "rate": r, "dayCounter": dcs, "compounding": int(c),
                                  "frequency": int(f)}
                        try:
                            H.call("InterestRate", r, dcs, int(c), int(f))
                        except RuntimeError:
                            out.append({"name": name, "inputs": inputs, "expected": {"interestRate": dict(v.THROWS)}})
                            continue
                        other = wheel.other_day_counter(dcs)
                        inputs.update({"times": wheel.TIMES, "d1": d1, "d2": ends,
                                       "equivalents": [[int(cc), int(ff)] for cc, ff in wheel.EQUIVALENTS],
                                       "equivalentTime": wheel.EQUIVALENT_TIME,
                                       "impliedCompound": wheel.IMPLIED_COMPOUND, "impliedTime": wheel.IMPLIED_TIME,
                                       "equivalentDates": {"dayCounter": other,
                                                           "compounding": int(wheel.EQUIVALENT_DATES[0]),
                                                           "frequency": int(wheel.EQUIVALENT_DATES[1])}})
                        ir = {"rate": r, "dayCounter": dcs, "compounding": int(c), "frequency": int(f)}
                        oc, of = int(wheel.EQUIVALENT_DATES[0]), int(wheel.EQUIVALENT_DATES[1])

                        def call(method, *params):
                            return lambda: H.call(method, *params)

                        def rate_of(method, *params):
                            return lambda: H.call(method, *params)["rate"]

                        out.append({"name": name, "inputs": inputs, "expected": {
                            "frequency": H.call("InterestRate::frequency", ir),
                            "compoundFactor": [v.attempt(call("InterestRate::compoundFactor(Time)", ir, t))
                                               for t in wheel.TIMES],
                            "discountFactor": [v.attempt(call("InterestRate::discountFactor(Time)", ir, t))
                                               for t in wheel.TIMES],
                            "compoundFactorDates": [v.attempt(call("InterestRate::compoundFactor(Date,Date)", ir,
                                                                   d1, d2)) for d2 in ends],
                            "equivalentRate": [v.attempt(rate_of(
                                "InterestRate::equivalentRate(Compounding,Frequency,Time)", ir, int(cc), int(ff),
                                wheel.EQUIVALENT_TIME)) for cc, ff in wheel.EQUIVALENTS],
                            "equivalentRateDates": [v.attempt(rate_of(
                                "InterestRate::equivalentRate(DayCounter,Compounding,Frequency,Date,Date)", ir,
                                other, oc, of, d1, d2)) for d2 in ends],
                            "impliedRate": v.attempt(rate_of(
                                "InterestRate::impliedRate(Real,DayCounter,Compounding,Frequency,Time)",
                                wheel.IMPLIED_COMPOUND, dcs, int(c), int(f), wheel.IMPLIED_TIME)),
                            "toString": v.attempt(call("operator<<(InterestRate)", ir))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/interestrate.hpp", cases)
