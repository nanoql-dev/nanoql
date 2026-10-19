# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscretizedCallableFixedRateBond (ql/experimental/callablebonds/
discretizedcallablefixedratebond.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness:
the asset built from a callable bond's arguments at the case's settlement date and a curve, with a spread.

Ten bonds of tools/vectors/gen/experimental/callablebonds/callablebond.py's fixtures (testCached's calls, puts, both
and dirty calls; a call two days before a coupon date and a put five days before one, each moved to its coupon; the
unreachable dirty call before a coupon date with a clean one on it, whose coupon a moved neighbour carries ahead; an
occurred call ahead of live ones; the ex-coupon bond with a call inside its window; testInterplay's zero-coupon bond),
each with spreads of 0 and 0.01 (which the moved callabilities' discount factors carry), on Hull-White's tree on a grid
through the asset's mandatory times. The outputs: the mandatory times (the redemption's, the live coupons' and the
callabilities', moved, in QuantLib's order, those not negative); the asset initialized at the redemption time and
rolled back to six grid times down to 0, its time and values after each; and its present value after each. The
lattice is tools/cxx/src/models/modelspec.hpp's ({"model", "timeGrid", "spread"}), its spread the asset's. Read back
by tests/NanoQL.Tests/Vectors/Cxx/Experimental/CallableBonds/DiscretizedCallableFixedRateBondCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location(
    "wheel_callablebond", os.path.join(v.GEN_ROOT, "experimental", "callablebonds", "callablebond.py"))
cb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cb)

D = ql.Date


def bonds():
    """(name, settings, bond spec, curve spec, grid steps)."""
    cached = {name: entry for name, *entry in cb.cached_cases()}
    out = []
    for name in ("cached / calls at 110, 240 steps", "cached / puts at 100, 240 steps",
                 "cached / calls and puts, 240 steps", "cached / calls at a dirty 110, 100 steps",
                 "cached / a call two days before a coupon date",
                 "cached / an unreachable dirty call before a coupon date",
                 "cached / an occurred call ahead of live calls"):
        st, spec, engine = cached[name][:3]
        out.append((name.split(" / ")[1], st, spec, engine["model"]["curve"], 60))
    today, cal, settlement, issue, maturity = cb.cached_fixture()
    sched = cb.semiannual(issue, maturity)
    coupons = list(cb.bd.make_schedule(sched))
    put = [cb.callability_spec(100.0, cb.CLEAN, cb.PUT, coupons[9] - 5)]
    out.append(("a put five days before a coupon date", cb.plain(today),
                cb.fixed_spec(3, 10000.0, sched, [0.05], cb.THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue, put),
                cb.flat(settlement, 0.032), 60))
    ex = {name: entry for name, *entry in cb.ex_coupon_cases()}
    st, spec, engine = ex["ex-coupon / a call inside the ex-coupon window"][:3]
    out.append(("a call inside the ex-coupon window", st, spec, engine["model"]["curve"], 50))
    runner = {name: entry for name, *entry in cb.runner_cases()}
    st, spec, engine = runner["interplay / the same, and a later call"][:3]
    out.append(("a zero-coupon bond's call, put and call", st, spec, engine["model"]["curve"], 60))
    return out


def cases():
    out = []
    with cxx.connect() as H:
        for name, st, spec, curve, steps in bonds():
            for spread in (0.0, 0.01):
                with H.case_settings(st[0].serialNumber(), include_reference_date_events=st[1],
                                     include_todays_cash_flows=st[2]) as s:
                    times = H.call("DiscretizedCallableFixedRateBond::mandatoryTimes", spec, curve, spread)
                    lattice = {"model": cb.hull_white(curve), "timeGrid": {"times": times, "steps": steps},
                               "spread": spread if spread != 0.0 else None}
                    # from the redemption time, the first mandatory time, down to 0 through five grid times
                    t0 = times[0]
                    size = H.call("TimeGrid::size", lattice["timeGrid"])
                    points = [H.call("TimeGrid::at", lattice["timeGrid"], i) for i in range(size)]
                    last = points.index(t0)
                    down = sorted({points[last * 3 // 4], points[last // 2], points[last // 4], points[2], points[1],
                                   0.0}, reverse=True)
                    rolled = H.call("DiscretizedCallableFixedRateBond::rollback", spec, curve, spread, lattice, t0,
                                    down)
                    pv = H.call("DiscretizedCallableFixedRateBond::presentValue", spec, curve, spread, lattice, t0,
                                down)
                    out.append({"name": f"{name} / spread {spread:g}",
                                "inputs": {"settings": s, "bond": spec, "curve": curve, "spread": spread,
                                           "lattice": lattice, "t0": t0, "times": down},
                                "expected": {"mandatoryTimes": times, "rollbackTimes": rolled["times"],
                                             "rollbackValues": rolled["values"], "presentValue": pv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/discretizedcallablefixedratebond.hpp", cases)
