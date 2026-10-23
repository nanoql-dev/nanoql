# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ZabrSmileSection (ql/termstructures/volatility/zabrsmilesection.hpp) with its four
kernels, through the wheel's named classes ZabrShortMaturityLognormalSmileSection, ZabrShortMaturityNormalSmileSection,
ZabrLocalVolatilitySmileSection and ZabrFullFdSmileSection (QuantLib-Python's %template names), and the SmileSection
interface they inherit: for each short-maturity kernel and the local-volatility one 21 sections, 16 by exercise time
and 5 by date (QuantLib's floating section, whose reference date is
the evaluation date, which nanoql builds by Create from the context), over ZABR parameters with γ 1 (the model's
closed form) and γ away from 1 (its Runge-Kutta integration), β 0 and 1, ν 0, ρ near ±1, expiries from 0.1 to 30
years, a sixth parameter (ignored) and a moneyness grid and refinement (which the short-maturity kernels don't read);
for the full finite-difference kernel, which solves a two-dimensional equation per strike of its grid, two of them, one
by time and one by date, on the moneyness grid 0.25, 0.5, 0.8, 1, 1.25, 2 and 5 with one point between two.

**The full finite-difference kernel's values are left out by the stability filter** (the S7 plan's S7R6): each of its
sections is rebuilt with its exercise time (a date stays), forward and five parameters moved one ulp up, then down,
every one together, then each alone, and a strike is kept only where every output at it stays within 1e-12 x
max(|base|, |moved|, 1); none is, and the generator prints the outputs dropped. The kernel's damping steps solve by
BiCGStab to a relative 1e-8, so a last-bit difference moves its prices by up to that: .NET's pow and the C runtime's,
one ulp apart at a forward node (§3.3's known class), part the two solves' iterations and move a call price by 5e-9.
QuantLib's testConsistency checks the kernel at its tolerances (tests/NanoQL.Tests/TestSuite/ZabrTests.cs).

- "section" cases, one per section: its exerciseDate (null for a section by time), referenceDate (refused by time),
  exerciseTime, dayCounter (its name, null for QuantLib's empty one), volatilityType, shift, minStrike, maxStrike and
  atmLevel; and at 12 strikes around the forward (a negative one and 0 among them) the variance, volatility, call and
  put prices and digital call price with the case's discount, vega, density, and the volatility converted to a normal
  one. A refused value is {"throws": true}: a lognormal section's Black price at a negative strike, and every value
  of a section whose model gives NaN; the volatility of the normal and the finite-difference kernels is 0 where
  QuantLib can't imply it.
- "refusal" cases: the sections QuantLib refuses to build, for each kernel: a zero or negative exercise time, a
  negative forward, each parameter out of its range, and an exercise date before the evaluation date; and the full
  finite-difference kernel's ν 0, whose volatility mesher has no width.

Each case's input "section" is {"kernel" ("ShortMaturityLognormal", "ShortMaturityNormal", "LocalVolatility" or
"FullFd"), "form" ("time" or
"floating"), "exerciseTime" or "exerciseDate", "forward", "parameters", "dayCounter" (the floating form's, as in
tools/vectors/gen/indexes/interestrateindex.py), "moneyness", "fdRefinement"}. The wheel's classes are the proxies of
the C++ constructors (volatilities.i), positional only. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/ZabrSmileSectionVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
KERNELS = {"ShortMaturityLognormal": ql.ZabrShortMaturityLognormalSmileSection,
           "ShortMaturityNormal": ql.ZabrShortMaturityNormalSmileSection,
           "LocalVolatility": ql.ZabrLocalVolatilitySmileSection,
           "FullFd": ql.ZabrFullFdSmileSection}
TODAY = ql.Date(15, ql.January, 2025)


def time_spec(kernel, time, forward, parameters, moneyness=(), fd_refinement=5):
    return {"kernel": kernel, "form": "time", "exerciseTime": time, "forward": forward, "parameters": list(parameters),
            "moneyness": list(moneyness), "fdRefinement": fd_refinement}


def floating_spec(kernel, exercise, forward, parameters, dc, moneyness=(), fd_refinement=5):
    return {"kernel": kernel, "form": "floating", "exerciseDate": v.enc(exercise), "forward": forward,
            "parameters": list(parameters), "dayCounter": dc, "moneyness": list(moneyness),
            "fdRefinement": fd_refinement}


def make_section(spec):
    """The section a spec describes, built with the wheel under the evaluation date the caller has set."""
    cls = KERNELS[spec["kernel"]]
    rest = (spec["moneyness"], spec["fdRefinement"])
    if spec["form"] == "time":
        return cls(spec["exerciseTime"], spec["forward"], spec["parameters"], *rest)
    return cls(ql.Date(spec["exerciseDate"]), spec["forward"], spec["parameters"],
               irx.day_counter(spec["dayCounter"]), *rest)


def strikes(forward):
    """12 strikes around the forward: a negative one, 0 and one just above it among them."""
    f = forward
    return [-0.01, 0.0, 1e-7, 0.1 * f, 0.5 * f, 0.8 * f, 0.95 * f, f, 1.05 * f, 1.25 * f, 2.0 * f, 5.0 * f]


# (exercise time or date, forward, parameters alpha, beta, nu, rho, gamma[, extra]); a date makes a floating section
SECTIONS = [
    (5.0, 0.03, (0.08, 0.7, 0.2, -0.3, 1.0)),
    (1.0, 0.03, (0.08, 0.7, 0.2, -0.3, 0.75)),
    (2.0, 0.025, (0.03, 0.5, 0.4, 0.2, 0.5)),
    (10.0, 0.04, (0.1, 0.8, 0.5, -0.5, 1.5)),
    (1.0, 0.03, (0.25, 1.0, 0.3, -0.2, 1.0)),
    (1.0, 0.03, (0.25, 1.0, 0.3, -0.2, 0.8)),
    (3.0, 0.02, (0.008, 0.0, 0.3, 0.1, 1.0)),
    (3.0, 0.02, (0.008, 0.0, 0.3, 0.1, 0.0)),
    (0.5, 0.05, (0.05, 0.5, 0.0, 0.0, 1.0)),
    (2.0, 0.035, (0.05, 0.6, 0.6, 0.95, 1.0)),
    (2.0, 0.035, (0.05, 0.6, 0.6, -0.95, 1.2)),
    (0.1, 0.03, (0.04, 0.5, 0.8, -0.4, 1.0)),
    (30.0, 0.045, (0.02, 0.4, 0.25, 0.3, 0.9)),
    (1.0, 0.03, (0.08, 0.7, 0.2, -0.3, 1.0, 0.123)),
    (4.0, 0.01, (0.02, 0.3, 0.7, -0.6, 1.1)),
    (1.5, 0.06, (0.3, 0.9, 1.5, 0.0, 0.3)),
    (ql.Date(15, ql.January, 2030), 0.03, (0.08, 0.7, 0.2, -0.3, 1.0)),
    (ql.Date(17, ql.March, 2026), 0.025, (0.03, 0.5, 0.4, 0.2, 0.5)),
    (ql.Date(15, ql.January, 2035), 0.04, (0.1, 0.8, 0.5, -0.5, 1.5)),
    (ql.Date(15, ql.July, 2025), 0.03, (0.25, 1.0, 0.3, -0.2, 0.8)),
    (ql.Date(31, ql.December, 2027), 0.02, (0.008, 0.0, 0.3, 0.1, 1.0)),
]
FLOATING_DAY_COUNTERS = [A365, A360, THIRTY360_BB, A365, A360]
# The full finite-difference kernel's sections, a two-dimensional solve per strike, on a coarser grid: one by time and
# one by date, for the class's inspectors; its values fail the stability filter (below), and ν 0 is refused
FULL_FD_SECTIONS = [0, 16]
FULL_FD_MONEYNESS = (0.25, 0.5, 0.8, 1.0, 1.25, 2.0, 5.0)
DISCOUNTS = [1.0, 0.95]
# (exercise time or date, forward, parameters)
REFUSALS = [
    (0.0, 0.03, (0.08, 0.7, 0.2, -0.3, 1.0)),
    (-1.0, 0.03, (0.08, 0.7, 0.2, -0.3, 1.0)),
    (1.0, -0.01, (0.08, 0.7, 0.2, -0.3, 1.0)),
    (1.0, 0.03, (0.0, 0.7, 0.2, -0.3, 1.0)),
    (1.0, 0.03, (0.08, 1.5, 0.2, -0.3, 1.0)),
    (1.0, 0.03, (0.08, 0.7, -0.1, -0.3, 1.0)),
    (1.0, 0.03, (0.08, 0.7, 0.2, 1.0, 1.0)),
    (1.0, 0.03, (0.08, 0.7, 0.2, -0.3, -0.5)),
    (ql.Date(14, ql.January, 2025), 0.03, (0.08, 0.7, 0.2, -0.3, 1.0)),
]
# The full finite-difference kernel's own refusal: ν 0, whose volatility mesher has no width
FULL_FD_REFUSALS = [(0.5, 0.05, (0.05, 0.5, 0.0, 0.0, 1.0))]


def spec_of(kernel, k, when, forward, parameters):
    if isinstance(when, ql.Date):
        dc = FLOATING_DAY_COUNTERS[k % len(FLOATING_DAY_COUNTERS)]
        if kernel == "FullFd":
            return floating_spec(kernel, when, forward, parameters, dc, FULL_FD_MONEYNESS, 1)
        return floating_spec(kernel, when, forward, parameters, dc)
    if k == 13:
        return time_spec(kernel, when, forward, parameters, (0.5, 1.0, 2.0), 3)
    if kernel == "FullFd":
        return time_spec(kernel, when, forward, parameters, FULL_FD_MONEYNESS, 1)
    return time_spec(kernel, when, forward, parameters)


STRIKE_OUTPUTS = ["variance", "volatility", "callPrice", "putPrice", "digitalCallPrice", "vega", "density",
                  "normalVolatility"]
RELATIVE = 1e-12


def strike_outputs(s, ks, discount):
    """The outputs at each strike, as lists over the strikes."""
    def at_strikes(f):
        return [v.attempt(lambda x=x: f(x)) for x in ks]

    return {"variance": at_strikes(s.variance),
            "volatility": at_strikes(s.volatility),
            "callPrice": at_strikes(lambda x: s.optionPrice(x, ql.Option.Call, discount)),
            "putPrice": at_strikes(lambda x: s.optionPrice(x, ql.Option.Put, discount)),
            "digitalCallPrice": at_strikes(lambda x: s.digitalOptionPrice(x, ql.Option.Call, discount)),
            "vega": at_strikes(lambda x: s.vega(x, discount)),
            "density": at_strikes(lambda x: s.density(x, discount)),
            "normalVolatility": at_strikes(lambda x: s.volatility(x, ql.Normal, 0.0))}


def moved_specs(spec):
    """The spec with its exercise time (a date stays), forward and five parameters moved one ulp up, then down: every
    one together first, then each alone."""
    def move(x, d):
        return math.nextafter(x, d)

    keys = (["exerciseTime"] if spec["form"] == "time" else []) + ["forward"]
    together, alone = [], []
    for d in (math.inf, -math.inf):
        m = dict(spec)
        for key in keys:
            m[key] = move(spec[key], d)
        m["parameters"] = [move(x, d) for x in spec["parameters"][:5]] + spec["parameters"][5:]
        together.append(m)
        for key in keys:
            alone.append({**spec, key: move(spec[key], d)})
        for i in range(5):
            ps = list(spec["parameters"])
            ps[i] = move(ps[i], d)
            alone.append({**spec, "parameters": ps})
    return together + alone


def same(a, b):
    if isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return a == b or abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def stable_strikes(spec, ks, discount, base):
    """The strikes at which every refit of a moved spec leaves every output stable (the S7 plan's S7R6); a moved spec
    the model refuses (a parameter moved out of its range) is no refit."""
    keep = list(range(len(ks)))
    for m in moved_specs(spec):
        if not keep:
            break
        try:
            moved = strike_outputs(make_section(m), ks, discount)
        except RuntimeError:
            continue
        keep = [j for j in keep if all(same(base[o][j], moved[o][j]) for o in STRIKE_OUTPUTS)]
    return [ks[j] for j in keep]


def section_case(k, spec):
    st = v.settings(TODAY)
    s = make_section(spec)
    ks = strikes(spec["forward"])
    discount = DISCOUNTS[k % 2]
    if spec["kernel"] == "FullFd":
        all_ks = ks
        ks = stable_strikes(spec, ks, discount, strike_outputs(s, ks, discount))
        print(f"FullFd section {k}: dropped {len(all_ks) - len(ks)} of {len(all_ks)} strikes, "
              f"{8 * (len(all_ks) - len(ks))} of {8 * len(all_ks)} outputs")
    dc = s.dayCounter()
    return {"name": f"{spec['kernel']} {spec['form']} {spec['parameters']} / {k}",
            "inputs": {"settings": st, "kind": "section", "section": v.enc(spec), "strikes": ks, "discount": discount},
            "expected": {
                "exerciseDate": v.enc(s.exerciseDate()),
                "referenceDate": v.attempt(lambda: s.referenceDate()),
                "exerciseTime": s.exerciseTime(),
                "dayCounter": None if dc.empty() else dc.name(),
                "volatilityType": int(s.volatilityType()),
                "shift": s.shift(),
                "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(),
                "atmLevel": v.enc(s.atmLevel()),
                **strike_outputs(s, ks, discount)}}


def refusal_case(k, spec):
    st = v.settings(TODAY)
    return {"name": f"refusal {spec['kernel']} {spec['form']} / {k}",
            "inputs": {"settings": st, "kind": "refusal", "section": v.enc(spec)},
            "expected": {"section": v.attempt(lambda: make_section(spec).exerciseTime())}}


def cases():
    out = []
    for kernel in KERNELS:
        for k, (when, forward, parameters) in enumerate(SECTIONS):
            if kernel != "FullFd" or k in FULL_FD_SECTIONS:
                out.append(section_case(k, spec_of(kernel, k, when, forward, parameters)))
    for kernel in KERNELS:
        refusals = REFUSALS + (FULL_FD_REFUSALS if kernel == "FullFd" else [])
        for k, (when, forward, parameters) in enumerate(refusals):
            out.append(refusal_case(k, spec_of(kernel, k, when, forward, parameters)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/zabrsmilesection.hpp", cases)
