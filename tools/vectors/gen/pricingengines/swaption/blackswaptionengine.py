# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackSwaptionEngine and BachelierSwaptionEngine
(ql/pricingengines/swaption/blackswaptionengine.hpp), as the wheel binds them: on a volatility quote and on a swaption
volatility structure, each with both cash annuity models. 30 swaptions on the evaluation date 15 January 2025, and 2
refused engines.

The swaptions are those of instruments/swaption.py's kind: payers and receivers, from 6M to 5Y options on 1Y to 10Y
swaps, struck from 2% to 4%, over the four consistent settlement pairs, on Euribor 6M vanilla swaps and ESTR
overnight-indexed swaps, with floating spreads. Each is priced by eight engines: the Black engine on a quote (with a
displacement on every third swaption) and on a shifted lognormal structure (a ConstantSwaptionVolatility with a fixed
reference date, or a SwaptionVolatilityMatrix of swaptionvolmatrix.py, flat extrapolated, with shifts), the Bachelier
engine on a quote and on a normal structure (a constant or a matrix one), each with the SwapRate and the DiscountCurve
models. The Bachelier engine on a quote passes its model as the volatility's displacement and keeps the DiscountCurve
model (QuantLib's own behaviour, which nanoql keeps), so its two models price alike. A swaption whose exercise is past
a matrix's last option date is refused by that engine. The refused engines: the Black engine on a normal structure,
"BlackSwaptionEngine requires (shifted) lognormal input volatility", and the Bachelier engine on a lognormal one.

Each swaption's outputs are, per engine (its prefix: "blackQuote", "blackStructure", "bachelierQuote" or
"bachelierStructure", then "SwapRate" or "DiscountCurve"), the NPV and the results the wheel reads: vega, delta,
annuity, forwardPrice, strike, atmForward, swapLength and stdDev, a refused pricing each output's {"throws": true}.
Amounts are compared at the nominal. A refused engine's case has the one output "engine".

Each case's inputs hold the swaption (instruments/swaption.py's spec), the discount curve, the quote engines' specs
("blackQuote", "bachelierQuote", without the model) and the structures ("blackStructure", "bachelierStructure"): a
ConstantSwaptionVolatility spec of swaptionconstantvol.py or a matrix spec of swaptionvolmatrix.py. The wheel's
engines are the proxies of the C++ constructors on a quote handle and on a structure handle. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BlackSwaptionEngineVectorTests.cs."""
import importlib.util
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


swp = _load("wheel_swaption", "instruments/swaption.py")
svm = _load("wheel_swaptionvolmatrix", "termstructures/volatility/swaption/swaptionvolmatrix.py")
scv = svm.scv
fvf, irx = swp.fvf, swp.irx

TODAY = swp.TODAY
M, Y = ql.Months, ql.Years
MODELS = [("SwapRate", swp.SWAP_RATE), ("DiscountCurve", swp.DISCOUNT_CURVE)]

LOGNORMAL_CONSTANT = scv.fixed_spec(TODAY, irx.TARGET, ql.Following, 0.22, irx.A365, scv.SL, 0.005)
NORMAL_CONSTANT = scv.fixed_spec(TODAY, irx.TARGET, ql.Following, 0.0085, irx.A365, scv.N)
SMALL_OPTIONS = [[3, M], [1, Y], [2, Y], [5, Y]]
SMALL_SWAPS = [[1, Y], [2, Y], [5, Y], [10, Y]]
LOGNORMAL_VOLS = [[0.30, 0.28, 0.25, 0.22], [0.28, 0.27, 0.24, 0.21], [0.26, 0.25, 0.23, 0.20],
                  [0.22, 0.21, 0.20, 0.19]]
NORMAL_VOLS = [[0.0070, 0.0074, 0.0078, 0.0080], [0.0072, 0.0076, 0.0079, 0.0081], [0.0075, 0.0078, 0.0080, 0.0082],
               [0.0077, 0.0079, 0.0081, 0.0083]]
LOGNORMAL_MATRIX = svm.tenors_spec(TODAY, irx.TARGET, ql.ModifiedFollowing, SMALL_OPTIONS, SMALL_SWAPS, LOGNORMAL_VOLS,
                                   irx.A365, True, scv.SL, svm.shifted(LOGNORMAL_VOLS, 0.004, 0.0005))
NORMAL_MATRIX = svm.tenors_spec(TODAY, irx.TARGET, ql.ModifiedFollowing, SMALL_OPTIONS, SMALL_SWAPS, NORMAL_VOLS,
                                irx.A365, False, scv.N)


def make_structure(spec):
    return svm.make_matrix(spec) if "form" in spec else scv.make_surface(spec)


def swaptions():
    """(name, swaption spec, black quote, bachelier quote, black structure, bachelier structure) for the 30."""
    out = []
    pairs = [(swp.PHYSICAL, swp.OTC), (swp.CASH, swp.PAR_YIELD), (swp.CASH, swp.COLLATERALIZED),
             (swp.PHYSICAL, swp.CLEARED)]
    k = 0
    for exercise_tenor, length in [((6, M), (2, Y)), ((1, Y), (1, Y)), ((1, Y), (5, Y)), ((2, Y), (10, Y)),
                                   ((5, Y), (5, Y)), ((3, Y), (2, Y))]:
        exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
        for swap_type, strike in [(swp.PAYER, 0.02), (swp.RECEIVER, 0.03), (swp.PAYER, 0.04), (swp.RECEIVER, 0.025)]:
            st, sm = pairs[k % 4]
            nominal = [1e6, 2.5e6, 1e7][k % 3]
            spec = swp.swaption_spec(swp.vanilla(swap_type, nominal, strike, start, maturity), swp.european(exercise),
                                     st, sm)
            black = {"vol": 0.15 + 0.01 * (k % 9), "dayCounter": irx.A365,
                     "displacement": 0.01 if k % 3 == 0 else 0.0}
            bachelier = {"vol": 0.006 + 0.0003 * (k % 7), "dayCounter": irx.A365}
            matrix = k % 2 == 1
            out.append((f"{exercise_tenor[0]}{'MY'[exercise_tenor[1] == Y]} on {length[0]}Y / {swap_type} / "
                        f"{strike} / {st}-{sm} / {k}", spec, black, bachelier,
                        LOGNORMAL_MATRIX if matrix else LOGNORMAL_CONSTANT, NORMAL_MATRIX if matrix else NORMAL_CONSTANT))
            k += 1
    # 24 so far.
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    quote = {"vol": 0.2, "dayCounter": irx.A365, "displacement": 0.0}
    normal = {"vol": 0.008, "dayCounter": irx.A365}
    for st, sm in [(swp.PHYSICAL, swp.OTC), (swp.CASH, swp.PAR_YIELD)]:
        out.append((f"overnight-indexed / {st}-{sm}", swp.swaption_spec(
            swp.overnight(swp.PAYER, 1e6, 0.03, start, maturity), swp.european(exercise), st, sm), quote, normal,
            LOGNORMAL_CONSTANT, NORMAL_MATRIX))
    for spread, st, sm in [(0.002, swp.CASH, swp.PAR_YIELD), (-0.0015, swp.PHYSICAL, swp.OTC)]:
        out.append((f"spread {spread} / {st}-{sm}", swp.swaption_spec(
            swp.vanilla(swp.RECEIVER, 1e6, 0.03, start, maturity, spread=spread), swp.european(exercise), st, sm),
            quote, normal, LOGNORMAL_MATRIX, NORMAL_CONSTANT))
    late, late_start, late_maturity = swp.dates_of(ql.Period(7, Y), ql.Period(5, Y))
    out.append(("past the matrix's last option date", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, late_start, late_maturity), swp.european(late)), quote, normal,
        LOGNORMAL_MATRIX, NORMAL_MATRIX))
    long_e, long_s, long_m = swp.dates_of(ql.Period(2, Y), ql.Period(15, Y))
    out.append(("swap past the matrix's last swap tenor", swp.swaption_spec(
        swp.vanilla(swp.RECEIVER, 1e6, 0.03, long_s, long_m), swp.european(long_e), swp.CASH, swp.PAR_YIELD),
        quote, normal, LOGNORMAL_MATRIX, NORMAL_MATRIX))
    assert len(out) == 30, len(out)
    return out


def engines(curve, black, bachelier, black_structure, bachelier_structure):
    """(prefix, engine) for the eight engines."""
    h = irx.handle(curve)
    out = []
    for name, model in MODELS:
        out.append(("blackQuote" + name, ql.BlackSwaptionEngine(
            h, ql.QuoteHandle(ql.SimpleQuote(black["vol"])), irx.day_counter(black["dayCounter"]),
            black["displacement"], model)))
        out.append(("blackStructure" + name, ql.BlackSwaptionEngine(
            h, ql.SwaptionVolatilityStructureHandle(make_structure(black_structure)), model)))
        out.append(("bachelierQuote" + name, ql.BachelierSwaptionEngine(
            h, ql.QuoteHandle(ql.SimpleQuote(bachelier["vol"])), irx.day_counter(bachelier["dayCounter"]), model)))
        out.append(("bachelierStructure" + name, ql.BachelierSwaptionEngine(
            h, ql.SwaptionVolatilityStructureHandle(make_structure(bachelier_structure)), model)))
    return out


def swaption_case(name, spec, black, bachelier, black_structure, bachelier_structure):
    with v.case_settings(TODAY) as st:
        swaption = swp.make_swaption(spec)
        scale = fvf.scale_of(spec["swap"])
        expected = {}
        for prefix, engine in engines(swp.DISCOUNT, black, bachelier, black_structure, bachelier_structure):
            swaption.setPricingEngine(engine)
            expected.update(swp.priced(swaption, scale, prefix))
        return {"name": name,
                "inputs": {"settings": st, "swaption": spec, "discountCurve": swp.DISCOUNT, "blackQuote": black,
                           "bachelierQuote": bachelier, "blackStructure": black_structure,
                           "bachelierStructure": bachelier_structure},
                "expected": expected}


def refused_case(name, engine_type, structure):
    with v.case_settings(TODAY) as st:
        def build():
            h = irx.handle(swp.DISCOUNT)
            s = ql.SwaptionVolatilityStructureHandle(make_structure(structure))
            return (ql.BlackSwaptionEngine(h, s) if engine_type == "black" else ql.BachelierSwaptionEngine(h, s)), None
        return {"name": name, "inputs": {"settings": st, "engine": engine_type, "structure": structure},
                "expected": {"engine": v.attempt(lambda: build()[1])}}


def cases():
    out = [swaption_case(*row) for row in swaptions()]
    out.append(refused_case("Black engine on a normal structure", "black", NORMAL_MATRIX))
    out.append(refused_case("Bachelier engine on a lognormal structure", "bachelier", LOGNORMAL_CONSTANT))
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for c in out[-2:]:
        assert c["expected"]["engine"] == v.THROWS, c["name"]
    # The Bachelier engine on a quote ignores its model (its constructor passes it as the displacement).
    cash = by_name["overnight-indexed / 1-3"]
    assert cash["bachelierQuoteSwapRateNPV"] == cash["bachelierQuoteDiscountCurveNPV"]
    assert cash["blackQuoteSwapRateNPV"] != cash["blackQuoteDiscountCurveNPV"]
    late = by_name["past the matrix's last option date"]
    assert late["blackStructureSwapRateNPV"] == v.THROWS and late["blackQuoteSwapRateNPV"] != v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/blackswaptionengine.hpp", cases)
