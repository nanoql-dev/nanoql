# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Swaption (ql/instruments/swaption.hpp), each output computed by the C++ harness: the
additional results the wheel can't read, Settlement::checkTypeAndMethodConsistency, and the settlement enums' texts.

- "results" cases: 58 swaptions of tools/vectors/gen/instruments/swaption.py, whose functions build them (all but the
  two on a fixed schedule from dates, a schedule spec the harness doesn't read), each priced by the Black and the
  Bachelier engines on a volatility quote, with both cash annuity models: the results timeToExpiry,
  impliedVolatility and spreadCorrection, which the engines set and the wheel has no accessor for, per engine and
  model as [engine][tag]. An expired swaption's results were never set, and a refused pricing refuses them too: each
  is {"throws": true}.
- one "settlement" case: checkTypeAndMethodConsistency over each settlement type (and a value outside the enum) and
  each method (and two outside it), as [type][method], null where the pair passes and {"throws": true} where QuantLib
  refuses it; and the stream texts of Settlement::Type ("Delivery" for Physical, a value outside the enum refused) and
  of Settlement::Method's members, which nanoql's enum names give.

Each "results" case's inputs hold the swaption, the discount curve and the engines, each {"type", "form": "quote",
"vol", "dayCounter", "displacement", "model"} (no displacement for BachelierSwaptionEngine), as
tools/cxx/src/instruments/swaptionspec.hpp reads them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/SwaptionCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_swaption",
                                               os.path.join(v.GEN_ROOT, "instruments", "swaption.py"))
swp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(swp)

TAGS = ["timeToExpiry", "impliedVolatility", "spreadCorrection"]
TYPES = [swp.PHYSICAL, swp.CASH, 2]
METHODS = [swp.OTC, swp.CLEARED, swp.COLLATERALIZED, swp.PAR_YIELD, 4, -1]


def engines_of(black, bachelier):
    """The four quote engines: Black and Bachelier, each with both models."""
    out = []
    for model in (swp.SWAP_RATE, swp.DISCOUNT_CURVE):
        out.append({"type": "BlackSwaptionEngine", "form": "quote", "vol": black["vol"],
                    "dayCounter": black["dayCounter"], "displacement": black["displacement"], "model": model})
        out.append({"type": "BachelierSwaptionEngine", "form": "quote", "vol": bachelier["vol"],
                    "dayCounter": bachelier["dayCounter"], "model": model})
    return out


def results_case(H, name, spec, black, bachelier, settings):
    harness_settings = {"include_reference_date_events": settings.get("include_reference_date_events", False)}
    if "use_at_par_coupons" in settings:
        harness_settings["use_at_par_coupons"] = settings["use_at_par_coupons"]
    with H.case_settings(v.enc(swp.TODAY), **harness_settings) as st:
        engines = engines_of(black, bachelier)
        expected = {"results": [[v.attempt(lambda e=e, t=t: H.call("Swaption::result<Real>", spec, swp.DISCOUNT, e, t))
                                 for t in TAGS] for e in engines]}
        return {"name": "results / " + name,
                "inputs": {"settings": st, "kind": "results", "swaption": spec, "discountCurve": swp.DISCOUNT,
                           "engines": engines, "tags": TAGS},
                "expected": expected}


def settlement_case(H):
    with H.case_settings(v.enc(swp.TODAY)) as st:
        expected = {
            "checkTypeAndMethodConsistency": [
                [v.attempt(lambda t=t, m=m: H.call("Settlement::checkTypeAndMethodConsistency", t, m)) for m in METHODS]
                for t in TYPES],
            "typeText": [v.attempt(lambda t=t: H.call("operator<<(Settlement::Type)", t)) for t in TYPES + [-1]],
            "methodText": [v.attempt(lambda m=m: H.call("operator<<(Settlement::Method)", m)) for m in METHODS[:4]],
        }
        return {"name": "settlement",
                "inputs": {"settings": st, "kind": "settlement", "types": TYPES, "methods": METHODS,
                           "textTypes": TYPES + [-1], "textMethods": METHODS[:4]},
                "expected": expected}


def cases():
    rows = [row for row in swp.swaptions() if "from dates" not in row[0]]
    with cxx.connect() as H:
        out = [results_case(H, *row) for row in rows]
        out.append(settlement_case(H))
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert len(out) == 59, len(out)
    # An expired swaption's results were never set; the spread correction is the spread's.
    assert by_name["results / expired"]["results"][0][0] == v.THROWS
    assert by_name["results / spread 0.001 / 0-0"]["results"][0][2] != 0.0
    settlement = by_name["settlement"]
    assert settlement["typeText"][:2] == ["Delivery", "Cash"]
    assert settlement["checkTypeAndMethodConsistency"][0][3] == v.THROWS
    assert settlement["checkTypeAndMethodConsistency"][2][4] is None


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/swaption.hpp", cases)
