# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ZabrInterpolation and the Zabr factory (ql/math/interpolations/zabrinterpolation.hpp), which no wheel
class exposes, each output computed by the C++ harness (docs/porting-playbook.md §9.4): ZABR fits on its two
short-maturity kernels, over five smiles (SMILES below: two of them ZABR's own, from a model with γ 1 and one with γ
0.75), with QuantLib's defaults and with every setting changed in turn (FITS below): the fixed parameters, γ fixed at
1, at 0.75 and free among them; vega weights or none; Levenberg-Marquardt, the default, and Simplex; restarts accepted
at once, accepted after some, and run out at a tiny errorAccept; the maximum error as the score; maxGuesses 0 and 1;
the end criteria and the optimizer given; every parameter fixed; and no guess given. Then the factory's fits, whose
values QuantLib reads through its Interpolation, the factory's trait, and QuantLib's refusals.

**The fixed values come from a fresh section** (the plan's S7R5 (ii)). QuantLib's interpolation prices with a
section built from a copy of the parameters, which its fit doesn't rebuild after writing the best restart's
parameters back; so where the restarts run out, or Simplex's last trial isn't its best vertex, its values and errors
are its last trial's, while nanoql's, rebuilt, are its best restart's. Each case's "values" are therefore those of a
QuantLib ZabrSmileSection built fresh at the reported parameters (the harness's ZabrSmileSection::volatility), at the
case's "valueStrikes", and its "rmsError" and "maxError" are recomputed by this generator from that section's
volatilities at the smile's strikes, the smile's volatilities and the fit's weights, with QuantLib's formulas
(xabrinterpolation.hpp:245-285: sums by loop, the maximum from QL_MIN_REAL, the plan's §2.11). The rest are QuantLib's
interpolation's: the parameters, γ among them, the weights and the end criterion, which the stale copy doesn't touch.
A factory's fit reports no parameters, so its cases are fits whose last evaluation is their result: one
Levenberg-Marquardt minimization, one accepted at once, or every parameter fixed.

**The stability filter** (the plan's S7R6): every fit is refitted in the harness with its volatilities moved one ulp
up, then down, by math.nextafter, every one together and each alone; an output is written only where every refit
leaves it within 1e-12 × max(|base|, |moved|, 1) (equal, for the end criterion), and the others are dropped: a value
by leaving its strike out of "valueStrikes", a parameter, an error or the end criterion by leaving its key out, the
weights only all together. The generator prints the dropped outputs per case and for the file.

Each fit case's input "fit" is the harness's fit spec (tools/cxx/src/math/interpolations/zabrinterpolation.cpp),
which tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/ZabrInterpolationCxxVectorTests.cs reads back. A refused
fit is {"throws": true}."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

LOGNORMAL, NORMAL = "ShortMaturityLognormal", "ShortMaturityNormal"
QL_MIN_REAL = -sys.float_info.max
TOLERANCE = 1e-12

LM = {"type": "LevenbergMarquardt", "epsfcn": 1e-8, "xtol": 1e-8, "gtol": 1e-8}
SIMPLEX = {"type": "Simplex", "lambda": 0.01}
END = {"maxIterations": 100000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": 1e-8}
# Simplex with γ free runs to its iteration limit: QuantLib's default 60000 takes seconds a fit
SHORT_END = dict(END, maxIterations=1500)

# Smiles whose volatilities are given; ZABR's own smiles are the harness's sections' at their strikes.
GIVEN = {
    "market": (1.0, 0.03, [0.02, 0.025, 0.03, 0.035, 0.04], [0.25, 0.22, 0.2, 0.19, 0.195]),
    "long": (10.0, 0.05, [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.08, 0.1, 0.15],
             [0.3, 0.26, 0.235, 0.22, 0.21, 0.205, 0.2, 0.202, 0.21]),
    "short": (0.5, 0.02, [0.015, 0.02, 0.025], [0.3, 0.27, 0.26]),
}
MODEL_SMILES = {
    "zabr": (5.0, 0.03, [0.01, 0.015, 0.02, 0.03, 0.04, 0.05, 0.07], [0.08, 0.7, 0.2, -0.3, 1.0]),
    "zabrGamma": (1.0, 0.03, [0.015, 0.02, 0.025, 0.03, 0.035, 0.045, 0.06], [0.08, 0.7, 0.2, -0.3, 0.75]),
}


def fit_spec(kernel, smile, alpha=None, beta=None, nu=None, rho=None, gamma=None, fixed=(False,) * 5,
             vega_weighted=True, end_criteria=None, opt_method=None, error_accept=0.002, use_max_error=False,
             max_guesses=50):
    t, f, strikes, vols = smile
    return {"kernel": kernel, "strikes": strikes, "volatilities": vols, "expiry": t, "forward": f, "alpha": alpha,
            "beta": beta, "nu": nu, "rho": rho, "gamma": gamma, "alphaIsFixed": fixed[0], "betaIsFixed": fixed[1],
            "nuIsFixed": fixed[2], "rhoIsFixed": fixed[3], "gammaIsFixed": fixed[4], "vegaWeighted": vega_weighted,
            "endCriteria": end_criteria, "optMethod": opt_method, "errorAccept": error_accept,
            "useMaxError": use_max_error, "maxGuesses": max_guesses}


BG = (False, True, False, False, True)  # beta and gamma fixed, the cube's usual choice
B = (False, True, False, False, False)  # beta fixed, gamma free


def fits(smiles):
    """(label, fit spec) for the fit cases."""
    m, lg, sh, z, zg = (smiles[k] for k in ("market", "long", "short", "zabr", "zabrGamma"))
    out = []
    for kernel in (LOGNORMAL, NORMAL):
        k = "lognormal" if kernel == LOGNORMAL else "normal"
        out += [
            (f"{k} / market / beta and gamma fixed", fit_spec(kernel, m, beta=0.5, gamma=1.0, fixed=BG)),
            (f"{k} / long / beta and gamma fixed", fit_spec(kernel, lg, beta=0.5, gamma=1.0, fixed=BG)),
            (f"{k} / zabr / beta and gamma fixed at the model's", fit_spec(kernel, z, beta=0.7, gamma=1.0, fixed=BG)),
            (f"{k} / zabrGamma / beta and gamma fixed at the model's",
             fit_spec(kernel, zg, beta=0.7, gamma=0.75, fixed=BG)),
            (f"{k} / market / gamma 0.75 fixed", fit_spec(kernel, m, beta=0.5, gamma=0.75, fixed=BG)),
            (f"{k} / market / beta fixed, gamma free", fit_spec(kernel, m, beta=0.5, gamma=1.0, fixed=B)),
            (f"{k} / market / Simplex", fit_spec(kernel, m, beta=0.5, gamma=1.0, fixed=BG, opt_method=SIMPLEX)),
            (f"{k} / market / restarts run out", fit_spec(kernel, m, beta=0.5, gamma=1.0, fixed=BG,
                                                          error_accept=1e-12, max_guesses=4)),
            (f"{k} / market / no vega weights", fit_spec(kernel, m, beta=0.5, gamma=1.0, fixed=BG,
                                                         vega_weighted=False)),
            (f"{k} / short / every parameter fixed", fit_spec(kernel, sh, alpha=0.03, beta=0.5, nu=0.4, rho=-0.1,
                                                              gamma=1.0, fixed=(True,) * 5)),
        ]
    out += [
        ("lognormal / zabr / every parameter free", fit_spec(LOGNORMAL, z)),
        ("lognormal / zabrGamma / beta fixed, gamma free", fit_spec(LOGNORMAL, zg, beta=0.7, gamma=1.0, fixed=B)),
        ("lognormal / long / beta fixed, gamma free", fit_spec(LOGNORMAL, lg, beta=0.5, gamma=1.0, fixed=B)),
        ("lognormal / market / no guesses", fit_spec(LOGNORMAL, m)),
        ("lognormal / market / alpha fixed", fit_spec(LOGNORMAL, m, alpha=0.035, beta=0.5, gamma=1.0,
                                                      fixed=(True, True, False, False, True))),
        ("lognormal / market / nu and rho fixed", fit_spec(LOGNORMAL, m, beta=0.5, nu=0.5, rho=-0.2, gamma=1.0,
                                                           fixed=(False, True, True, True, True))),
        ("lognormal / long / Simplex, gamma free, 1500 iterations", fit_spec(LOGNORMAL, lg, beta=0.5, gamma=1.0,
                                                                             fixed=B, opt_method=SIMPLEX,
                                                                             end_criteria=SHORT_END, max_guesses=3)),
        ("lognormal / long / Simplex, restarts run out", fit_spec(LOGNORMAL, lg, beta=0.5, gamma=1.0, fixed=BG,
                                                                  opt_method=SIMPLEX, error_accept=1e-12,
                                                                  max_guesses=3)),
        ("lognormal / long / restarts accepted after some", fit_spec(LOGNORMAL, lg, beta=0.5, gamma=1.0, fixed=BG,
                                                                     error_accept=0.0035)),
        ("lognormal / market / maximum error", fit_spec(LOGNORMAL, m, beta=0.5, gamma=1.0, fixed=BG,
                                                        use_max_error=True)),
        ("lognormal / market / maxGuesses 0", fit_spec(LOGNORMAL, m, beta=0.5, gamma=1.0, fixed=BG,
                                                       error_accept=1e-12, max_guesses=0)),
        ("lognormal / market / maxGuesses 1", fit_spec(LOGNORMAL, m, beta=0.5, gamma=1.0, fixed=BG,
                                                       error_accept=1e-12, max_guesses=1)),
        ("lognormal / market / end criteria and optimizer given", fit_spec(LOGNORMAL, m, beta=0.5, gamma=1.0,
                                                                           fixed=BG, end_criteria=END,
                                                                           opt_method=LM)),
        ("lognormal / short / beta and gamma fixed", fit_spec(LOGNORMAL, sh, beta=0.5, gamma=1.0, fixed=BG)),
        ("lognormal / zabr / gamma free, beta 1 fixed", fit_spec(LOGNORMAL, z, beta=1.0, gamma=1.0, fixed=B)),
        ("normal / zabrGamma / beta fixed, gamma free", fit_spec(NORMAL, zg, beta=0.7, gamma=1.0, fixed=B)),
        ("normal / long / Simplex, restarts run out", fit_spec(NORMAL, lg, beta=0.5, gamma=1.0, fixed=BG,
                                                               opt_method=SIMPLEX, error_accept=1e-12,
                                                               max_guesses=3)),
    ]
    return out


def factory_fits(smiles):
    m, sh, z = smiles["market"], smiles["short"], smiles["zabr"]
    return [
        ("factory / lognormal / market / one minimization", fit_spec(LOGNORMAL, m, beta=0.5, gamma=1.0, fixed=BG,
                                                                     vega_weighted=False, max_guesses=1)),
        ("factory / normal / short / every parameter fixed", fit_spec(NORMAL, sh, alpha=0.03, beta=0.5, nu=0.4,
                                                                      rho=-0.1, gamma=1.0, fixed=(True,) * 5)),
        ("factory / lognormal / zabr / vega weights, accepted at once",
         fit_spec(LOGNORMAL, z, beta=0.7, gamma=1.0, fixed=BG, error_accept=1.0)),
    ]


def refusals(smiles):
    m = smiles["market"]
    one = (1.0, 0.03, [0.03], [0.2])
    return [
        ("refused / expiry 0", fit_spec(LOGNORMAL, (0.0,) + m[1:], beta=0.5, gamma=1.0, fixed=BG)),
        ("refused / forward negative", fit_spec(LOGNORMAL, (1.0, -0.01) + m[2:], beta=0.5, gamma=1.0, fixed=BG)),
        ("refused / alpha guess 0", fit_spec(LOGNORMAL, m, alpha=0.0, beta=0.5, gamma=1.0, fixed=BG)),
        ("refused / gamma guess negative", fit_spec(NORMAL, m, beta=0.5, gamma=-0.5, fixed=BG)),
        ("refused / beta guess above 1", fit_spec(LOGNORMAL, m, beta=1.5, gamma=1.0, fixed=BG)),
        ("refused / fewer strikes than free parameters", fit_spec(LOGNORMAL, one, beta=0.5, gamma=1.0, fixed=BG)),
    ]


def value_strikes(smile):
    t, f, strikes, _ = smile
    return sorted(set(strikes) | {0.5 * f, 0.9 * f, f, 1.2 * f, 2.0 * f, 3.0 * f, strikes[0] * 0.75})


def moved_sets(vols):
    """The refits' volatilities: every one up, every one down, then each alone up and down."""
    out = [[math.nextafter(x, math.inf) for x in vols], [math.nextafter(x, -math.inf) for x in vols]]
    for i in range(len(vols)):
        for d in (math.inf, -math.inf):
            moved = list(vols)
            moved[i] = math.nextafter(vols[i], d)
            out.append(moved)
    return out


def stable(base, moved):
    if isinstance(base, int) and not isinstance(base, bool):
        return base == moved
    if math.isnan(base) or math.isnan(moved):
        return math.isnan(base) and math.isnan(moved)
    if math.isinf(base) or math.isinf(moved):
        return base == moved
    return abs(base - moved) <= TOLERANCE * max(abs(base), abs(moved), 1.0)


def errors(section_vols, vols, weights):
    """QuantLib's rmsError and maxError (xabrinterpolation.hpp:245-285) at the given section's volatilities."""
    n = len(vols)
    total = 0.0
    for value, y, w in zip(section_vols, vols, weights):
        error = value - y
        total += error * error * w
    rms = math.sqrt(n * total / (1 if n == 1 else n - 1))
    largest = QL_MIN_REAL
    for value, y in zip(section_vols, vols):
        error = abs(value - y)
        largest = error if largest < error else largest
    return rms, largest


SCALARS = ("alpha", "beta", "nu", "rho", "gamma")


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        smiles = dict(GIVEN)
        for name, (t, f, strikes, params) in MODEL_SMILES.items():
            smiles[name] = (t, f, strikes, H.call("ZabrSmileSection::volatility", LOGNORMAL, t, f, params, strikes))

        def fresh(spec, r, points):
            params = [r[p] for p in SCALARS]
            return H.call("ZabrSmileSection::volatility", spec["kernel"], spec["expiry"], spec["forward"], params,
                          points)

        def run(spec, points):
            """A fit's outputs: QuantLib's parameters, weights and end criterion, the fresh section's values at the
            points and its errors over the smile."""
            r = H.call("ZabrInterpolation::ZabrInterpolation", spec, [])
            out = {p: r[p] for p in ("expiry", "forward") + SCALARS}
            rms, largest = errors(fresh(spec, r, spec["strikes"]), spec["volatilities"], r["interpolationWeights"])
            out.update({"rmsError": rms, "maxError": largest, "endCriteria": r["endCriteria"],
                        "interpolationWeights": r["interpolationWeights"], "values": fresh(spec, r, points)})
            return out

        def run_factory(spec, points):
            return {"values": H.call("Zabr::interpolate", spec, points)}

        totals = [0, 0]

        def filtered(label, spec, runner):
            points = value_strikes((spec["expiry"], spec["forward"], spec["strikes"], spec["volatilities"]))
            base = runner(spec, points)
            refits = [runner(dict(spec, volatilities=vols), points) for vols in moved_sets(spec["volatilities"])]
            expected, dropped, total = {}, 0, 0
            for key, value in base.items():
                if key == "values":
                    continue
                if isinstance(value, list):
                    total += len(value)
                    if all(stable(b, r[key][i]) for r in refits for i, b in enumerate(value)):
                        expected[key] = value
                    else:
                        dropped += len(value)
                else:
                    total += 1
                    if all(stable(value, r[key]) for r in refits):
                        expected[key] = value
                    else:
                        dropped += 1
            kept = [i for i in range(len(points)) if all(stable(base["values"][i], r["values"][i]) for r in refits)]
            total += len(points)
            dropped += len(points) - len(kept)
            expected["values"] = [base["values"][i] for i in kept]
            totals[0] += dropped
            totals[1] += total
            print(f"{label}: dropped {dropped} of {total} outputs")
            return {"name": label,
                    "inputs": {"settings": st, "kind": "factory" if runner is run_factory else "fit", "fit": v.enc(spec),
                               "valueStrikes": [points[i] for i in kept]},
                    "expected": v.enc(expected)}

        out = [filtered(label, spec, run) for label, spec in fits(smiles)]
        out += [filtered(label, spec, run_factory) for label, spec in factory_fits(smiles)]
        out.append({"name": "factory trait / global", "inputs": {"settings": st, "kind": "global"},
                    "expected": {"global": H.call("Zabr::global")}})
        for label, spec in refusals(smiles):
            out.append({"name": label, "inputs": {"settings": st, "kind": "refusal", "fit": v.enc(spec)},
                        "expected": {"fit": v.attempt(
                            lambda spec=spec: H.call("ZabrInterpolation::ZabrInterpolation", spec, [])["alpha"])}})
        print(f"dropped {totals[0]} of {totals[1]} outputs")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/zabrinterpolation.hpp", cases)
