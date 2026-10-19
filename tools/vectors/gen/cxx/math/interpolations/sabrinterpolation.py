# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of SABRInterpolation (ql/math/interpolations/sabrinterpolation.hpp) that the wheel's shim
doesn't bind, and for the SABR factory, which the wheel doesn't bind at all, each output computed by the C++ harness
(docs/porting-playbook.md §9.4), which builds each interpolation and runs its update(), the fit QuantLib's callers run
at once. The fits are 12 of tools/vectors/gen/math/interpolations/sabrinterpolation.py's, by both methods, weighted
and not, every parameter fixed or some, its defaults, a shift, normal volatilities, the maximum error and forced
restarts; the three Levenberg-Marquardt fits with every parameter free, whose values the C runtimes' pow moves in their
last bits, are left to that file.

- "fit" cases, one per fit: expiry and forward; endCriteria, the end criterion of the minimization kept (QuantLib's
  EndCriteria::Type integer, None for the fit with every parameter fixed); interpolationWeights, 1/n or the
  normalised vega weights; rmsError and maxError; and the primitive, derivative and second derivative at the
  forward, which XABR refuses.
- "factory" cases: SABR's interpolate on 4 data sets, by Levenberg-Marquardt and Simplex, its default (unweighted)
  errors and vega weights, a shift and its defaults, updated: the values at the strikes, between them and outside
  them (allowExtrapolation set); and SABR::global.
- "refusal" cases: the factory on no strike, and on an expiry of 0.

**The stability filter** (the plan's S7R6) runs on the outputs a fit computes, endCriteria, rmsError, maxError and the
factory's values: each case is refitted in the harness with every input volatility moved one ulp up, then down, by
math.nextafter, together; then each alone, up and down; an output is written only where every refit leaves it within
1e-12 x max(|base|, |moved|, 1) (an end criterion where every refit gives the same), and the generator prints "dropped
n of m outputs" per case and for the file. Expiry, forward and the weights, which no fit computes, aren't filtered.

A refused value is {"throws": true}. Each fit's input "interpolation" is the spec
tools/cxx/src/math/interpolations/sabrinterpolation.cpp reads, the wheel file's inputs; a factory's input "factory"
is the same without "x", "y" and "volatilityType", with the data as "x" and "y" and the points as "points".
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/SABRInterpolationCxxVectorTests.cs reads the file back."""
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
    "wheel_sabrinterpolation", os.path.join(v.GEN_ROOT, "math", "interpolations", "sabrinterpolation.py"))
wsi = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(wsi)

# The wheel file's fits these take.
FITS = ["test.simplex.v1.f0000", "test.simplex.v0.f0111", "test.lm.v1.f1000", "test.lm.v0.f0100", "test.lm.v1.f1111",
        "single.default.x", "shifted.simplex.x", "normal.simplex.x", "usemaxerror.simplex.x",
        "forced.simplex.rms.f0100", "forced.lm.max.f0100", "nullfixed.lm.x"]
FITTED = ["endCriteria", "rmsError", "maxError"]


def within(a, b):
    if isinstance(a, int) or isinstance(b, int):
        return a == b
    return wsi.within(a, b)


def factory_spec(spec):
    return {k: val for k, val in spec.items() if k not in ("x", "y", "volatilityType")}


def fit_outputs(H, spec):
    return {name: H.call("SABRInterpolation::" + name, spec) for name in FITTED}


def factory_outputs(H, spec, x, y, points):
    values = H.call("SABR::interpolate", spec, x, y, points)
    return {("values", i): val for i, val in enumerate(values)}


def stable(base, refit, ys):
    """The names of base's outputs that every refit, over each of the moved volatilities, leaves within the filter's
    measure."""
    keep = set(base)
    for moved_ys in wsi.moves(ys):
        try:
            moved = refit(moved_ys)
        except RuntimeError:
            return set()
        keep = {k for k in keep if within(base[k], moved[k])}
    return keep


class Tally:
    def __init__(self):
        self.dropped = self.total = 0


def report(tally, name, dropped, total):
    tally.dropped += dropped
    tally.total += total
    print(f"{name}: dropped {dropped} of {total} outputs")


def fit_case(H, tally, st, name, spec, x_at):
    spec = v.enc(spec)
    base = fit_outputs(H, spec)
    keep = stable(base, lambda ys: fit_outputs(H, {**spec, "y": ys}), spec["y"])
    report(tally, name, len(base) - len(keep), len(base))
    expected = {"expiry": H.call("SABRInterpolation::expiry", spec),
                "forward": H.call("SABRInterpolation::forward", spec)}
    expected.update({k: base[k] for k in FITTED if k in keep})
    expected["interpolationWeights"] = H.call("SABRInterpolation::interpolationWeights", spec)
    for member in ("primitive", "derivative", "secondDerivative"):
        expected[member] = v.attempt(lambda m=member: H.call("SABRInterpolation::" + m, spec, x_at))
    return {"name": name, "inputs": {"settings": st, "kind": "fit", "interpolation": spec, "at": x_at},
            "expected": expected}


def factory_case(H, tally, st, name, spec, x, y, points):
    spec = v.enc(factory_spec(spec))
    base = factory_outputs(H, spec, x, y, points)
    keep = stable(base, lambda ys: factory_outputs(H, spec, x, ys, points), y)
    report(tally, name, len(base) - len(keep), len(base))
    kept = [p for i, p in enumerate(points) if ("values", i) in keep]
    return {"name": name,
            "inputs": {"settings": st, "kind": "factory", "factory": spec, "x": x, "y": y, "points": kept},
            "expected": {"values": [base[("values", i)] for i in range(len(points)) if ("values", i) in keep]}}


def factories():
    """(name, spec, x, y, points) for the factory cases; the spec's own vega weighting is the factory's default, off,
    unless it says otherwise."""
    strikes, vols, ec = wsi.STRIKES, wsi.VOLS, wsi.END_CRITERIA
    points = [0.03, 0.035, 0.04, 0.05, 0.061, 0.07, 0.083, 0.09, 0.02, 0.1]
    beta_fixed = (wsi.GUESS[0], wsi.INITIAL[1], wsi.GUESS[2], wsi.GUESS[3])
    sk = [0.01, 0.01125, 0.0125, 0.01375, 0.0150]
    sv = [0.1667, 0.2020, 0.2785, 0.3279, 0.3727]
    sh, fwd2 = 0.03, -0.002
    nk = [-0.01 + 0.002 * i for i in range(16)]
    nv = [ql.shiftedSabrVolatility(k, fwd2, 2.0, 0.05, 0.4, 0.3, -0.2, sh) for k in nk]
    return [
        ("factory lm beta fixed", wsi.fit_spec(strikes, vols, wsi.EXPIRY, wsi.FORWARD, beta_fixed,
                                               (False, True, False, False), False, wsi.LM, ec, 1e-10, False, 50),
         strikes, vols, points),
        ("factory simplex vega", wsi.fit_spec(strikes, vols, wsi.EXPIRY, wsi.FORWARD, wsi.GUESS, (False,) * 4, True,
                                              wsi.SIMPLEX, ec, 1e-10, False, 50), strikes, vols, points),
        ("factory defaults single", wsi.fit_spec(sk, sv, 0.3833, 0.011025, (wsi.NULL, 0.25, wsi.NULL, wsi.NULL),
                                                 (False, True, False, False), False, None, None, 0.002, False, 50),
         sk, sv, sk + [0.0105, 0.012, 0.016]),
        ("factory shifted simplex", wsi.fit_spec(nk, nv, 2.0, fwd2, wsi.GUESS, (False, True, False, False), False,
                                                 wsi.SIMPLEX, ec, 1e-10, False, 50, sh), nk, nv,
         nk[::3] + [-0.0099, 0.0205]),
    ]


def cases():
    fits = {name: (spec, at) for name, spec, at in wsi.fits()}
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 19, 10, 2026))
        tally = Tally()
        out = [fit_case(H, tally, st, name, fits[name][0], fits[name][0]["forward"]) for name in FITS]
        out += [factory_case(H, tally, st, *row) for row in factories()]
        out.append({"name": "SABR::global", "inputs": {"settings": st, "kind": "global"},
                    "expected": {"global": H.call("SABR::global")}})
        no_strike = v.enc(factory_spec(factories()[0][1]))
        expiry_zero = {**no_strike, "t": 0.0}
        for name, spec, x, y in (("no strike", no_strike, [], []),
                                 ("expiry 0", expiry_zero, wsi.STRIKES, wsi.VOLS)):
            out.append({"name": "refusal factory " + name,
                        "inputs": {"settings": st, "kind": "refusal", "factory": spec, "x": x, "y": y,
                                   "points": [0.05]},
                        "expected": {"values": v.attempt(lambda s=spec, x=x, y=y: H.call("SABR::interpolate", s, x,
                                                                                          y, [0.05]))}})
            assert out[-1]["expected"]["values"] == v.THROWS, name
        print(f"cxx/math/interpolations/sabrinterpolation: dropped {tally.dropped} of {tally.total} outputs")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/sabrinterpolation.hpp", cases)
