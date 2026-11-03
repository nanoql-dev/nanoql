# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticDoubleBarrierBinaryEngine
(ql/pricingengines/barrier/analyticdoublebarrierbinaryengine.hpp, .cpp), on the evaluation date 15 October 2026:
double-barrier binary options by Hui's series, cash-or-nothing knock-outs and knock-ins paid at expiry and the KIKO and
KOKI types paid at the hit.

Three corridors around the spot (80-120%, 90-115% and 70-140% of it), over six months, a year and two years, on the
seven processes of tools/vectors/gen/processes/blackscholesprocess.py off the variance surface (a strike-0 payoff reads
the volatility at strike 0, where the surface extrapolates): each knock-out a cash-or-nothing call (cash 10) at strike
0 and at half the lower barrier and a put at one and a half times the upper; and each knock-in the strike-0 call: the
NPV, the engine's one result off its degenerate branches. On these processes Abel's bound on QuantLib's KIKO and KOKI
sums is 1.6e-11 to 7.5e-4 of the sums, so the port takes the flux route for every one; the KIKO and KOKI, strike-0
calls on an American exercise, are on flat markets where QuantLib's sums converge, near beta = 0: the risk-free rate 0
or 0.2%, the carry sigma^2/2 or 1% below it, sigma of 20% and 50%, over six months and a year, the spot at 97 and 103
in the corridors [95, 105], [90, 110] and [80, 120]. Then the
degenerate branches, where QuantLib also sets the Greeks: a knock-out, KIKO and KOKI with the spot on or beyond a
barrier: the NPV, delta, gamma, vega and rho. Then an expired option and the refusals: a European KIKO, an American
knock-out, a plain-vanilla payoff and barriers out of order.

The outputs nanoql fixes are left out: every process here has one day counter and reference date
(tests/NanoQL.Tests/Divergences/BarrierClosedFormMismatchedTimesTests.cs); every rebate is 0, which QuantLib's engine
never reads (DoubleBinaryRebateTests.cs); no knock-in is already in, where QuantLib pays the cash undiscounted
(DoubleBinaryKnockedInTests.cs); every knock-out pays over the whole corridor (a call struck at or below L, a put at or
above U) and every knock-in is QuantLib's strike-0 call, where QuantLib's value stands (DoubleBinaryStrikeTests.cs); and
every value is a decade inside the port's switches, by their replicas below: Hui's series converged, its sine-free tail
bound within 1e-12 of its sum (the port takes it within 1e-11; DoubleBinaryHuiCheckTests.cs; where QuantLib refuses,
DoubleBinarySmallVarianceTests.cs), and the KIKO and KOKI sums under Abel's bound within 1e-12 of theirs (1e-11;
DoubleBinaryKikoTests.cs); a case whose value is outside either is left out.

A case holds one process, exercise, double barrier and payoff, the specs
tools/vectors/gen/pricingengines/barrier/analyticdoublebarrierengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's
options.i: DoubleBarrierOption(barrierType, barrierLo, barrierHi, rebate, payoff, exercise),
AnalyticDoubleBarrierBinaryEngine(process) and the option's NPV and Greeks. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/AnalyticDoubleBarrierBinaryEngineVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


dbe = _load("analyticdoublebarrierengine", "pricingengines/barrier/analyticdoublebarrierengine.py")
abe, bsp, bc = dbe.abe, dbe.bsp, dbe.bc
R = dbe.R
CALL, PUT = dbe.CALL, dbe.PUT
KNOCK_IN, KNOCK_OUT, KIKO, KOKI = dbe.KNOCK_IN, dbe.KNOCK_OUT, dbe.KIKO, dbe.KOKI
PROCESSES = [(n, p) for n, p in bsp.PROCESSES if "surface" not in n]
GREEKS = ["delta", "gamma", "vega", "rho"]
MARGIN = 1e-12
EPS = 2.220446049250313e-16


def cash(t, k):
    return {"type": "CashOrNothing", "optionType": t, "strike": k, "cashPayoff": 10.0}


def market(p, last, strike):
    """The port's market: the volatility's time and variance, and the zero rates over it."""
    t = p.blackVolatility().timeFromReference(last)
    variance = p.blackVolatility().blackVariance(last, strike)
    r = p.riskFreeRate().zeroRate(t, ql.Continuous, ql.NoFrequency).rate()
    q = p.dividendYield().zeroRate(t, ql.Continuous, ql.NoFrequency).rate()
    return t, variance, r, q


def envelope_tail(term, first, peak):
    total = 0.0
    for i in range(first, first + 100000):
        x = term(i)
        if not math.isfinite(x):
            return math.inf
        total += x
        if i > peak and x <= EPS * total:
            return total
    return math.inf


def hui_ratio(spot, lo, hi, t, variance, r, q, cash_payoff):
    """Hui's knock-out sum over the whole corridor, the port's HuiFull: its envelope tail over its sum."""
    sigmaq = variance / t
    z = math.log(hi / lo)
    alpha = -0.5 * (2 * (r - q) / sigmaq - 1)
    beta = -0.25 * (2 * (r - q) / sigmaq - 1) ** 2 - 2 * r / sigmaq
    factor = 2 * math.pi * cash_payoff / z ** 2
    lo_a, hi_a = (spot / lo) ** alpha, (spot / hi) ** alpha
    tot = 0.0
    for i in range(1, 100):
        w = i * math.pi / z
        tot += (factor * i * (lo_a - (-1.0) ** i * hi_a) / (alpha ** 2 + w ** 2) * math.sin(w * math.log(spot / lo))
                * math.exp(-0.5 * (w ** 2 - beta) * variance))

    def tail(i):
        w = i * math.pi / z
        return factor * i * (lo_a + hi_a) / (alpha * alpha + w * w) * math.exp(-0.5 * (w * w - beta) * variance)

    return envelope_tail(tail, 100, abs(alpha) * z / math.pi) / abs(tot) if tot != 0 else math.inf


def kiko_ratio(spot, lo, hi, kind, t, variance, r, q):
    """QuantLib's KIKO and KOKI sum, the port's QuantLibKiko: Abel's bound and the Gaussian tail over its sum."""
    if kind == KOKI:
        lo, hi = hi, lo
    sigmaq = variance / t
    alpha = -0.5 * (2 * (r - q) / sigmaq - 1)
    beta = -0.25 * (2 * (r - q) / sigmaq - 1) ** 2 - 2 * r / sigmaq
    z = math.log(hi / lo)
    log_sl = math.log(spot / lo)
    tot = 0.0
    for i in range(1, 1000):
        f = (i * math.pi / z) ** 2 - beta
        tot += 2.0 / (i * math.pi) * (beta - (i * math.pi / z) ** 2 * math.exp(-0.5 * f * variance)) / f * math.sin(
            i * math.pi / z * log_sl)
    tot += 1 - log_sl / z
    theta = math.pi * log_sl / z
    w_n = 1000 * math.pi / z
    if not w_n * w_n > 2.0 * beta:
        return math.inf
    abel = abs(2.0 / (1000 * math.pi) * beta / (w_n * w_n - beta)) / abs(math.sin(theta / 2.0))

    def gaussian(i):
        w = i * math.pi / z
        return 2.0 / (i * math.pi) * w * w * math.exp(-0.5 * (w * w - beta) * variance) / abs(w * w - beta)

    return (abel + envelope_tail(gaussian, 1000, 0.0)) / abs(tot)


def american(days):
    return {"type": "American", "date": (R + days).serialNumber()}


def exercise(spec):
    date = ql.Date(spec["date"])
    if spec["type"] == "European":
        return ql.EuropeanExercise(date)
    earliest = ql.Date(spec["earliestDate"]) if "earliestDate" in spec else R
    return ql.AmericanExercise(earliest, date)


def option(barrier, payoff, ex):
    return ql.DoubleBarrierOption(barrier["barrierType"], barrier["barrierLo"], barrier["barrierHi"],
                                  barrier["rebate"], bc.payoff(payoff), exercise(ex))


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, payoff, greeks=False):
        o = option(barrier, payoff, ex)
        o.setPricingEngine(ql.AnalyticDoubleBarrierBinaryEngine(bsp.process(pspec)))
        expected = {"NPV": v.attempt(o.NPV)}
        if greeks:
            for g in GREEKS:
                expected[g] = v.result(getattr(o, g))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier,
                               "payoff": payoff},
                    "expected": expected})

    for pname, pspec in PROCESSES:
        x0 = pspec["x0"]
        p = bsp.process(pspec)
        for days in abe.EXPIRIES:
            last = R + days
            for flo, fhi in dbe.CORRIDORS:
                lo, hi = x0 * flo, x0 * fhi
                corridor = f"[{flo:g}, {fhi:g}] x spot"
                setups = [("knock-out, a strike-0 call", KNOCK_OUT, abe.exercise_spec("European", days), cash(CALL, 0.0)),
                          ("knock-out, a call below L", KNOCK_OUT, abe.exercise_spec("European", days),
                           cash(CALL, 0.5 * lo)),
                          ("knock-out, a put above U", KNOCK_OUT, abe.exercise_spec("European", days),
                           cash(PUT, 1.5 * hi)),
                          ("knock-in, a strike-0 call", KNOCK_IN, abe.exercise_spec("European", days), cash(CALL, 0.0))]
                for sname, kind, ex, payoff in setups:
                    t, variance, r, q = market(p, last, payoff["strike"])
                    if hui_ratio(x0, lo, hi, t, variance, r, q, payoff["cashPayoff"]) <= MARGIN:
                        add(f"{pname}, {days} days, {corridor}, {sname}", pspec, ex, dbe.barrier_spec(kind, lo, hi),
                            payoff)
    for sigma in (0.2, 0.5):
        for rate in (0.0, 0.002):
            for below in (0.0, 0.01):
                for spot in (97.0, 103.0):
                    pspec = bsp.process_spec("BlackScholesMertonProcess", spot, bsp.flat_spec(R, rate),
                                             bsp.bvts.vol_spec_constant(R, sigma),
                                             bsp.flat_spec(R, rate - sigma * sigma / 2 + below))
                    p = bsp.process(pspec)
                    for days in (182, 365):
                        t, variance, r, q = market(p, R + days, 0.0)
                        for lo, hi in ((95.0, 105.0), (90.0, 110.0), (80.0, 120.0)):
                            for kname, kind in (("KIKO", KIKO), ("KOKI", KOKI)):
                                if kiko_ratio(spot, lo, hi, kind, t, variance, r, q) <= MARGIN:
                                    add(f"{kname}, sigma {sigma:g}, r {rate:g}, carry sigma^2/2 - {below:g}, spot "
                                        f"{spot:g}, {days} days, [{lo:g}, {hi:g}]", pspec, american(days),
                                        dbe.barrier_spec(kind, lo, hi), cash(CALL, 0.0))
    pspec = PROCESSES[0][1]
    for name, kind, lo, hi in [("knock-out, the spot on the lower barrier", KNOCK_OUT, 100.0, 120.0),
                               ("knock-out, the spot above the corridor", KNOCK_OUT, 80.0, 95.0),
                               ("KIKO, the spot below the corridor", KIKO, 105.0, 120.0),
                               ("KIKO, the spot on the upper barrier", KIKO, 80.0, 100.0),
                               ("KOKI, the spot on the lower barrier", KOKI, 100.0, 120.0),
                               ("KOKI, the spot above the corridor", KOKI, 80.0, 95.0)]:
        ex = american(365) if kind in (KIKO, KOKI) else abe.exercise_spec("European", 365)
        add(name, pspec, ex, dbe.barrier_spec(kind, lo, hi), cash(CALL, 0.0), greeks=True)
    add("expired", pspec, abe.exercise_spec("European", -1), dbe.barrier_spec(KNOCK_OUT, 80.0, 120.0), cash(CALL, 0.0))
    add("a European KIKO", pspec, abe.exercise_spec("European", 365), dbe.barrier_spec(KIKO, 80.0, 120.0),
        cash(CALL, 0.0))
    add("an American knock-out", pspec, american(365), dbe.barrier_spec(KNOCK_OUT, 80.0, 120.0), cash(CALL, 0.0))
    add("a plain-vanilla payoff", pspec, abe.exercise_spec("European", 365), dbe.barrier_spec(KNOCK_OUT, 80.0, 120.0),
        bc.payoff_spec("PlainVanilla", CALL, 100.0))
    add("barriers out of order", pspec, abe.exercise_spec("European", 365), dbe.barrier_spec(KNOCK_OUT, 120.0, 80.0),
        cash(CALL, 0.0))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/analyticdoublebarrierbinaryengine.hpp", cases)
