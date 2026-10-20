# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeCreditDefaultSwap (ql/instruments/makecds.hpp) through the constructor and setters the
wheel doesn't bind, each output computed by the C++ harness: the schedule constructor, and convention, settleAccrual,
payAtDefaultTime, protectionStart, upfrontDate, claim and rebateAccrual, with the wheel's setters beside them.

- "build" cases: on a Monday, a Saturday and a roll date: the swap's inspectors, as
  tools/vectors/gen/instruments/creditdefaultswap.py writes them; a refused build, {"construction": {"throws": true}}
  (a schedule with one date, an upfront date before the protection start).

A case's inputs hold its settings and the builder {"tenor", "termDate" or "schedule", "runningSpread", "setters"}, as
tools/cxx/src/instruments/makecds.cpp reads it. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/MakeCreditDefaultSwapCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_creditdefaultswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "creditdefaultswap.py"))
cds_gen = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cds_gen)
dts = cds_gen.dts
M, Y = cds_gen.M, cds_gen.Y

EVALUATIONS = [ql.Date(15, ql.January, 2024), ql.Date(16, ql.March, 2024), ql.Date(20, ql.March, 2024)]


def schedule(ev, rule=ql.DateGeneration.CDS2015, cal=None):
    return cds_gen.schedule_spec(ev - 4, ql.Date(20, ql.June, 2029), (3, M), cal or dts.WEEKENDS_ONLY, rule)


def builders(ev):
    return [
        ("schedule", {"schedule": schedule(ev)}, []),
        ("schedule, 2009 rule on TARGET, protection start", {"schedule": schedule(ev, ql.DateGeneration.CDS, dts.TARGET)},
         [["protectionStart", v.enc(ev)]]),
        ("5Y, Modified Following", {"tenor": [5, Y]}, [["convention", int(ql.ModifiedFollowing)]]),
        ("5Y, accrual not settled, paid at the end", {"tenor": [5, Y]},
         [["settleAccrual", False], ["payAtDefaultTime", False]]),
        ("5Y, protection start and upfront date", {"tenor": [5, Y]},
         [["protectionStart", v.enc(ev + 1)], ["upfrontDate", v.enc(ev + 10)], ["upfrontRate", 0.01]]),
        ("5Y, face-value claim, no rebate", {"tenor": [5, Y]}, [["claim", "FaceValueClaim"], ["rebateAccrual", False]]),
        ("term date, every setter", {"termDate": v.enc(ql.Date(20, ql.December, 2030))},
         [["side", cds_gen.SELLER], ["nominal", 5e6], ["upfrontRate", -0.005], ["couponTenor", [6, M]],
          ["dateGenerationRule", int(ql.DateGeneration.CDS)], ["convention", int(ql.Following)],
          ["dayCounter", dts.A365], ["settleAccrual", True], ["payAtDefaultTime", True],
          ["lastPeriodDayCounter", dts.A360_INC], ["rebateAccrual", True], ["tradeDate", v.enc(ev - 1)],
          ["cashSettlementDays", 2]]),
        ("a schedule of one date",
         {"schedule": cds_gen.schedule_spec(ev, ev, (3, M), dts.WEEKENDS_ONLY, ql.DateGeneration.CDS2015)}, []),
        ("an upfront date before the protection start", {"tenor": [5, Y]},
         [["protectionStart", v.enc(ev + 5)], ["upfrontDate", v.enc(ev + 2)]]),
    ]


def cases():
    out = []
    k = 0
    with cxx.connect() as H:
        for ev in EVALUATIONS:
            for label, start, setters in builders(ev):
                with H.case_settings(v.enc(ev)) as st:
                    spec = dict(start, runningSpread=0.01, setters=setters)
                    result = v.attempt(lambda: H.call("MakeCreditDefaultSwap::build", spec))
                    expected = {"construction": result} if "throws" in result else result
                    out.append({"name": f"build {label} / {ev.ISO()} / {k}", "inputs": {"settings": st, "builder": spec},
                                "expected": expected})
                k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makecds.hpp", cases)
