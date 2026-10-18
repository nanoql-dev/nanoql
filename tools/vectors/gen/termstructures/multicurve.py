# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiCurve (ql/termstructures/multicurve.hpp) and the multicurve bootstrap of
ql/termstructures/globalbootstrap.hpp: cycles of curves solved together, 10 bootstrapped curves in 5 cases, and
QuantLib's refusals in 4 cases.

- A 3M/6M cycle on Monday 15 January 2024 and on Thursday 29 February 2024: a 3M curve on a Euribor 3M deposit and 5
  swaps on Euribor 3M discounted on the 6M curve's member, and a 6M curve on a Euribor 6M deposit and 5 swaps on
  Euribor 6M discounted on the 3M curve's member, both log-linear discount curves with a fixed reference date two
  TARGET days after the evaluation date.
- The same cycle on 15 January 2024 in the settlement-days forms: each member's reference date and each curve's
  computed from 2 TARGET settlement days, QuantLib's moving curves.
- A 1M/3M/6M cycle, a linear zero curve, a log-linear discount curve and a backward-flat forward curve, each curve's
  swaps discounted on the next curve's member.
- A non-bootstrapped member: a flat forward curve (3%, continuous, Actual/365 (Fixed)) added as it is, on whose member
  a 6M curve's swaps are discounted.
- QuantLib's refusals: a member added twice to one multicurve; a member another multicurve has linked; a null curve;
  and the 3M/6M cycle under end criteria of 3 iterations, which fail to minimize ("... (during multi curve
  bootstrap): MaxIterations").

A cycle's outputs are whether it builds, QuantLib's refusal recorded as its throw, and for each member, in add order:
its curve's nodes' dates and data (none for a non-bootstrapped member), its maximum date, its discounts at 40 dates
read through its member (35 from its reference date to its maximum date, 5 past it, extrapolated), and each of its
helpers' quote errors, read from the helper QuantLib has bound to the curve. A refusal case's output is each step's
result: true, or QuantLib's refusal recorded as its throw.

Each case's inputs hold its settings and "kind". A cycle ("cycle") has the multicurve's "accuracy" or its "optimizer"
and "endCriteria" (as termstructures/globalbootstrap.py writes them, null for the defaults), "members" in add order,
"discountDates" and "extrapolate" (one list per member). A member is {"name", "handle", "dayCounter", "curve"} with
"helpers" for a bootstrapped one: the handle {"form": "referenceDate", "referenceDate"} or {"form":
"settlementDays", "settlementDays", "calendar"}; the curve a curve spec of termstructures/yield/piecewiseyieldcurve.py,
built with QuantLib's default GlobalBootstrap, or {"type": "FlatForward", ...} (a flat forward spec) for a
non-bootstrapped one; and the helper specs of termstructures/yield/ratehelpers.py, whose discounting curve may be
{"handle": <a member's name>}. A refusal case ("steps") has "members" (handles and curves, as above, a null curve
for QuantLib's) and "steps", each {"op": "addBootstrappedCurve" | "addNonBootstrappedCurve", "multiCurve": its
index, "member": a member's name, "curve": the curve's member}, a multicurve linking its members when a later step
uses another (QuantLib links them on adding). Read back by tests/NanoQL.Tests/Vectors/TermStructures/
MultiCurveVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)
rh = pw.rh
irx = pw.irx

FIRST, SECOND = pw.EVALUATION_DATES[0], pw.EVALUATION_DATES[1]
A365 = pw.A365
ACCURACY = 1e-10

# The wheel's global class for each traits and interpolator type.
WHEEL = {
    ("Discount", "LogLinear"): ql.GlobalPiecewiseLogLinearDiscount,
    ("ZeroYield", "Linear"): ql.GlobalPiecewiseLinearZero,
    ("ForwardRate", "BackwardFlat"): ql.GlobalPiecewiseFlatForward,
}

# Each index's deposit and swap quotes.
QUOTES = {
    "1M": (0.0390, [(1, 0.0380), (2, 0.0355), (3, 0.0335), (5, 0.0325), (10, 0.0315)]),
    "3M": (0.0385, [(1, 0.0375), (2, 0.0350), (3, 0.0330), (5, 0.0320), (10, 0.0310)]),
    "6M": (0.0390, [(1, 0.0385), (2, 0.0360), (3, 0.0340), (5, 0.0330), (10, 0.0320)]),
}
INDEXES = {"1M": rh.E1M, "3M": rh.E3M, "6M": rh.E6M}


def helpers(index, discount):
    """A deposit on the index and 5 swaps on it, discounted on the named member."""
    deposit, swaps = QUOTES[index]
    out = [rh.deposit_index(deposit, INDEXES[index])]
    out += [rh.swap_tenor(rate, (n, ql.Years), index=INDEXES[index], discount={"handle": discount})
            for n, rate in swaps]
    return out


def handle_spec(today, moving):
    if moving:
        return {"form": "settlementDays", "settlementDays": 2, "calendar": rh.TARGET}
    return {"form": "referenceDate", "referenceDate": v.enc(ql.TARGET().advance(today, 2, ql.Days))}


def bootstrapped(name, traits, interpolator, today, discount, moving=False):
    return {"name": name, "handle": handle_spec(today, moving), "dayCounter": A365,
            "curve": pw.curve_spec(traits, interpolator, A365, today, moving), "helpers": helpers(name, discount)}


def flat(name, today, rate=0.03):
    reference = ql.TARGET().advance(today, 2, ql.Days)
    return {"name": name, "handle": handle_spec(today, False), "dayCounter": A365,
            "curve": dict({"type": "FlatForward"}, **irx.curve_spec(reference, rate, A365))}


def make_helper(spec, handles):
    """The wheel's helper for a spec, its discounting curve a member's relinkable handle when it names one."""
    d = spec.get("discountingCurve")
    if not (isinstance(d, dict) and "handle" in d):
        return rh.make_helper(spec)
    assert spec["kind"] == "SwapRateHelper" and spec["form"] == "tenor" and spec["spread"] == 0.0
    days = v.NULL_INTEGER if spec["settlementDays"] is None else spec["settlementDays"]
    return ql.SwapRateHelper(spec["rate"], ql.Period(*spec["tenor"]), irx.calendar(spec["calendar"]),
                             spec["fixedFrequency"], spec["fixedConvention"], irx.day_counter(spec["fixedDayCount"]),
                             rh.fvf.make_index(spec["index"]), ql.QuoteHandle(), ql.Period(*spec["fwdStart"]),
                             handles[d["handle"]], days, spec["pillar"], ql.Date(), spec["endOfMonth"],
                             spec["useIndexedCoupons"], spec["floatConvention"])


def make_curve(member, handles):
    """The member's curve, and its helpers (none for a non-bootstrapped one)."""
    c = member["curve"]
    if c is None:
        return None, []
    if c.get("type") == "FlatForward":
        return ql.FlatForward(ql.Date(c["referenceDate"]), c["forward"], irx.day_counter(c["dayCounter"]),
                              c["compounding"], c["frequency"]), []
    hs = [make_helper(h, handles) for h in member["helpers"]]
    cls = WHEEL[(c["traits"], c["interpolator"]["type"])]
    if c["form"] == "referenceDate":
        head = (ql.Date(c["referenceDate"]),)
    else:
        head = (c["settlementDays"], irx.calendar(c["calendar"]))
    return cls(*head, hs, irx.day_counter(c["dayCounter"]), ql.GlobalBootstrap(), [], [],
               pw.make_interpolator(c["traits"], c["interpolator"])), hs


def discount_dates(curve, bootstrapped_member):
    """A bootstrapped curve's discount dates (termstructures/yield/piecewiseyieldcurve.py's); 40 dates over 50 years
    from its reference date for another curve, whose maximum date is the latest date."""
    if bootstrapped_member:
        return pw.discount_dates(curve)
    reference = curve.referenceDate()
    return [reference + int(round(18262 * (k / 39.0) ** 2)) for k in range(40)], [False] * 40


def make_multicurve(spec):
    if spec.get("accuracy") is not None:
        return ql.MultiCurve(spec["accuracy"])
    o, e = spec["optimizer"], spec["endCriteria"]
    optimizer = None if o is None else ql.LevenbergMarquardt(o["epsfcn"], o["xtol"], o["gtol"])
    end_criteria = None if e is None else ql.EndCriteria(e["maxIterations"], e["maxStationaryStateIterations"],
                                                        e["rootEpsilon"], e["functionEpsilon"],
                                                        e["gradientNormEpsilon"])
    return ql.MultiCurve(optimizer, end_criteria)


def cycle_case(name, today, members, multicurve=None):
    multicurve = multicurve or {"accuracy": ACCURACY}
    with v.case_settings(today) as st:
        handles = {m["name"]: ql.RelinkableYieldTermStructureHandle() for m in members}
        built = [make_curve(m, handles) for m in members]
        mc = make_multicurve(multicurve)
        external = []
        for m, (curve, _) in zip(members, built):
            add = mc.addBootstrappedCurve if "traits" in m["curve"] else mc.addNonBootstrappedCurve
            external.append(add(handles[m["name"]], curve))
        keys = ["dates", "data", "maxDate", "discounts", "quoteErrors"]
        expected = {"built": True}
        expected.update({k: [] for k in keys})
        dates_in, flags_in = [], []
        try:
            for curve, _ in built:
                curve.maxDate()
        except RuntimeError:
            expected = {"built": dict(v.THROWS)}
        else:
            for m, (curve, hs), h in zip(members, built, external):
                bootstrapped_member = "traits" in m["curve"]
                dates, flags = discount_dates(curve, bootstrapped_member)
                expected["dates"].append(v.enc(list(curve.dates())) if bootstrapped_member else [])
                expected["data"].append(list(curve.data()) if bootstrapped_member else [])
                expected["maxDate"].append(v.enc(h.maxDate()))
                expected["discounts"].append([h.discount(d, x) for d, x in zip(dates, flags)])
                expected["quoteErrors"].append([x.quoteError() for x in hs])
                dates_in.append(v.enc(dates))
                flags_in.append(flags)
        inputs = {"settings": st, "kind": "cycle"}
        inputs.update(multicurve)
        inputs.update({"members": members, "discountDates": dates_in, "extrapolate": flags_in})
        return {"name": f"{today.ISO()} {name}", "inputs": inputs, "expected": expected}


def cycle_cases():
    LL, LZ, BF = pw.LOG_LINEAR, pw.LINEAR, pw.BACKWARD_FLAT
    out = []
    for today in (FIRST, SECOND):
        out.append(cycle_case("3M/6M cycle", today, [bootstrapped("3M", "Discount", LL, today, "6M"),
                                                     bootstrapped("6M", "Discount", LL, today, "3M")]))
    out.append(cycle_case("3M/6M cycle, settlement-days forms", FIRST,
                          [bootstrapped("3M", "Discount", LL, FIRST, "6M", moving=True),
                           bootstrapped("6M", "Discount", LL, FIRST, "3M", moving=True)]))
    out.append(cycle_case("1M/3M/6M cycle", FIRST, [bootstrapped("1M", "ZeroYield", LZ, FIRST, "3M"),
                                                    bootstrapped("3M", "Discount", LL, FIRST, "6M"),
                                                    bootstrapped("6M", "ForwardRate", BF, FIRST, "1M")]))
    out.append(cycle_case("non-bootstrapped member", FIRST, [flat("flat", FIRST),
                                                              bootstrapped("6M", "Discount", LL, FIRST, "flat")]))
    out.append(cycle_case("3M/6M cycle, 3 iterations", FIRST,
                          [bootstrapped("3M", "Discount", LL, FIRST, "6M"),
                           bootstrapped("6M", "Discount", LL, FIRST, "3M")],
                          {"accuracy": None, "optimizer": None,
                           "endCriteria": {"maxIterations": 3, "maxStationaryStateIterations": 2,
                                           "rootEpsilon": 1e-15, "functionEpsilon": 1e-15,
                                           "gradientNormEpsilon": 1e-15}}))
    return out


def refusal_case(name, members, steps):
    """The steps' results, each multicurve's members linked as QuantLib links them, on adding."""
    with v.case_settings(FIRST) as st:
        handles = {m["name"]: ql.RelinkableYieldTermStructureHandle() for m in members}
        curves = {m["name"]: make_curve(m, handles)[0] for m in members}
        multicurves = {}
        results = []
        for step in steps:
            mc = multicurves.setdefault(step["multiCurve"], ql.MultiCurve(ACCURACY))
            add = getattr(mc, step["op"])
            results.append(v.attempt(lambda a=add, s=step: (a(handles[s["member"]], curves[s["curve"]]), True)[1]))
        return {"name": f"{FIRST.ISO()} {name}", "inputs": {"settings": st, "kind": "steps", "members": members,
                                                           "steps": steps},
                "expected": {"results": results}}


def step(op, multicurve, member, curve):
    return {"op": op, "multiCurve": multicurve, "member": member, "curve": curve}


def refusal_cases():
    NB = "addNonBootstrappedCurve"
    flats = [flat("A", FIRST), flat("B", FIRST, 0.04)]
    null = dict(flat("null", FIRST), curve=None)
    return [
        refusal_case("a member added twice", flats, [step(NB, 0, "A", "A"), step(NB, 0, "A", "B")]),
        refusal_case("a member another multicurve has linked", flats,
                     [step(NB, 0, "A", "A"), step(NB, 1, "B", "B"), step(NB, 1, "A", "A")]),
        refusal_case("a null curve", flats[:1] + [null], [step(NB, 0, "A", "null")]),
    ]


def cases():
    out = cycle_cases() + refusal_cases()
    check_rows(out)
    return out


def check_rows(out):
    cycles = [c for c in out if c["inputs"]["kind"] == "cycle"]
    assert sum(len(c["expected"].get("data", [])) - sum(1 for d in c["expected"].get("data", []) if not d)
               for c in cycles) == 10
    assert [c["name"] for c in cycles if c["expected"]["built"] is not True] == [f"{FIRST.ISO()} 3M/6M cycle, 3 iterations"]
    # every bootstrapped curve reprices its helpers, its errors those of a joint least-squares optimum
    for c in cycles:
        for errors in c["expected"].get("quoteErrors", []):
            assert all(abs(e) < 1e-12 for e in errors), (c["name"], errors)
    results = {c["name"][11:]: c["expected"]["results"] for c in out if c["inputs"]["kind"] == "steps"}
    assert results == {"a member added twice": [True, v.THROWS],
                       "a member another multicurve has linked": [True, True, v.THROWS],
                       "a null curve": [v.THROWS]}, results


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/multicurve.hpp", cases)
