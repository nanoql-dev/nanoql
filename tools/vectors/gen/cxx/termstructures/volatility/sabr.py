# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the SABR functions of ql/termstructures/volatility/sabr.hpp, .cpp that the wheel doesn't bind, each
output computed by the C++ harness (docs/porting-playbook.md §9.4): the four expansions that check nothing,
unsafeSabrLogNormalVolatility, unsafeSabrNormalVolatility, unsafeShiftedSabrVolatility and unsafeSabrVolatility,
and validateSabrParameters.

- "grid" cases: 40 parameter sets drawn as tools/vectors/gen/termstructures/volatility/sabr.py draws its grid, with
  a seed of their own (the seed plus 1), each at the same four strikes: the two plain expansions at the strikes and
  the forward, unsafeShiftedSabrVolatility with the set's shift and type, and unsafeSabrVolatility with its type.
- "edge" cases: on a forward of 0.039, each type and beta (0, 0.5, 1) with rho -0.9999 and 0.9999, nu 0 and 0.4 and
  the expiries 0 and 1, the alpha giving a volatility of about 0.3 (lognormal) or 0.01 (normal): the four functions
  at the forward (log(F/K)'s expansion), at the forward times 1 + 1e-13 (the multiplier's expansion) and at twice the
  forward, the shifted one with a shift of 0.01.
- "arguments" cases: the four functions on lists of arguments the checking functions refuse and these compute on: a
  zero, negative or NaN strike or forward, a negative expiry, alpha 0 or negative, beta beyond [0, 1], a negative nu,
  rho at or beyond 1 in size, NaN parameters, infinite arguments, and a strike or forward plus the shift not
  positive.
- "validate" cases: validateSabrParameters on lists of parameters: the edges it accepts (beta 0 and 1, nu 0, rho
  0.9999) and each of its refusals, in its order (alpha, beta, nu, rho), NaN among them; null where it accepts.

A refused value is {"throws": true}. A volatility type outside the enum, which the expansions read as lognormal and
nanoql refuses, is never a case. tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SabrCxxVectorTests.cs reads the file
back."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_sabr", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "sabr.py"))
sabr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(sabr)

SL, N, TYPE_NAMES = sabr.SL, sabr.N, sabr.TYPE_NAMES
NAN, INF = float("nan"), float("inf")
GRID_SETS = 40
EDGE_STRIKES = [sabr.EDGE_FORWARD, sabr.EDGE_FORWARD * (1.0 + 1e-13), 2.0 * sabr.EDGE_FORWARD]

# (strike, forward, expiryTime, alpha, beta, nu, rho, shift, volatilityType)
ARGUMENTS = [
    [0.0, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL], [-0.01, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL],
    [-0.01, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, N], [NAN, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL],
    [0.03, 0.0, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL], [0.03, -0.02, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, N],
    [0.03, NAN, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL], [0.04, 0.03, -1.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL],
    [0.04, 0.03, -1.0, 0.2, 0.5, 0.4, 0.0, 0.0, N], [0.04, 0.03, 1.0, 0.0, 0.5, 0.4, 0.0, 0.0, SL],
    [0.04, 0.03, 1.0, -0.1, 0.5, 0.4, 0.0, 0.0, N], [0.04, 0.03, 1.0, 0.2, -0.5, 0.4, 0.0, 0.0, SL],
    [0.04, 0.03, 1.0, 0.2, 1.5, 0.4, 0.0, 0.0, SL], [0.04, 0.03, 1.0, 0.2, 1.5, 0.4, 0.0, 0.0, N],
    [0.04, 0.03, 1.0, 0.2, 0.5, -0.4, 0.0, 0.0, SL], [0.04, 0.03, 1.0, 0.2, 0.5, 0.4, 1.0, 0.0, SL],
    [0.04, 0.03, 1.0, 0.2, 0.5, 0.4, -1.0, 0.0, N], [0.04, 0.03, 1.0, 0.2, 0.5, 0.4, 1.5, 0.0, SL],
    [0.04, 0.03, 1.0, NAN, 0.5, 0.4, 0.0, 0.0, SL], [0.04, 0.03, 1.0, 0.2, NAN, 0.4, 0.0, 0.0, N],
    [0.04, 0.03, 1.0, 0.2, 0.5, NAN, 0.0, 0.0, SL], [0.04, 0.03, 1.0, 0.2, 0.5, 0.4, NAN, 0.0, SL],
    [INF, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL], [0.04, INF, 1.0, 0.2, 0.5, 0.4, 0.0, 0.0, N],
    [0.04, 0.03, INF, 0.2, 0.5, 0.4, 0.0, 0.0, SL], [0.04, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, INF, SL],
    [-0.01, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, SL], [-0.02, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, N],
    [0.03, -0.01, 1.0, 0.2, 0.5, 0.4, 0.0, 0.01, SL], [0.03, 0.03, 1.0, 0.2, 0.5, 0.4, 0.0, -0.05, SL],
    [0.03, 0.03, 0.0, 0.2, 0.5, 0.4, 0.0, 0.0, SL], [0.04, 0.03, 1.0, 0.2, 0.0, 0.0, -0.9999, 0.0, N]]
# (alpha, beta, nu, rho)
VALIDATE = [
    [0.2, 0.5, 0.4, 0.0], [0.2, 0.0, 0.4, 0.0], [0.2, 1.0, 0.4, 0.0], [0.2, 0.5, 0.0, 0.0], [0.2, 0.5, 0.4, 0.9999],
    [0.2, 0.5, 0.4, -0.9999], [1e-300, 0.5, 0.4, 0.0], [INF, 1.0, INF, 0.0],
    [0.0, 0.5, 0.4, 0.0], [-0.1, 0.5, 0.4, 0.0], [NAN, 0.5, 0.4, 0.0], [0.2, -1e-16, 0.4, 0.0],
    [0.2, 1.1, 0.4, 0.0], [0.2, NAN, 0.4, 0.0], [0.2, 0.5, -0.1, 0.0], [0.2, 0.5, NAN, 0.0], [0.2, 0.5, 0.4, 1.0],
    [0.2, 0.5, 0.4, -1.0], [0.2, 0.5, 0.4, 1.0000000000000002], [0.2, 0.5, 0.4, NAN], [0.0, 1.5, -0.1, 1.0],
    [0.2, 0.5, 0.4, -INF]]


def expansions(H, strikes, f, t, a, b, n, r, shift, typ):
    def each(method, *rest):
        return [v.attempt(lambda k=k: H.call(method, k, f, t, a, b, n, r, *rest)) for k in strikes]

    return {"unsafeSabrLogNormalVolatility": each("unsafeSabrLogNormalVolatility"),
            "unsafeSabrNormalVolatility": each("unsafeSabrNormalVolatility"),
            "unsafeShiftedSabrVolatility": each("unsafeShiftedSabrVolatility", shift, typ),
            "unsafeSabrVolatility": each("unsafeSabrVolatility", typ)}


def grid_cases(H, st):
    out = []
    for k, (typ, f, t, a, b, n, r, shift, low, high) in enumerate(sabr.random_sets(random.Random(v.SEED + 1))):
        if k == GRID_SETS:
            break
        strikes = [f * low, f, f * (low + high) / 2.0 * 1.0000001, f * high]
        out.append({"name": f"grid {TYPE_NAMES[typ]} shift {shift!r} beta {b!r} / {k}",
                    "inputs": {"settings": st, "kind": "grid", **sabr.parameters(f, t, a, b, n, r, shift, typ),
                               "strikes": strikes},
                    "expected": expansions(H, strikes, f, t, a, b, n, r, shift, typ)})
    return out


def edge_cases(H, st):
    out = []
    for typ, t, a, b, n, r in sabr.edge_sets():
        if r == 0.0 or t == 1e-4:
            continue
        f = sabr.EDGE_FORWARD
        out.append({"name": f"edge {TYPE_NAMES[typ]} beta {b!r} rho {r!r} nu {n!r} expiry {t!r}",
                    "inputs": {"settings": st, "kind": "edge",
                               **sabr.parameters(f, t, a, b, n, r, sabr.EDGE_SHIFT, typ), "strikes": EDGE_STRIKES},
                    "expected": expansions(H, EDGE_STRIKES, f, t, a, b, n, r, sabr.EDGE_SHIFT, typ)})
    return out


def argument_cases(H, st):
    def each(method, pick):
        return [v.attempt(lambda a=a: H.call(method, *pick(a))) for a in ARGUMENTS]

    return [{"name": "the expansions' arguments",
             "inputs": {"settings": st, "kind": "arguments", "arguments": v.enc(ARGUMENTS)},
             "expected": {"unsafeSabrLogNormalVolatility": each("unsafeSabrLogNormalVolatility", lambda a: a[:7]),
                          "unsafeSabrNormalVolatility": each("unsafeSabrNormalVolatility", lambda a: a[:7]),
                          "unsafeShiftedSabrVolatility": each("unsafeShiftedSabrVolatility", lambda a: a),
                          "unsafeSabrVolatility": each("unsafeSabrVolatility", lambda a: a[:7] + a[8:])}},
            {"name": "validateSabrParameters",
             "inputs": {"settings": st, "kind": "validate", "arguments": v.enc(VALIDATE)},
             "expected": {"validateSabrParameters": [v.attempt(lambda a=a: H.call("validateSabrParameters", *a))
                                                     for a in VALIDATE]}}]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 19, 10, 2026))
        return grid_cases(H, st) + edge_cases(H, st) + argument_cases(H, st)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/sabr.hpp", cases)
