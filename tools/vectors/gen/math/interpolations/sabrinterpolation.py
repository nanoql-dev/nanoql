# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SABRInterpolation (ql/math/interpolations/sabrinterpolation.hpp) and the fit it runs,
XABRInterpolationImpl::update (ql/math/interpolations/xabrinterpolation.hpp), through the wheel's SABRInterpolation,
SWIG's SafeSABRInterpolation, which copies its strikes, volatilities and forward and calls update() in its constructor
(volatilities.i:348-391), so that its values are QuantLib's construction followed by update(), nanoql's
construction-time fit. A calibration's outputs, they pass the stability filter below.

- "fit" cases, 86:
  - testSabrInterpolation's 64 fits (test-suite/interpolations.cpp:1371-1525): its 31 strikes from 0.03 to 0.09 and
    literal volatilities, an expiry of 1 and a forward of 0.039, Simplex(0.01) and LevenbergMarquardt(1e-8, 1e-8,
    1e-8), vega-weighted or not, every fixed and free pattern (a fixed parameter at the test's initial value, a free
    one at its guess), EndCriteria(100000, 100, 1e-8, 1e-8, 1e-8), errorAccept 1e-10 and up to 50 guesses
    ("test.<method>.v<vega>.f<the four flags>");
  - testSabrSingleCases' fit (:2145-2168), on QuantLib's defaults: alpha, nu and rho Null<Real>(), beta 0.25 fixed;
  - the defaults on the test's data, from its guesses and from Null<Real>() everywhere; one guess (maxGuesses 1) and
    the maximum error (useMaxError) with each method; a shift of 0.03 under a negative forward and strikes, the
    volatilities from shiftedSabrVolatility; normal volatilities, from its normal form;
  - forced restarts, which run out: errorAccept 0 and 10 guesses, each method, by the root mean square error and the
    maximum error, every parameter free or beta fixed;
  - maxGuesses 0 (QuantLib's do-while runs one fit, as for 1), Null<Real>() guesses flagged fixed (and so free), and
    five unsorted strikes, whose range is their first and last.

  Each gives alpha, beta, nu and rho; its values at the strikes, between them and outside them (allowExtrapolation
  set); rmsError and maxError, which the wheel doesn't bind, recomputed here from the wheel's values at the strikes
  and the market volatilities with QuantLib's formulas (xabrinterpolation.hpp:245-285): the weights 1/n, or the
  normalised vega weights, blackFormulaStdDevDerivative(k, F, sqrt(v v t), 1, shift) transcribed with the normal
  density; sqrt(n sum(w e e) / (n - 1)) and the largest |e| from QuantLib's QL_MIN_REAL, the sums by loop, never
  Python's compensated sum(); the values read are checked, bit for bit, to be shiftedSabrVolatility at the reported
  parameters, so the errors are the reported fit's. And its values just outside the range without extrapolation,
  which QuantLib refuses.
- "refusal" cases: the interpolations QuantLib refuses to build or fit: an expiry of 0, -1 or NaN; the forward plus
  the shift not positive; no strike; fewer strikes than free parameters under Levenberg-Marquardt (one and three);
  a free guess out of validateSabrParameters' range (alpha -0.1, beta 1.5, rho 1); a negative shift with vega weights,
  which blackFormulaStdDevDerivative refuses; and a strike at minus the shift, whose value shiftedSabrVolatility
  refuses at the fit's first evaluation.

**The stability filter** (the plan's S7R6, master §3.2): each fit is refitted in the wheel with every input volatility
moved one ulp up, then down, by math.nextafter, together; then each alone, up and down. An output is written only
where every refit leaves it within 1e-12 x max(|base|, |moved|, 1); the others are dropped, and the case's input
"evalAt" lists the points whose values are kept. The generator prints "dropped n of m outputs" per case and for the
file; the outputs just outside the range, which no fit computes, aren't filtered.

A refused value is {"throws": true}. Each case's inputs are the interpolation's arguments: "x", "y", "t", "forward",
"alpha" ... "rho" (null for Null<Real>()), "alphaIsFixed" ... "rhoIsFixed", "vegaWeighted", "endCriteria" (null for
QuantLib's default, else {"maxIterations", "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon",
"gradientNormEpsilon"}), "method" (null for QuantLib's default, else {"type": "Simplex", "lambda"} or {"type":
"LevenbergMarquardt", "epsfcn", "xtol", "gtol", "useCostFunctionsJacobian"}), "errorAccept", "useMaxError",
"maxGuesses", "shift" and "volatilityType". A guess the parameter transforms can't invert (a rho beyond 0.9999, an
alpha or nu below 1e-7), where QuantLib flips rho's sign or fails and nanoql clamps, an all-zero smile with vega
weights, where QuantLib fits on NaN weights and nanoql refuses, and fewer volatilities than strikes, which QuantLib
reads past, are never cases. The wheel's shim exposes only the value and alpha() ... rho(); the members it doesn't
bind are the harness's (tools/vectors/gen/cxx/math/interpolations/sabrinterpolation.py). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/SABRInterpolationVectorTests.cs."""
import math
import os
import struct
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

NULL = v.NULL_REAL
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
RELATIVE = 1e-12
# QuantLib's M_SQRT_2 and M_1_SQRTPI, for the normal density of the vega weights.
M_SQRT_2 = 0.7071067811865475244008443621048490392848359376887
M_1_SQRTPI = 0.564189583547756286948

# testSabrInterpolation's data (test-suite/interpolations.cpp:1380-1404).
STRIKES = [0.03, 0.032, 0.034, 0.036, 0.038, 0.04, 0.042, 0.044, 0.046, 0.048, 0.05, 0.052, 0.054, 0.056, 0.058, 0.06,
           0.062, 0.064, 0.066, 0.068, 0.07, 0.072, 0.074, 0.076, 0.078, 0.08, 0.082, 0.084, 0.086, 0.088, 0.09]
VOLS = [1.16725837321531, 1.15226075991385, 1.13829711098834, 1.12524190877505, 1.11299079244474, 1.10145609357162,
        1.09056348513411, 1.08024942745106, 1.07045919457758, 1.06114533019077, 1.05226642581503, 1.04378614411707,
        1.03567243073732, 1.0278968727451, 1.02043417226345, 1.01326171139321, 1.00635919013311, 0.999708323124949,
        0.993292584155381, 0.987096989695393, 0.98110791455717, 0.975312934134512, 0.969700688771689,
        0.964260766651027, 0.958983602256592, 0.953860388001395, 0.948882997029509, 0.944043915545469,
        0.939336183299237, 0.934753341079515, 0.930289384251337]
EXPIRY, FORWARD = 1.0, 0.039
INITIAL = (0.3, 0.6, 0.02, 0.01)
GUESS = (math.sqrt(0.2), 0.5, math.sqrt(0.4), 0.0)
EVAL_AT = STRIKES + [0.5 * (STRIKES[i] + STRIKES[i + 1]) for i in range(30)] + [0.02, 0.1]

END_CRITERIA = {"maxIterations": 100000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8,
                "functionEpsilon": 1e-8, "gradientNormEpsilon": 1e-8}
SIMPLEX = {"type": "Simplex", "lambda": 0.01}
LM = {"type": "LevenbergMarquardt", "epsfcn": 1e-8, "xtol": 1e-8, "gtol": 1e-8, "useCostFunctionsJacobian": False}
METHODS = [("simplex", SIMPLEX), ("lm", LM)]


def fit_spec(x, y, t, forward, params, fixed, vega, method, end_criteria, error_accept, use_max_error, max_guesses,
             shift=0.0, vol_type=SL):
    return {"x": x, "y": y, "t": t, "forward": forward, "alpha": params[0], "beta": params[1], "nu": params[2],
            "rho": params[3], "alphaIsFixed": fixed[0], "betaIsFixed": fixed[1], "nuIsFixed": fixed[2],
            "rhoIsFixed": fixed[3], "vegaWeighted": vega, "endCriteria": end_criteria, "method": method,
            "errorAccept": error_accept, "useMaxError": use_max_error, "maxGuesses": max_guesses, "shift": shift,
            "volatilityType": vol_type}


def method_of(spec):
    m = spec["method"]
    if m is None:
        return None
    if m["type"] == "Simplex":
        return ql.Simplex(m["lambda"])
    return ql.LevenbergMarquardt(m["epsfcn"], m["xtol"], m["gtol"], m["useCostFunctionsJacobian"])


def end_criteria_of(spec):
    e = spec["endCriteria"]
    if e is None:
        return None
    return ql.EndCriteria(e["maxIterations"], e["maxStationaryStateIterations"], e["rootEpsilon"],
                          e["functionEpsilon"], e["gradientNormEpsilon"])


def interpolate(spec, ys):
    """The wheel's fit of a spec over the volatilities ys, a fresh optimizer and end criteria for each, as QuantLib's
    callers pass them."""
    kw = dict(alphaIsFixed=spec["alphaIsFixed"], betaIsFixed=spec["betaIsFixed"], nuIsFixed=spec["nuIsFixed"],
              rhoIsFixed=spec["rhoIsFixed"], vegaWeighted=spec["vegaWeighted"], errorAccept=spec["errorAccept"],
              useMaxError=spec["useMaxError"], maxGuesses=spec["maxGuesses"], shift=spec["shift"],
              volatilityType=spec["volatilityType"])
    method, ec = method_of(spec), end_criteria_of(spec)
    if method is not None:
        kw["optMethod"] = method
    if ec is not None:
        kw["endCriteria"] = ec
    return ql.SABRInterpolation(ql.Array(spec["x"]), ql.Array(ys), spec["t"], spec["forward"], spec["alpha"],
                                spec["beta"], spec["nu"], spec["rho"], **kw)


def density(x):
    # NormalDistribution()(x): its exponent, then M_SQRT_2 M_1_SQRTPI / sigma times exp.
    e = -(x * x) / 2.0
    return 0.0 if e <= -690.0 else (M_SQRT_2 * M_1_SQRTPI / 1.0) * math.exp(e)


def vega_weight(k, f, sd, shift):
    # SABRSpecs::weight: blackFormulaStdDevDerivative(strike, forward, stdDev, 1.0, shift), past its checks, which
    # the fit has passed.
    f, k = f + shift, k + shift
    if sd == 0.0 or k == 0.0:
        return 0.0
    d1 = math.log(f / k) / sd + .5 * sd
    return 1.0 * f * (density(d1) / 1.0)


def weights(spec, ys):
    n = len(spec["x"])
    if not spec["vegaWeighted"]:
        return [1.0 / n] * n
    w = [vega_weight(k, spec["forward"], math.sqrt(y * y * spec["t"]), spec["shift"]) for k, y in zip(spec["x"], ys)]
    total = 0.0
    for x in w:
        total += x
    return [x / total for x in w]


def bits(x):
    return struct.pack("<d", x)


def errors(spec, s, ys):
    """rmsError and maxError as QuantLib's interpolationError and interpolationMaxError compute them, from the fit's
    values at the strikes, checked to be shiftedSabrVolatility at the reported parameters."""
    params = (s.alpha(), s.beta(), s.nu(), s.rho())
    values = [s(x, True) for x in spec["x"]]
    for x, val in zip(spec["x"], values):
        ref = ql.shiftedSabrVolatility(x, spec["forward"], spec["t"], *params, spec["shift"], spec["volatilityType"])
        assert bits(val) == bits(ref), (x, val, ref)
    w = weights(spec, ys)
    n = len(values)
    total = 0.0
    largest = -sys.float_info.max
    for val, y, wi in zip(values, ys, w):
        e = val - y
        total += e * e * wi
        a = abs(val - y)
        largest = a if largest < a else largest
    return math.sqrt(n * total / (1 if n == 1 else n - 1)), largest


def outputs(spec, ys, eval_at):
    """The fit's outputs over ys, by name: the parameters, ("values", i) at each point, and the two errors."""
    s = interpolate(spec, ys)
    out = {"alpha": s.alpha(), "beta": s.beta(), "nu": s.nu(), "rho": s.rho()}
    for i, x in enumerate(eval_at):
        out[("values", i)] = s(x, True)
    out["rmsError"], out["maxError"] = errors(spec, s, ys)
    return out, s


def within(a, b):
    if a == b or (math.isnan(a) and math.isnan(b)):
        return True
    return abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def moves(ys):
    """The volatilities moved one ulp: all up, all down, then each alone up and down."""
    out = [[math.nextafter(y, math.inf) for y in ys], [math.nextafter(y, -math.inf) for y in ys]]
    for i in range(len(ys)):
        for direction in (math.inf, -math.inf):
            moved = list(ys)
            moved[i] = math.nextafter(ys[i], direction)
            out.append(moved)
    return out


def stable(spec, base, eval_at):
    """The names of base's outputs that every refit leaves within the filter's measure."""
    keep = set(base)
    for ys in moves(spec["y"]):
        try:
            moved, _ = outputs(spec, ys, eval_at)
        except RuntimeError:
            return set()
        keep = {k for k in keep if within(base[k], moved[k])}
    return keep


class Tally:
    def __init__(self):
        self.dropped = self.total = self.cases = 0


def fit_case(tally, name, spec, eval_at, st):
    base, s = outputs(spec, spec["y"], eval_at)
    keep = stable(spec, base, eval_at)
    dropped = len(base) - len(keep)
    tally.dropped += dropped
    tally.total += len(base)
    tally.cases += dropped > 0
    print(f"{name}: dropped {dropped} of {len(base)} outputs")
    kept_at = [x for i, x in enumerate(eval_at) if ("values", i) in keep]
    lo, hi = spec["x"][0], spec["x"][-1]
    outside = [lo - 0.005, hi + 0.005]
    expected = {k: base[k] for k in ("alpha", "beta", "nu", "rho") if k in keep}
    expected["values"] = [base[("values", i)] for i in range(len(eval_at)) if ("values", i) in keep]
    expected.update({k: base[k] for k in ("rmsError", "maxError") if k in keep})
    expected["outsideRange"] = [v.attempt(lambda x=x: s(x)) for x in outside]
    return {"name": name,
            "inputs": {"settings": st, "kind": "fit", **v.enc(spec), "evalAt": kept_at, "outside": outside,
                       "droppedOutputs": dropped},
            "expected": expected}


def refusal_case(name, spec, st):
    return {"name": "refusal " + name,
            "inputs": {"settings": st, "kind": "refusal", **v.enc(spec)},
            "expected": {"interpolation": v.attempt(lambda: interpolate(spec, spec["y"]).alpha())}}


def fits():
    """(name, spec, evaluation points) for the fit cases."""
    out = []
    for mname, method in METHODS:
        for vega in (True, False):
            for fa in (True, False):
                for fb in (True, False):
                    for fn in (True, False):
                        for fr in (True, False):
                            fixed = (fa, fb, fn, fr)
                            params = tuple(INITIAL[i] if fixed[i] else GUESS[i] for i in range(4))
                            out.append((f"test.{mname}.v{int(vega)}.f{int(fa)}{int(fb)}{int(fn)}{int(fr)}",
                                        fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, params, fixed, vega, method,
                                                 END_CRITERIA, 1e-10, False, 50), EVAL_AT))
    # testSabrSingleCases: alpha, nu and rho null, beta 0.25 fixed, QuantLib's defaults.
    sk = [0.01, 0.01125, 0.0125, 0.01375, 0.0150]
    sv = [0.1667, 0.2020, 0.2785, 0.3279, 0.3727]
    out.append(("single.default.x", fit_spec(sk, sv, 0.3833, 0.011025, (NULL, 0.25, NULL, NULL),
                                             (False, True, False, False), True, None, None, 0.002, False, 50),
                sk + [0.0105, 0.012, 0.016]))
    out.append(("defaults.default.x", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, GUESS, (False,) * 4, True, None, None,
                                               0.002, False, 50), EVAL_AT))
    out.append(("defaults.default.nulls", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, (NULL,) * 4, (False,) * 4, True,
                                                   None, None, 0.002, False, 50), EVAL_AT))
    for mname, method in METHODS:
        out.append((f"maxguesses1.{mname}.x", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, GUESS, (False,) * 4, False,
                                                       method, END_CRITERIA, 1e-10, False, 1), EVAL_AT))
        out.append((f"usemaxerror.{mname}.x", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, GUESS,
                                                       (False, True, False, False), True, method, END_CRITERIA, 1e-10,
                                                       True, 50), EVAL_AT))
    # A negative forward and strikes under a shift of 0.03, the volatilities from the shifted formula.
    sh, fwd2 = 0.03, -0.002
    nk = [-0.01 + 0.002 * i for i in range(16)]
    nv = [ql.shiftedSabrVolatility(k, fwd2, 2.0, 0.05, 0.4, 0.3, -0.2, sh) for k in nk]
    for mname, method in METHODS:
        out.append((f"shifted.{mname}.x", fit_spec(nk, nv, 2.0, fwd2, GUESS, (False, True, False, False), True, method,
                                                   END_CRITERIA, 1e-10, False, 50, sh), nk + [-0.0099, 0.0205]))
    # Normal volatilities, from the normal formula.
    nvs = [ql.shiftedSabrVolatility(k, FORWARD, 1.5, 0.02, 0.5, 0.35, 0.15, 0.0, ql.Normal) for k in STRIKES]
    for mname, method in METHODS:
        out.append((f"normal.{mname}.x", fit_spec(STRIKES, nvs, 1.5, FORWARD, GUESS, (False, True, False, False), True,
                                                  method, END_CRITERIA, 1e-10, False, 50, 0.0, N), EVAL_AT))
    # Forced restarts: errorAccept 0, so that all 10 guesses run.
    for mname, method in METHODS:
        for use_max in (False, True):
            for fixed in ((False,) * 4, (False, True, False, False)):
                params = tuple(INITIAL[i] if fixed[i] else GUESS[i] for i in range(4))
                out.append((f"forced.{mname}.{'max' if use_max else 'rms'}.f{''.join(str(int(f)) for f in fixed)}",
                            fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, params, fixed, not use_max, method, END_CRITERIA,
                                     0.0, use_max, 10), EVAL_AT))
    out.append(("maxguesses0.lm.x", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, GUESS, (False,) * 4, True, LM,
                                             END_CRITERIA, 1e-10, False, 0), EVAL_AT))
    out.append(("nullfixed.lm.x", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, (NULL, 0.6, NULL, 0.01), (True,) * 4, True,
                                           LM, END_CRITERIA, 1e-10, False, 50), EVAL_AT))
    uk = [0.05, 0.04, 0.06, 0.03, 0.07]
    uv = [ql.sabrVolatility(k, 0.05, 1.0, 0.04, 0.5, 0.4, -0.2) for k in uk]
    out.append(("unsorted.lm.x", fit_spec(uk, uv, 1.0, 0.05, GUESS, (False, True, False, False), False, LM,
                                          END_CRITERIA, 1e-10, False, 50), [0.05, 0.055, 0.06, 0.065, 0.07]))
    return out


def refusals():
    three, one = STRIKES[:3], STRIKES[:1]
    r = []
    r.append(("expiry 0", fit_spec(STRIKES, VOLS, 0.0, FORWARD, GUESS, (False,) * 4, True, None, None, 0.002, False,
                                   50)))
    r.append(("expiry -1", fit_spec(STRIKES, VOLS, -1.0, FORWARD, GUESS, (False,) * 4, True, None, None, 0.002, False,
                                    50)))
    r.append(("expiry NaN", fit_spec(STRIKES, VOLS, math.nan, FORWARD, GUESS, (False,) * 4, True, None, None, 0.002,
                                     False, 50)))
    r.append(("forward plus shift", fit_spec(STRIKES, VOLS, EXPIRY, -0.01, GUESS, (False,) * 4, True, None, None,
                                             0.002, False, 50)))
    r.append(("no strike", fit_spec([], [], EXPIRY, FORWARD, GUESS, (False,) * 4, True, None, None, 0.002, False, 50)))
    r.append(("three strikes, four free", fit_spec(three, VOLS[:3], EXPIRY, FORWARD, GUESS, (False,) * 4, False, LM,
                                                   END_CRITERIA, 1e-10, False, 50)))
    r.append(("one strike, four free", fit_spec(one, VOLS[:1], EXPIRY, FORWARD, GUESS, (False,) * 4, False, None,
                                                None, 0.002, False, 50)))
    r.append(("alpha guess", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, (-0.1, 0.5, 0.4, 0.0), (False,) * 4, True, None,
                                      None, 0.002, False, 50)))
    r.append(("beta guess", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, (0.2, 1.5, 0.4, 0.0), (False,) * 4, True, None,
                                     None, 0.002, False, 50)))
    r.append(("rho guess", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, (0.2, 0.5, 0.4, 1.0), (False,) * 4, True, None,
                                    None, 0.002, False, 50)))
    r.append(("negative shift with vega weights", fit_spec(STRIKES, VOLS, EXPIRY, FORWARD, GUESS, (False,) * 4, True,
                                                           None, None, 0.002, False, 50, -0.001)))
    r.append(("strike at minus the shift", fit_spec([0.0, 0.03, 0.05, 0.07], [0.3, 0.25, 0.22, 0.21], EXPIRY,
                                                    FORWARD, GUESS, (False,) * 4, False, LM, END_CRITERIA, 1e-10,
                                                    False, 50)))
    return r


def cases():
    st = v.settings(ql.Date(19, ql.October, 2026))
    tally = Tally()
    out = [fit_case(tally, *row, st) for row in fits()]
    out += [refusal_case(name, spec, st) for name, spec in refusals()]
    for c in out:
        if c["inputs"]["kind"] == "refusal":
            assert c["expected"]["interpolation"] == v.THROWS, c["name"]
    print(f"math/interpolations/sabrinterpolation: dropped {tally.dropped} of {tally.total} outputs, "
          f"in {tally.cases} of {len(out) - len(refusals())} fits")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/sabrinterpolation.hpp", cases)
