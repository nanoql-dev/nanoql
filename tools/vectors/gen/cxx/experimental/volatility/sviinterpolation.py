# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SviInterpolation and its Svi factory (ql/experimental/volatility/sviinterpolation.hpp), which
the wheel doesn't bind, computed by the C++ harness (tools/cxx/src/experimental/volatility/sviinterpolation.cpp).

Fits on four given smiles (a seven-strike equity skew, a rates smile, a ten-year one, a three-strike one) and on one
SVI's own smile: every parameter free; Simplex; restarts that run out or are accepted after some; without vega weights;
the maximum error; every parameter fixed; m, ρ, σ, a or b fixed where QuantLib's fit completes; maxGuesses 0 and 1; the
end criteria and optimizer given. Each fit's outputs are its expiry, forward, a, b, σ, ρ and m, its root-mean-square and
maximum errors, its end criterion, its weights and its values at the strikes and beyond. The factory's fits give their
values; its trait global. Then QuantLib's refusals: a zero expiry, fewer strikes than free parameters, and each of the
parameters' checks failing at the guesses (a fixed b above 4 among them).

**The values and errors are a fresh SviSmileSection's** at the fit's parameters (the harness's
SviSmileSection::volatility), not the fit's own: QuantLib doesn't rebuild its model instance after writing the best
restart's parameters back, so its values can be its last trial's, where nanoql's are the best's (XabrFit's remarks).

The outputs nanoql fixes are left out: no guess sits at an edge of SVI's inverse transforms
(tests/NanoQL.Tests/Divergences/SviTransformEdgeTests.cs), and a fit with b or a fixed is written only where QuantLib's
completes, so no trial fails its check and nanoql's clamp never acts (SviFixedParameterTests.cs). The generator prints
the fits it leaves out.

**The stability filter**: every fit is refitted with its volatilities moved one ulp up, then down, by math.nextafter,
every one together and each alone; an output is written only where every refit leaves it within 1e-12 × max(|base|,
|moved|, 1) (equal, for the end criterion), and the others are dropped: a value by leaving its strike out of
"valueStrikes", a parameter, an error or the end criterion by leaving its key out, the weights only all together. The
generator prints the dropped outputs per case and for the file.

Each fit case's input "fit" is the harness's fit spec, which
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/SviInterpolationCxxVectorTests.cs reads back. A refused fit is
{"throws": true}."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

QL_MIN_REAL = -sys.float_info.max
TOLERANCE = 1e-12

LM = {"type": "LevenbergMarquardt", "epsfcn": 1e-8, "xtol": 1e-8, "gtol": 1e-8}
SIMPLEX = {"type": "Simplex", "lambda": 0.01}
END = {"maxIterations": 100000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": 1e-8}
SHORT_END = dict(END, maxIterations=1500)

GIVEN = {
    "skew": (1.0, 100.0, [70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0],
             [0.32, 0.28, 0.245, 0.22, 0.205, 0.198, 0.197]),
    "rates": (1.0, 0.03, [0.02, 0.025, 0.03, 0.035, 0.04], [0.25, 0.22, 0.2, 0.19, 0.195]),
    "long": (10.0, 0.05, [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.08, 0.1, 0.15],
             [0.3, 0.26, 0.235, 0.22, 0.21, 0.205, 0.2, 0.202, 0.21]),
    "short": (0.5, 100.0, [90.0, 100.0, 110.0], [0.27, 0.25, 0.24]),
}
MODEL = (2.0, 100.0, [60.0, 75.0, 90.0, 100.0, 110.0, 125.0, 150.0], [0.02, 0.15, 0.3, -0.5, 0.05])
SCALARS = ("a", "b", "sigma", "rho", "m")
LEFT_OUT = []


def fit_spec(smile, a=None, b=None, sigma=None, rho=None, m=None, fixed=(False,) * 5, vega_weighted=True,
             end_criteria=None, opt_method=None, error_accept=0.002, use_max_error=False, max_guesses=50):
    t, f, strikes, vols = smile
    return {"strikes": strikes, "volatilities": vols, "expiry": t, "forward": f, "a": a, "b": b, "sigma": sigma,
            "rho": rho, "m": m, "aIsFixed": fixed[0], "bIsFixed": fixed[1], "sigmaIsFixed": fixed[2],
            "rhoIsFixed": fixed[3], "mIsFixed": fixed[4], "vegaWeighted": vega_weighted, "endCriteria": end_criteria,
            "optMethod": opt_method, "errorAccept": error_accept, "useMaxError": use_max_error,
            "maxGuesses": max_guesses}


A, B, S, R, M = ((True, False, False, False, False), (False, True, False, False, False),
                 (False, False, True, False, False), (False, False, False, True, False),
                 (False, False, False, False, True))


def fits(smiles):
    sk, rt, lg, sh, md = (smiles[k] for k in ("skew", "rates", "long", "short", "model"))
    return [
        ("skew / every parameter free", fit_spec(sk)),
        ("rates / every parameter free", fit_spec(rt)),
        ("long / every parameter free", fit_spec(lg)),
        ("model / every parameter free", fit_spec(md)),
        ("skew / Simplex", fit_spec(sk, opt_method=SIMPLEX, end_criteria=SHORT_END, max_guesses=3)),
        ("skew / restarts run out", fit_spec(sk, error_accept=1e-12, max_guesses=4)),
        ("long / restarts accepted after some", fit_spec(lg, error_accept=0.0035)),
        ("skew / no vega weights", fit_spec(sk, vega_weighted=False)),
        ("skew / maximum error", fit_spec(sk, use_max_error=True)),
        ("short / every parameter fixed", fit_spec(sh, a=0.04, b=0.1, sigma=0.2, rho=-0.3, m=0.0, fixed=(True,) * 5)),
        ("skew / m fixed", fit_spec(sk, m=0.0, fixed=M)),
        ("skew / rho fixed", fit_spec(sk, rho=-0.5, fixed=R)),
        ("skew / sigma fixed", fit_spec(sk, sigma=0.2, fixed=S)),
        ("skew / a fixed", fit_spec(sk, a=0.01, fixed=A)),
        ("skew / b fixed", fit_spec(sk, b=0.2, rho=-0.4, fixed=B)),
        ("model / b and m fixed", fit_spec(md, b=0.15, m=0.05, fixed=(False, True, False, False, True))),
        ("skew / maxGuesses 0", fit_spec(sk, error_accept=1e-12, max_guesses=0)),
        ("skew / maxGuesses 1", fit_spec(sk, error_accept=1e-12, max_guesses=1)),
        ("skew / end criteria and optimizer given", fit_spec(sk, end_criteria=END, opt_method=LM)),
    ]


def factory_fits(smiles):
    sk, sh = smiles["skew"], smiles["short"]
    return [
        ("factory / skew / one minimization", fit_spec(sk, vega_weighted=False, max_guesses=1)),
        ("factory / short / every parameter fixed", fit_spec(sh, a=0.04, b=0.1, sigma=0.2, rho=-0.3, m=0.0,
                                                             fixed=(True,) * 5)),
    ]


def refusals(smiles):
    sk = smiles["skew"]
    return [
        ("refused / expiry 0", fit_spec((0.0,) + sk[1:])),
        ("refused / fewer strikes than free parameters", fit_spec(smiles["short"])),
        ("refused / b guess negative", fit_spec(sk, b=-0.1)),
        ("refused / rho guess at 1", fit_spec(sk, rho=1.0)),
        ("refused / sigma guess 0", fit_spec(sk, sigma=0.0, fixed=S)),
        ("refused / a fixed below its constraint", fit_spec(sk, a=-0.5, b=0.1, sigma=0.1, fixed=(True, True, True,
                                                                                                False, False))),
        ("refused / b fixed above 4", fit_spec(sk, b=4.5, rho=0.0, fixed=B)),
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


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        smiles = dict(GIVEN)
        t, f, strikes, params = MODEL
        smiles["model"] = (t, f, strikes, H.call("SviSmileSection::volatility", t, f, params, strikes))

        def fresh(spec, r, points):
            return H.call("SviSmileSection::volatility", spec["expiry"], spec["forward"], [r[p] for p in SCALARS],
                          points)

        def run(spec, points):
            """A fit's outputs: QuantLib's parameters, weights and end criterion, the fresh section's values at the
            points and its errors over the smile."""
            r = H.call("SviInterpolation::SviInterpolation", spec, [])
            out = {p: r[p] for p in ("expiry", "forward") + SCALARS}
            rms, largest = errors(fresh(spec, r, spec["strikes"]), spec["volatilities"], r["interpolationWeights"])
            out.update({"rmsError": rms, "maxError": largest, "endCriteria": r["endCriteria"],
                        "interpolationWeights": r["interpolationWeights"], "values": fresh(spec, r, points)})
            return out

        def run_factory(spec, points):
            return {"values": H.call("Svi::interpolate", spec, points)}

        totals = [0, 0]

        def filtered(label, spec, runner):
            points = value_strikes((spec["expiry"], spec["forward"], spec["strikes"], spec["volatilities"]))
            try:
                base = runner(spec, points)
            except RuntimeError:
                LEFT_OUT.append(label)
                return None
            refits = []
            for vols in moved_sets(spec["volatilities"]):
                try:
                    refits.append(runner(dict(spec, volatilities=vols), points))
                except RuntimeError:
                    LEFT_OUT.append(f"{label} (a refit refused)")
                    return None
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
                    "inputs": {"settings": st, "kind": "factory" if runner is run_factory else "fit",
                               "fit": v.enc(spec),
                               "valueStrikes": [points[i] for i in kept]},
                    "expected": v.enc(expected)}

        out = [filtered(label, spec, run) for label, spec in fits(smiles)]
        out += [filtered(label, spec, run_factory) for label, spec in factory_fits(smiles)]
        out = [c for c in out if c is not None]
        out.append({"name": "factory trait / global", "inputs": {"settings": st, "kind": "global"},
                    "expected": {"global": H.call("Svi::global")}})
        for label, spec in refusals(smiles):
            out.append({"name": label, "inputs": {"settings": st, "kind": "refusal", "fit": v.enc(spec)},
                        "expected": {"fit": v.attempt(
                            lambda spec=spec: H.call("SviInterpolation::SviInterpolation", spec, [])["a"])}})
        print(f"dropped {totals[0]} of {totals[1]} outputs")
        print(f"left out {len(LEFT_OUT)} fits: {LEFT_OUT}")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/sviinterpolation.hpp", cases)
