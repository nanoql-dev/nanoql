# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the SABR functions of ql/termstructures/volatility/sabr.hpp, .cpp that the wheel binds:
sabrVolatility, shiftedSabrVolatility, sabrFlochKennedyVolatility and sabrGuess, the last over Boost 1.90's
cubic_roots and quadratic_roots (boost/math/tools/cubic_roots.hpp, roots.hpp).

- "grid" cases: 150 parameter sets drawn at random (the seed), each of either volatility type, a forward from 0.002 to
  0.08 or from 0.5 to 3, a shift of 0, 0.01 or 0.03, a beta of 0, 0.3, 0.5, 0.7, 1 or drawn, an expiry from 0.05 to
  12, a nu from 0.02 to 1.5, a rho from -0.9 to 0.9 and an alpha that gives the type's volatility level. At four
  strikes (the forward, two either side of it by 1% to 30%, and a point between them just off the middle):
  shiftedSabrVolatility with the set's shift and type, sabrVolatility with its type (no shift), and for a lognormal
  set sabrFlochKennedyVolatility; and sabrGuess on the three strikes around the forward, with the volatilities
  shiftedSabrVolatility gives there ("smile", an input) and the set's beta, shift and type.
- "edge" cases: on a forward of 0.039, every combination of type, beta (0, 0.5, 1), rho (-0.9999, 0, 0.9999), nu (0,
  0.4) and expiry (0, 1e-4, 1), the alpha giving a volatility of about 0.3 (lognormal) or 0.01 (normal). At the
  forward (where close() takes log(F/K)'s expansion), at the forward times 1 + 1e-13, 1 - 1e-13 and 1 + 1e-9 (where z
  is small enough for the multiplier's expansion), and at half and twice the forward: sabrVolatility,
  shiftedSabrVolatility with a shift of 0.01, and for a lognormal set sabrFlochKennedyVolatility, whose Taylor
  expansion holds within 0.25% of the forward and which gives NaN for nu 0 or beta 1 outside it.
- "guessTest" cases: interpolations.cpp's testSabrGuess (Le Floc'h and Kennedy's table 3), its 11 parameter sets for
  each type and shift (0, 0.01, 0.1), the smile at the forward and 5% either side from shiftedSabrVolatility.
- "guessSmile" cases: sabrGuess on smiles given directly, for each type and beta (0, 0.5, 1) over the strikes 0.0285,
  0.03 and 0.0315 with a forward of 0.03: all zero (sigma_0 = 0, so cubic_roots' d == 0 branch), flat, negative, and
  two smiles of their own shape. Between them the guesses reach every branch of the roots: the quadratic (a == 0,
  b != 0), the linear (a == b == 0), d == 0, three real roots and one.
- "arguments" cases: each function on lists of arguments: the refusals in QuantLib's order (the strike, the
  forward, the expiry, then validateSabrParameters' alpha, beta, nu and rho, NaN among them; for the shifted
  volatility the strike and the forward plus the shift), the edges QuantLib accepts (beta 0 and 1, nu 0, expiry 0, rho
  0.9999) and infinite arguments, on which the expansions give NaN or an infinity; sabrFlochKennedyVolatility, which
  checks nothing, where it gives NaN (alpha 0, a zero or negative strike, nu 0, beta 1 outside its Taylor band, NaN
  arguments) and either side of its band's edge; and sabrGuess's unknown volatility types 2 and -1.

A refused value is {"throws": true}. A volatility type outside the enum (which QuantLib's expansions read as
lognormal and nanoql refuses) and a non-finite argument to sabrGuess (on which QuantLib computes, and nanoql refuses)
are never cases. The wheel's functions are the proxies of the C++ functions, sabrGuess an %inline shim returning
QuantLib's std::array as a list (volatilities.i:303-345), positional only but for the last two. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/SabrVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
TYPE_NAMES = {SL: "ShiftedLognormal", N: "Normal"}
NAN = float("nan")
INF = float("inf")
RANDOM_SETS = 150

# testSabrGuess's table: (expiry, forward, alpha, beta, nu, rho).
GUESS_TESTS = [
    (0.058, 2016, 0.271, 1.0, 1.010, -0.345), (0.153, 2016, 0.256, 1.0, 0.933, -0.321),
    (0.230, 2016, 0.256, 1.0, 0.820, -0.346), (0.479, 2016, 0.255, 1.0, 0.629, -0.370),
    (0.729, 2016, 0.257, 1.0, 0.528, -0.403), (1.227, 2016, 0.260, 1.0, 0.448, -0.429),
    (1.726, 2016, 0.261, 1.0, 0.392, -0.440), (2.244, 2016, 0.262, 1.0, 0.355, -0.445),
    (2.742, 2016, 0.262, 1.0, 0.329, -0.445), (3.241, 2016, 0.262, 1.0, 0.310, -0.447),
    (4.239, 2016, 0.263, 1.0, 0.284, -0.452)]
GUESS_SMILES = [("zero", [0.0, 0.0, 0.0]), ("flat", [0.2, 0.2, 0.2]), ("negative", [-0.2, -0.2, -0.2]),
                ("smile", [0.25, 0.2, 0.22]), ("rising", [0.002, 0.0021, 0.0025])]
GUESS_SMILE_STRIKES = [0.0285, 0.03, 0.0315]

EDGE_FORWARD = 0.039
EDGE_SHIFT = 0.01
EDGE_STRIKES = [EDGE_FORWARD, EDGE_FORWARD * (1.0 + 1e-13), EDGE_FORWARD * (1.0 - 1e-13),
                EDGE_FORWARD * (1.0 + 1e-9), 0.5 * EDGE_FORWARD, 2.0 * EDGE_FORWARD]

# sabrVolatility(strike, forward, expiryTime, alpha, beta, nu, rho, volatilityType)
SABR_ARGUMENTS = [
    [0.0, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, SL], [-0.01, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, N],
    [NAN, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, SL], [0.03, 0.0, 1.0, 0.2, 0.5, 0.4, 0.0, SL],
    [0.03, -0.01, 1.0, 0.2, 0.5, 0.4, 0.0, N], [0.03, NAN, 1.0, 0.2, 0.5, 0.4, 0.0, SL],
    [0.03, 0.03, -1.0, 0.2, 0.5, 0.4, 0.0, SL], [0.03, 0.03, NAN, 0.2, 0.5, 0.4, 0.0, N],
    [0.03, 0.03, 1.0, 0.0, 0.5, 0.4, 0.0, SL], [0.03, 0.03, 1.0, -0.1, 0.5, 0.4, 0.0, N],
    [0.03, 0.03, 1.0, NAN, 0.5, 0.4, 0.0, SL], [0.03, 0.03, 1.0, 0.2, -0.1, 0.4, 0.0, SL],
    [0.03, 0.03, 1.0, 0.2, 1.1, 0.4, 0.0, N], [0.03, 0.03, 1.0, 0.2, NAN, 0.4, 0.0, SL],
    [0.03, 0.03, 1.0, 0.2, 0.5, -0.1, 0.0, SL], [0.03, 0.03, 1.0, 0.2, 0.5, NAN, 0.0, N],
    [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, 1.0, SL], [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, -1.0, N],
    [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, 1.5, SL], [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, NAN, SL],
    [0.0, 0.0, -1.0, 0.0, 1.5, -0.1, 1.0, SL],
    [0.03, 0.03, 0.0, 0.2, 0.5, 0.4, 0.0, SL], [0.025, 0.03, 1.0, 0.2, 0.0, 0.4, 0.3, SL],
    [0.025, 0.03, 1.0, 0.2, 1.0, 0.4, -0.3, N], [0.04, 0.03, 2.0, 0.2, 0.5, 0.0, 0.0, SL],
    [0.04, 0.03, 2.0, 0.2, 0.5, 0.4, 0.9999, N], [0.04, 0.03, 2.0, 0.2, 0.5, 0.4, -0.9999, SL],
    [INF, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, SL], [0.03, INF, 1.0, 0.2, 0.5, 0.4, 0.0, N],
    [0.03, 0.03, INF, 0.2, 0.5, 0.4, 0.0, SL], [0.03, 0.03, 1.0, INF, 0.5, 0.4, 0.0, SL],
    [0.03, 0.03, 1.0, 0.2, 0.5, INF, 0.0, SL]]
# shiftedSabrVolatility(strike, forward, expiryTime, alpha, beta, nu, rho, shift, volatilityType)
SHIFTED_ARGUMENTS = [
    [-0.02, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, SL], [-0.01, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, N],
    [NAN, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, SL], [0.03, -0.011, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, SL],
    [0.03, -0.01, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, N], [0.03, 0.03, -0.5, 0.2, 0.5, 0.4, 0.0, 0.01, SL],
    [0.03, 0.03, 1.0, 0.0, 0.5, 0.4, 0.0, 0.01, SL], [0.03, 0.03, 1.0, 0.2, 2.0, 0.4, 0.0, 0.01, N],
    [0.03, 0.03, 1.0, 0.2, 0.5, -1e-12, 0.0, 0.01, SL], [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, -1.0, 0.01, SL],
    [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, NAN, SL],
    [-0.005, -0.002, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, SL], [-0.005, -0.002, 1.0, 0.002, 0.5, 0.4, 0.0, 0.01, N],
    [0.03, 0.02, 1.0, 0.2, 0.5, 0.4, 0.0, -0.01, SL], [0.03, 0.03, 0.0, 0.2, 0.0, 0.0, 0.0, 0.03, N],
    [0.0, 0.0, 1.0, 0.2, 1.0, 0.4, 0.5, 0.005, SL], [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, INF, SL]]
# sabrFlochKennedyVolatility(strike, forward, expiryTime, alpha, beta, nu, rho), which checks nothing
FLOCH_KENNEDY_ARGUMENTS = [
    [0.03, 0.03, 1.0, 0.0, 0.5, 0.4, 0.0], [0.04, 0.03, 1.0, 0.0, 0.5, 0.4, 0.0],
    [-0.01, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0], [0.0, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0], [0.04, 0.03, 1.0, 0.2, 0.5, 0.0, 0.0],
    [0.03, 0.03, 1.0, 0.2, 0.5, 0.0, 0.0],
    [0.04, 0.03, 1.0, 0.3, 1.0, 0.4, 0.0], [0.03 * 1.002, 0.03, 1.0, 0.3, 1.0, 0.4, 0.0],
    [0.03 / 1.0024, 0.03, 1.0, 0.2, 0.5, 0.4, -0.3], [0.03 / 1.0026, 0.03, 1.0, 0.2, 0.5, 0.4, -0.3],
    [0.03, 0.03, 0.0, 0.2, 0.5, 0.4, 0.0], [0.04, 0.03, -1.0, 0.2, 0.5, 0.4, 0.0],
    [0.04, 0.03, 1.0, 0.2, 0.5, 0.4, 1.0],
    [NAN, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0], [0.04, 0.03, 1.0, 0.2, 0.5, 0.4, NAN]]
# sabrGuess(k_m, vol_m, k_0, vol_0, k_p, vol_p, forward, expiryTime, beta, shift, volatilityType)
GUESS_ARGUMENTS = [
    [0.0285, 0.21, 0.03, 0.2, 0.0315, 0.205, 0.03, 1.0, 0.5, 0.0, 2],
    [0.0285, 0.21, 0.03, 0.2, 0.0315, 0.205, 0.03, 1.0, 0.5, 0.0, -1]]


def lognormal_alpha(level, forward, shift, beta):
    return level * (forward + shift) ** (1.0 - beta)


def normal_alpha(level, forward, shift, beta):
    return level * (forward + shift) ** (-beta)


def random_sets(rnd):
    """(type, forward, expiry, alpha, beta, nu, rho, shift, low, high) for the grid cases: low and high are the
    strikes' ratios to the forward either side of it."""
    out = []
    for _ in range(RANDOM_SETS):
        typ = rnd.choice((SL, N))
        f = rnd.choice((rnd.uniform(0.002, 0.08), rnd.uniform(0.5, 3.0)))
        shift = rnd.choice((0.0, 0.0, 0.01, 0.03))
        b = rnd.choice((0.0, 0.3, 0.5, 0.7, 1.0, rnd.uniform(0.0, 1.0)))
        t = rnd.uniform(0.05, 12.0)
        n = rnd.uniform(0.02, 1.5)
        r = rnd.uniform(-0.9, 0.9)
        if typ == SL:
            a = lognormal_alpha(rnd.uniform(0.05, 0.6), f, shift, b)
        else:
            a = normal_alpha(rnd.uniform(0.002, 0.02) * (f + shift), f, shift, b)
        low, high = rnd.choice(((0.95, 1.05), (0.9, 1.1), (0.99, 1.01), (0.8, 1.3)))
        out.append((typ, f, t, a, b, n, r, shift, low, high))
    return out


def edge_sets():
    """(type, expiry, alpha, beta, nu, rho) for the edge cases, on EDGE_FORWARD."""
    out = []
    for typ in (SL, N):
        for b in (0.0, 0.5, 1.0):
            a = lognormal_alpha(0.3, EDGE_FORWARD, 0.0, b) if typ == SL else normal_alpha(0.01, EDGE_FORWARD, 0.0, b)
            for r in (-0.9999, 0.0, 0.9999):
                for n in (0.0, 0.4):
                    for t in (0.0, 1e-4, 1.0):
                        out.append((typ, t, a, b, n, r))
    return out


def volatilities(strikes, f, t, a, b, n, r, shift, typ):
    out = {"shiftedSabrVolatility": [v.attempt(lambda k=k: ql.shiftedSabrVolatility(k, f, t, a, b, n, r, shift, typ))
                                     for k in strikes],
           "sabrVolatility": [v.attempt(lambda k=k: ql.sabrVolatility(k, f, t, a, b, n, r, typ)) for k in strikes]}
    if typ == SL:
        out["sabrFlochKennedyVolatility"] = [v.attempt(lambda k=k: ql.sabrFlochKennedyVolatility(k, f, t, a, b, n, r))
                                             for k in strikes]
    return out


def guess(strikes, vols, f, t, b, shift, typ):
    return v.attempt(lambda: list(ql.sabrGuess(strikes[0], vols[0], strikes[1], vols[1], strikes[2], vols[2], f, t, b,
                                               shift, typ)))


def parameters(f, t, a, b, n, r, shift, typ):
    return {"forward": f, "expiryTime": t, "alpha": a, "beta": b, "nu": n, "rho": r, "shift": shift,
            "volatilityType": typ}


def grid_cases(st):
    out = []
    for k, (typ, f, t, a, b, n, r, shift, low, high) in enumerate(random_sets(random.Random(v.SEED))):
        strikes = [f * low, f, f * (low + high) / 2.0 * 1.0000001, f * high]
        smile_strikes = [f * low, f, f * high]
        smile = [ql.shiftedSabrVolatility(x, f, t, a, b, n, r, shift, typ) for x in smile_strikes]
        expected = volatilities(strikes, f, t, a, b, n, r, shift, typ)
        expected["sabrGuess"] = guess(smile_strikes, smile, f, t, b, shift, typ)
        out.append({"name": f"grid {TYPE_NAMES[typ]} shift {shift!r} beta {b!r} / {k}",
                    "inputs": {"settings": st, "kind": "grid", **parameters(f, t, a, b, n, r, shift, typ),
                               "strikes": strikes, "smileStrikes": smile_strikes, "smile": smile},
                    "expected": expected})
    return out


def edge_cases(st):
    out = []
    for typ, t, a, b, n, r in edge_sets():
        f = EDGE_FORWARD
        expected = volatilities(EDGE_STRIKES, f, t, a, b, n, r, EDGE_SHIFT, typ)
        out.append({"name": f"edge {TYPE_NAMES[typ]} beta {b!r} rho {r!r} nu {n!r} expiry {t!r}",
                    "inputs": {"settings": st, "kind": "edge", **parameters(f, t, a, b, n, r, EDGE_SHIFT, typ),
                               "strikes": EDGE_STRIKES},
                    "expected": expected})
    return out


def guess_test_cases(st):
    out = []
    for i, (t, f, a, b, n, r) in enumerate(GUESS_TESTS):
        f = float(f)
        for typ in (SL, N):
            for shift in (0.0, 0.01, 0.1):
                # strikes at forward and plus or minus 5%
                strikes = [f * 0.95, f, f * 1.05]
                smile = [ql.shiftedSabrVolatility(x, f, t, a, b, n, r, shift, typ) for x in strikes]
                out.append({"name": f"testSabrGuess {i} {TYPE_NAMES[typ]} shift {shift!r}",
                            "inputs": {"settings": st, "kind": "guess", "strikes": strikes, "smile": smile,
                                       "forward": f, "expiryTime": t, "beta": b, "shift": shift,
                                       "volatilityType": typ},
                            "expected": {"sabrGuess": guess(strikes, smile, f, t, b, shift, typ)}})
    return out


def guess_smile_cases(st):
    out = []
    for name, smile in GUESS_SMILES:
        for typ in (SL, N):
            for b in (0.0, 0.5, 1.0):
                out.append({"name": f"guess smile {name} {TYPE_NAMES[typ]} beta {b!r}",
                            "inputs": {"settings": st, "kind": "guess", "strikes": GUESS_SMILE_STRIKES,
                                       "smile": smile, "forward": 0.03, "expiryTime": 1.0, "beta": b, "shift": 0.0,
                                       "volatilityType": typ},
                            "expected": {"sabrGuess": guess(GUESS_SMILE_STRIKES, smile, 0.03, 1.0, b, 0.0, typ)}})
    return out


def argument_cases(st):
    return [
        {"name": "sabrVolatility arguments",
         "inputs": {"settings": st, "kind": "sabrArguments", "arguments": v.enc(SABR_ARGUMENTS)},
         "expected": {"sabrVolatility": [v.attempt(lambda a=a: ql.sabrVolatility(*a)) for a in SABR_ARGUMENTS]}},
        {"name": "shiftedSabrVolatility arguments",
         "inputs": {"settings": st, "kind": "shiftedArguments", "arguments": v.enc(SHIFTED_ARGUMENTS)},
         "expected": {"shiftedSabrVolatility": [v.attempt(lambda a=a: ql.shiftedSabrVolatility(*a))
                                                for a in SHIFTED_ARGUMENTS]}},
        {"name": "sabrFlochKennedyVolatility arguments",
         "inputs": {"settings": st, "kind": "flochKennedyArguments", "arguments": v.enc(FLOCH_KENNEDY_ARGUMENTS)},
         "expected": {"sabrFlochKennedyVolatility": [v.attempt(lambda a=a: ql.sabrFlochKennedyVolatility(*a))
                                                     for a in FLOCH_KENNEDY_ARGUMENTS]}},
        {"name": "sabrGuess arguments",
         "inputs": {"settings": st, "kind": "guessArguments", "arguments": v.enc(GUESS_ARGUMENTS)},
         "expected": {"sabrGuess": [v.attempt(lambda a=a: list(ql.sabrGuess(*a))) for a in GUESS_ARGUMENTS]}}]


def cases():
    st = v.settings(ql.Date(19, ql.October, 2026))
    out = grid_cases(st) + edge_cases(st) + guess_test_cases(st) + guess_smile_cases(st) + argument_cases(st)
    for c in out:
        g = c["expected"].get("sabrGuess")
        if c["inputs"]["kind"] in ("grid", "guess"):
            assert isinstance(g, list) and len(g) == 4, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/sabr.hpp", cases)
