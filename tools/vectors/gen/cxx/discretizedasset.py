# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's discretized assets (ql/discretizedasset.hpp, .cpp) rolled back on Hull-White's fitted tree,
and the tree lattice members they run through (ql/methods/lattices/lattice.hpp, lattice1d.hpp), which the wheel
doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

Six Hull-White trees (tools/cxx/src/models/modelspec.hpp's lattices): flat and log-linear discount curves, mean
reversions from 1e-9 (below sqrt(QL_EPSILON)) to 0.5, uniform and non-uniform grids, one with a step below the dx
floor's threshold, and a seventh, the first with a spread (setSpread). On each tree:
- a DiscretizedDiscountBond initialized at the grid's last time, then at a time inside it, each rolled back in turn
  to grid times down to 0: rollback's and partialRollback's times and values after each, and the present value after
  each rollback (TreeLattice::presentValue, from the state prices at the asset's time);
- DiscretizedOption over a DiscretizedDiscountBond, QuantLib's own class, the bond initialized at the last time and
  the option at it or before it: European, Bermudan (with a negative exercise time, which mandatoryTimes and the
  exercise condition skip) and American, the option's and the bond's times and values after each rollback and
  partialRollback, the option's present values, and its mandatory times;
- TreeLattice::statePrices(i) at five steps, the last among them, and TreeLattice1D::grid(t) at three times.
Then the refusals, on the first tree: a rollback forward ("cannot roll the asset back to"), to a time off the grid,
an initialization off the grid, grid(t) off the grid, an option whose bond was initialized on another tree built from
the same spec or never ("option and underlying were initialized on different methods"), an option initialized after
its bond's time (the bond's partial rollback forward), and an exercise type outside the enum ("invalid exercise
type", which nanoql refuses at construction).

An asset argument is the harness's spec (tools/cxx/src/discretizedasset.cpp): {"type": "DiscretizedDiscountBond"}
or {"type": "DiscretizedOption", "underlyingTime", "otherLattice", "exerciseType", "exerciseTimes"}. Read back by
tests/NanoQL.Tests/Vectors/Cxx/DiscretizedAssetCxxVectorTests.cs, with tests/NanoQL.Tests/Vectors/ModelInputs.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
COMPOUNDED, CONTINUOUS = 1, 2
ANNUAL, SEMIANNUAL = 1, 2
REFERENCE = 46311  # Friday 16 October 2026


def flat(forward, dc=A365, compounding=CONTINUOUS, frequency=ANNUAL):
    return {"type": "FlatForward", "referenceDate": REFERENCE, "forward": forward, "dayCounter": dc,
            "compounding": compounding, "frequency": frequency}


def discount_curve(days, discounts, dc=A365):
    return {"type": "DiscountCurve", "dates": [REFERENCE + d for d in days], "discounts": discounts, "dayCounter": dc}


def hull_white(curve, a, sigma):
    return {"type": "HullWhite", "curve": curve, "a": a, "sigma": sigma, "params": None}


def lattice(model, grid, spread=None):
    return {"model": model, "timeGrid": grid, "spread": spread}


def bond():
    return {"type": "DiscretizedDiscountBond"}


def option(exercise_type, times, underlying_time, other_lattice=False):
    return {"type": "DiscretizedOption", "underlyingTime": underlying_time, "otherLattice": other_lattice,
            "exerciseType": exercise_type, "exerciseTimes": times}


AMERICAN, BERMUDAN, EUROPEAN = 0, 1, 2

UPWARD = discount_curve([0, 365, 730, 1461, 2557, 3652], [1.0, 0.98, 0.955, 0.9, 0.82, 0.74])
NEGATIVE = discount_curve([0, 182, 365, 730, 1461, 2557], [1.0, 1.002, 1.003, 1.001, 0.99, 0.95])

LATTICES = [
    ("flat 4%, uniform", lattice(hull_white(flat(0.04), 0.1, 0.01), {"end": 5.0, "steps": 20})),
    ("flat 3% Actual/360 annual, non-uniform",
     lattice(hull_white(flat(0.03, A360, COMPOUNDED, ANNUAL), 0.05, 0.015),
             {"times": [0.25, 0.2525, 1.0, 1.003, 3.0, 5.0], "steps": 20})),
    ("upward discount curve, mandatory times with steps",
     lattice(hull_white(UPWARD, 0.2, 0.008), {"times": [1.0, 2.0, 3.0, 4.0], "steps": 16})),
    ("flat 2%, reversion 1e-9", lattice(hull_white(flat(0.02), 1e-9, 0.01), {"end": 4.0, "steps": 12})),
    ("negative rates, a floored step",
     lattice(hull_white(NEGATIVE, 0.5, 0.02), {"times": [0.5, 1.0, 1.0099, 2.0, 3.0]})),
    ("flat 5% semiannual, coarse", lattice(hull_white(flat(0.05, A365, COMPOUNDED, SEMIANNUAL), 0.3, 0.012),
                                           {"end": 3.0, "steps": 6})),
    ("flat 4%, uniform, spread 0.002",
     lattice(hull_white(flat(0.04), 0.1, 0.01), {"end": 5.0, "steps": 20}, 0.002)),
]


def cases():
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        out = []

        def grid_times(spec):
            g = spec["timeGrid"]
            return [H.call("TimeGrid::at", g, i) for i in range(H.call("TimeGrid::size", g))]

        def rolled(method, lat, asset, t0, times):
            r = H.call(method, lat, asset, t0, times)
            keys = ["times", "values"] + (["underlyingTimes", "underlyingValues"] if "underlyingTimes" in r else [])
            return {k: r[k] for k in keys}

        def asset_case(name, lat, asset, t0, times):
            expected = {}
            for key, method in (("rollback", "DiscretizedAsset::rollback"),
                                ("partialRollback", "DiscretizedAsset::partialRollback")):
                for k, x in rolled(method, lat, asset, t0, times).items():
                    expected[key + k[0].upper() + k[1:]] = x
            expected["presentValue"] = H.call("DiscretizedAsset::presentValue", lat, asset, t0, times)
            expected["mandatoryTimes"] = H.call("DiscretizedAsset::mandatoryTimes", asset)
            return {"name": name, "inputs": {"settings": st, "lattice": lat, "asset": asset, "t0": t0,
                                             "times": times},
                    "expected": expected}

        for name, lat in LATTICES:
            ts = grid_times(lat)
            n = len(ts) - 1
            last, mid = ts[n], ts[n // 2]
            down = [ts[n - 1], ts[(3 * n) // 4], mid, ts[1], 0.0]
            out.append(asset_case(f"{name}: bond from the last time", lat, bond(), last, down))
            out.append(asset_case(f"{name}: bond from the middle", lat, bond(), mid, [ts[n // 4], 0.0]))
            ex = [ts[(3 * n) // 4]]
            out.append(asset_case(f"{name}: European option", lat, option(EUROPEAN, ex, last), last, down))
            berm = [-0.5, ts[n // 4], mid, ts[(3 * n) // 4]]
            out.append(asset_case(f"{name}: Bermudan option", lat, option(BERMUDAN, berm, last), last, down))
            out.append(asset_case(f"{name}: American option", lat, option(AMERICAN, [ts[n // 4], ts[(3 * n) // 4]],
                                                                          last), last, down))
            out.append(asset_case(f"{name}: Bermudan option from the middle", lat,
                                  option(BERMUDAN, [ts[1], ts[n // 4], mid], last), mid, [ts[n // 4], ts[1], 0.0]))
            steps = sorted({0, 1, n // 2, n - 1, n})
            grid_at = [0.0, mid, last]
            out.append({"name": f"{name}: state prices and grid",
                        "inputs": {"settings": st, "lattice": lat, "steps": steps, "gridTimes": grid_at},
                        "expected": {"statePrices": [H.call("TreeLattice::statePrices", lat, i) for i in steps],
                                     "grid": [H.call("TreeLattice1D::grid", lat, t) for t in grid_at]}})

        lat = LATTICES[0][1]
        ts = grid_times(lat)
        n = len(ts) - 1
        off = 0.5 * (ts[3] + ts[4])
        refusals = [
            ("a rollback forward", bond(), ts[n], [ts[10], ts[12]]),
            ("a rollback to a time off the grid", bond(), ts[n], [off]),
            ("an initialization off the grid", bond(), off, [0.0]),
            ("an option whose bond is on another tree", option(EUROPEAN, [ts[5]], ts[n], True), ts[n], [0.0]),
            ("an option whose bond was never initialized", option(EUROPEAN, [ts[5]], None), ts[n], [0.0]),
            ("an option after its bond's time", option(EUROPEAN, [ts[5]], ts[10]), ts[n], [0.0]),
            ("an exercise type outside the enum", option(3, [ts[5]], ts[n]), ts[n], [0.0]),
        ]
        for name, asset, t0, times in refusals:
            out.append({"name": "refused: " + name,
                        "inputs": {"settings": st, "lattice": lat, "asset": asset, "t0": t0, "times": times},
                        "expected": {"rollback": v.attempt(
                            lambda: H.call("DiscretizedAsset::rollback", lat, asset, t0, times))}})
        out.append({"name": "refused: grid off the grid",
                    "inputs": {"settings": st, "lattice": lat, "steps": [], "gridTimes": [off]},
                    "expected": {"grid": [v.attempt(lambda: H.call("TreeLattice1D::grid", lat, off))]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/discretizedasset.hpp", cases)
