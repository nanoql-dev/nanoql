# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Python layer's conformance suite (docs/bindings.md, "Checking the bindings"): every kind both ways, the status
codes as exceptions, ownership, a closed object, generators, overloads, struct defaults, bound engines, the experimental
opt-in, use_library and a user's package. It runs on the test build's packages: nanoql, whose library has the test
exports, and nqlt, the test types' own package over nanoql's (a user's assembly, prefix nqlt):

    nanoql-bind build --rid <rid> --out <lib> --test-exports
    python bindings/python/build.py --library <lib> --test-exports --stage <folder>
    PYTHONPATH=<folder> python -m unittest discover bindings/python/tests
"""

import datetime
import gc
import math
import struct
import threading
import unittest
import warnings

import numpy as np

import nanoql
import nqlt
from nanoql import (
    ArgumentError,
    ArgumentNullError,
    ArgumentOutOfRangeError,
    Compounding,
    ConventionSet,
    FlatForward,
    Frequency,
    HandleError,
    NanoQLError,
    Period,
    TimeUnit,
)
from nqlt import CountingRng, Deprecations, Echo, EchoObject, Fixtures, McShapedEngine, References, Refusals, Tuples


def bits(x: float) -> int:
    return struct.unpack("<q", struct.pack("<d", x))[0]


def collected() -> int:
    gc.collect()
    return nanoql.live_handles()


class Kinds(unittest.TestCase):
    def test_numbers(self):
        self.assertEqual(Echo.double(3.25), 3.25)
        self.assertTrue(math.isnan(Echo.double(float("nan"))))
        self.assertEqual(bits(Echo.double(-0.0)), bits(-0.0))
        self.assertEqual(Echo.double(float("inf")), float("inf"))
        self.assertEqual(Echo.double(2), 2.0)
        self.assertEqual(Echo.int(-(2**31)), -(2**31))
        self.assertEqual(Echo.long(2**63 - 1), 2**63 - 1)
        self.assertIs(Echo.bool(True), True)
        self.assertIs(Echo.bool(False), False)

    def test_strings(self):
        text = "Zürich € \U0001f4c8"
        self.assertEqual(Echo.string(text), text)
        self.assertEqual(Echo.string_length(text), 11)
        self.assertIsNone(Echo.string(None))
        self.assertEqual(Echo.string_length(None), -1)
        self.assertEqual(Echo.string(""), "")

    def test_dates(self):
        self.assertEqual(Echo.date(datetime.date(1901, 1, 1)), datetime.date(1901, 1, 1))
        self.assertEqual(Echo.date(datetime.date(2199, 12, 31)), datetime.date(2199, 12, 31))
        self.assertEqual(Echo.date(datetime.datetime(2026, 10, 30, 15, 0)), datetime.date(2026, 10, 30))
        self.assertIsNone(Echo.opt_date(None))
        self.assertEqual(Echo.opt_date(datetime.date(2023, 3, 15)), datetime.date(2023, 3, 15))

    def test_period_operators(self):
        # Period's operators and equality are nanoql's, not the named tuple's.
        y, m, w = Period(1, TimeUnit.Years), Period(6, TimeUnit.Months), Period(2, TimeUnit.Weeks)
        self.assertTrue(y > m)
        self.assertFalse(y < m)
        self.assertTrue(w < m <= m)
        self.assertEqual(y + m, Period(18, TimeUnit.Months))
        self.assertEqual(y - m, Period(6, TimeUnit.Months))
        self.assertEqual(m * 2, Period(12, TimeUnit.Months))
        self.assertEqual(2 * m, Period(12, TimeUnit.Months))
        self.assertEqual(-m, Period(-6, TimeUnit.Months))
        self.assertEqual(sorted([y, m, w]), [w, m, y])
        self.assertEqual(y, Period(12, TimeUnit.Months))
        self.assertEqual(hash(y), hash(Period(12, TimeUnit.Months)))
        self.assertEqual(hash(Period(1, TimeUnit.Weeks)), hash(Period(7, TimeUnit.Days)))
        self.assertNotEqual(y, m)
        self.assertEqual(len({y, Period(12, TimeUnit.Months), m}), 2)

    def test_periods_enums_complex(self):
        p = Echo.period(Period(6, TimeUnit.Months))
        self.assertEqual(p, Period(6, TimeUnit.Months))
        self.assertIsInstance(p.units, TimeUnit)
        self.assertEqual(Echo.period((1, TimeUnit.Years)), Period(1, TimeUnit.Years))
        self.assertEqual(Echo.opt_period(Period(6, TimeUnit.Months)).length, 6)
        self.assertIsNone(Echo.opt_period(None))
        self.assertIs(Echo.enum(TimeUnit.Years), TimeUnit.Years)
        self.assertIs(Echo.opt_enum(TimeUnit.Weeks), TimeUnit.Weeks)
        self.assertIsNone(Echo.opt_enum(None))
        self.assertEqual(Echo.complex(complex(1.5, -2.25)), complex(1.5, -2.25))

    def test_nullables(self):
        self.assertEqual(Echo.opt_double(2.5), 2.5)
        self.assertIsNone(Echo.opt_double(None))
        self.assertEqual(Echo.opt_int(7), 7)
        self.assertIs(Echo.opt_bool(True), True)
        self.assertIsNone(Echo.opt_bool(None))
        self.assertEqual(Echo.opt_long(2**40 + 3), 2**40 + 3)
        self.assertIsNone(Echo.opt_long(None))
        self.assertEqual(Echo.opt_complex(complex(0.5, -1.0)), complex(0.5, -1.0))
        self.assertIsNone(Echo.opt_complex(None))

    def test_structs(self):
        usd = ConventionSet.quant_lib().currency("USD")
        money = nanoql.Money(usd, 12.5)
        echoed = Echo.money(money)
        self.assertIsInstance(echoed, nanoql.Money)
        self.assertEqual(echoed.value, 12.5)
        self.assertEqual(echoed, money)
        self.assertEqual(Echo.opt_money(money).value, 12.5)
        self.assertIsNone(Echo.opt_money(None))
        with self.assertRaises(ArgumentNullError):
            Echo.money(None)
        with self.assertRaises(HandleError):
            Echo.money(usd)


class Arrays(unittest.TestCase):
    def test_numbers(self):
        out = Echo.doubles([1.0, -0.0, float("nan"), 4.5])
        self.assertIsInstance(out, np.ndarray)
        self.assertEqual(out.dtype, np.float64)
        self.assertEqual(out[0], 1.0)
        self.assertEqual(bits(out[1]), bits(-0.0))
        self.assertTrue(math.isnan(out[2]))
        self.assertEqual(Echo.doubles(np.array([2.0, 3.0]))[1], 3.0)
        self.assertEqual(len(Echo.doubles([])), 0)
        self.assertEqual(Echo.doubles_count(None), -1)
        self.assertEqual(Echo.doubles_count([]), 0)
        self.assertEqual(list(Echo.ints([3, -1, 2])), [3, -1, 2])
        self.assertEqual(list(Echo.bools([True, False, True])), [True, False, True])

    def test_values(self):
        d = [datetime.date(2023, 3, 15), datetime.date(1901, 1, 1), datetime.date(2199, 12, 31)]
        self.assertEqual(Echo.dates(d), d)
        self.assertEqual(Echo.date_set(d), sorted(d))
        self.assertEqual(Echo.opt_doubles([1.0, None, 3.0]), [1.0, None, 3.0])
        self.assertEqual(Echo.periods([Period(1, TimeUnit.Years), (3, TimeUnit.Months)]), [Period(1, TimeUnit.Years), Period(3, TimeUnit.Months)])
        self.assertEqual(Echo.enums([TimeUnit.Days, TimeUnit.Years]), [TimeUnit.Days, TimeUnit.Years])
        self.assertEqual(Echo.strings(["a", "été", ""]), ["a", "été", ""])
        self.assertEqual(Echo.complexes([complex(1, 2), complex(-3, 4)]), [complex(1, 2), complex(-3, 4)])

    def test_jagged_maps_tuples(self):
        rows = Echo.jagged([[1, 2], [], [3, 4, 5, 6]])
        self.assertEqual([len(r) for r in rows], [2, 0, 4])
        self.assertEqual(rows[2][3], 6.0)
        m = Echo.map({datetime.date(2025, 12, 9): 0.02, datetime.date(2023, 3, 15): 0.01})
        self.assertEqual(list(m.items()), [(datetime.date(2023, 3, 15), 0.01), (datetime.date(2025, 12, 9), 0.02)])
        nodes = Echo.nodes([datetime.date(2025, 12, 9), datetime.date(2023, 3, 15)], [0.02, 0.01])
        self.assertEqual(list(nodes.values()), [0.02, 0.01])
        split = Echo.split(3.75)
        self.assertEqual(tuple(split), (3, 0.75))

    def test_matrices_vectors_spans(self):
        t = Echo.transpose([[1, 2, 3], [4, 5, 6]])
        self.assertEqual(t.shape, (3, 2))
        self.assertEqual(t.tolist(), [[1, 4], [2, 5], [3, 6]])
        s = nanoql.PseudoSqrtFunctions.pseudo_sqrt(np.array([[1.0, 0.5], [0.5, 1.0]]), nanoql.SalvagingAlgorithmType.None_)
        self.assertTrue(np.allclose(s @ s.T, [[1.0, 0.5], [0.5, 1.0]], atol=1e-15))
        self.assertEqual(Echo.twice([1.5, -2]).tolist(), [3.0, -4.0])
        span = np.zeros(3)
        Echo.fill(span, 7.5)
        self.assertEqual(span.tolist(), [7.5, 7.5, 7.5])
        with self.assertRaises(TypeError):
            Echo.fill([0.0, 0.0], 1.0)

    def test_in_and_out(self):
        # A vector the member writes into: a line search's step moves the caller's point, as C# moves it.
        point = np.array([0.5, 0.2])
        step = nanoql.BoundaryConstraint(-1.0, 1.0).update(point, [0.1, -0.3], 1.0)
        self.assertEqual(step, 1.0)
        self.assertEqual(point.tolist(), list(References.constraint_update()))
        with self.assertRaises(TypeError):
            nanoql.BoundaryConstraint(-1.0, 1.0).update([0.5, 0.2], [0.1, -0.3], 1.0)

    def test_distribution(self):
        import nanoql.experimental as x

        loss = x.LossDistBinomial(10, 10.0)
        d = loss.evaluate(5, 1.0, 0.3)
        self.assertIsInstance(d, nanoql.Distribution)
        self.assertEqual(len(d.x), 10)
        self.assertAlmostEqual(float(np.sum(d.density * d.dx)), 1.0, places=12)
        self.assertEqual(d.excess[0], 1.0)


class Dispatch(unittest.TestCase):
    """Each collection kind through an overloaded member: the dispatch reads a collection's kind by its head."""

    def test_collections(self):
        self.assertEqual(Echo.total(2.5), 2.5)
        self.assertEqual(Echo.total([1.0, 2.0, 3.5]), 6.5)
        self.assertEqual(Echo.total(np.array([1.0, 2.0])), 3.0)
        self.assertEqual(Echo.total([[1.0, 2.0], [], [4.0]]), 7.0)
        self.assertEqual(Echo.total({datetime.date(2025, 1, 2): 1.5, datetime.date(2026, 1, 2): 2.0}), 3.5)
        self.assertEqual(Echo.total([2**40, 1]), 2**40 + 1)
        self.assertEqual(Echo.total([EchoObject(1.0, "a"), EchoObject(2.5, "b")]), 3.5)
        span = np.array([1.0, -2.0])
        self.assertIsNone(Echo.scale(span, 2.0))
        self.assertEqual(span.tolist(), [2.0, -4.0])
        self.assertEqual(Echo.scale(1.5, 2.0), 3.0)
        self.assertEqual(nanoql.MersenneTwisterUniformRng([1, 2, 3]).type_name, "NanoQL.Numerics.MersenneTwisterUniformRng")


class Defaults(unittest.TestCase):
    """An optional struct left out takes C#'s default(T); its implicit constructor crosses too."""

    def test_piecewise_curve(self):
        qs = ConventionSet.quant_lib()
        helpers = Fixtures.deposit_helpers()
        curve = nanoql.PiecewiseFlatForward.create(0, qs.calendar("TARGET"), helpers, qs.day_counter("Actual365Fixed"), Fixtures.context())
        self.assertEqual(bits(curve.discount(2.0)), bits(References.piecewise_discount()))
        explicit = nanoql.PiecewiseFlatForward.create(0, qs.calendar("TARGET"), helpers, qs.day_counter("Actual365Fixed"), Fixtures.context(),
                                                      i=nanoql.BackwardFlat(), bootstrap=nanoql.IterativeBootstrap())
        self.assertEqual(bits(explicit.discount(2.0)), bits(References.piecewise_discount()))


class Codes(unittest.TestCase):
    def test_each_kind(self):
        kinds = [(1, 1, NanoQLError), (2, 2, ArgumentError), (3, 3, ArgumentNullError), (4, 4, ArgumentOutOfRangeError), (5, 5, NanoQLError),
                 (6, 6, NanoQLError), (7, 7, NanoQLError), (8, 8, NanoQLError), (11, 11, NanoQLError), (12, 12, NanoQLError),
                 (13, 13, NanoQLError), (14, 12, NanoQLError), (15, 13, NanoQLError), (0, 99, NanoQLError)]
        for kind, code, cls in kinds:
            with self.subTest(kind=kind):
                with self.assertRaises(cls) as e:
                    Refusals.throw(kind)
                self.assertEqual(e.exception.code, code)

    def test_parts(self):
        with self.assertRaises(ArgumentError) as e:
            Refusals.throw(2)
        self.assertIsInstance(e.exception, ValueError)
        self.assertEqual(e.exception.param, "x")
        self.assertEqual(e.exception.type_name, "System.ArgumentException")
        self.assertIn("a bad argument", str(e.exception))
        with self.assertRaises(ArgumentOutOfRangeError):
            Refusals.documented_element(5)
        with self.assertRaises(NanoQLError) as e:
            Refusals.undocumented_element(5)
        self.assertEqual(e.exception.code, 99)
        with self.assertRaises(ArgumentNullError):
            FlatForward(datetime.date(2023, 3, 15), 0.03, None)
        with self.assertRaises(ArgumentError) as e:
            ConventionSet.quant_lib().calendar("NoSuchCalendar")
        self.assertEqual(e.exception.param, "name")

    def test_files(self):
        with self.assertRaises(NanoQLError) as e:
            nanoql.ConventionsXml.read("no-such-conventions.xml")
        self.assertEqual(e.exception.code, 12)


class Handles(unittest.TestCase):
    def test_ownership(self):
        live0 = collected()
        a = EchoObject(2.0, "two")
        self.assertEqual(a.type_name, "NanoQL.Bind.TestTypes.EchoObject")
        self.assertIn("two", str(a))
        scaled = a.scaled(3.0)
        self.assertEqual(scaled.value, 6.0)
        self.assertEqual(a.value, 2.0)
        b = EchoObject(2.0, "two")
        self.assertEqual(a, b)
        self.assertEqual(hash(a), hash(b))
        self.assertEqual(a.with_name("three").name, "three")
        self.assertEqual(collected(), live0 + 3)
        del scaled, b
        self.assertEqual(collected(), live0 + 1)
        del a
        self.assertEqual(collected(), live0)

    def test_close(self):
        live0 = collected()
        with EchoObject(1.0, "one") as o:
            self.assertEqual(o.value, 1.0)
        self.assertTrue(o.closed)
        self.assertEqual(nanoql.live_handles(), live0)
        with self.assertRaises(HandleError):
            _ = o.value
        o.close()
        p = EchoObject(4.0, "four")
        with self.assertRaises(HandleError):
            EchoObject.op_equality(o, p)
        p.close()

    def test_refusals(self):
        cal = ConventionSet.quant_lib().calendar("TARGET")
        with self.assertRaises(HandleError) as e:
            EchoObject.scaled(cal, 2.0)
        self.assertEqual((e.exception.code, e.exception.param), (10, "self"))
        objs = Echo.objects([EchoObject(1.0, "a"), EchoObject(2.0, "b")])
        self.assertEqual([o.value for o in objs], [1.0, 2.0])
        self.assertIsInstance(objs[0], EchoObject)

    def test_derived_class(self):
        cal = ConventionSet.quant_lib().calendar("TARGET")
        self.assertIsInstance(cal, nanoql.Calendar)
        self.assertEqual(type(cal).__name__, cal.type_name.rsplit(".", 1)[1])
        self.assertTrue(cal.is_business_day(datetime.date(2026, 10, 30)))


class Generators(unittest.TestCase):
    def test_batches(self):
        live0 = collected()
        mt = nanoql.MersenneTwisterUniformRng(42)
        self.assertEqual(mt.type_name, "NanoQL.Numerics.MersenneTwisterUniformRng")
        v1, w1, n1 = mt.draws(1000)
        v2, _, _ = mt.draws(1000)
        self.assertTrue(np.array_equal(v1, v2))
        self.assertEqual(w1[0], 1.0)
        self.assertIsInstance(n1, nanoql.MersenneTwisterUniformRng)
        everything, _, _ = mt.draws(2000)
        rest, _, _ = n1.draws(1000)
        self.assertTrue(np.array_equal(everything, np.concatenate([v1, rest])))
        with self.assertRaises(ArgumentOutOfRangeError):
            mt.draws(-1)

        results = []

        def draw():
            results.append([mt.draws(1000)[0] for _ in range(20)])

        threads = [threading.Thread(target=draw) for _ in range(2)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()
        self.assertTrue(all(np.array_equal(r, v1) for rs in results for r in rs))

        sobol = nanoql.SobolRsg(5, 1, nanoql.SobolRsgDirectionIntegers.Jaeckel, True)
        values, _, _ = sobol.draws(3)
        self.assertEqual(values.shape, (3, 5))
        self.assertEqual(values[0].tolist(), [0.5] * 5)

        counting = CountingRng(10.0, 0.5)
        v, _, nxt = counting.draws(3)
        self.assertEqual(v.tolist(), [10.0, 10.5, 11.0])
        self.assertEqual(nxt.draws(1)[0].tolist(), [11.5])
        del mt, n1, sobol, counting, nxt
        self.assertEqual(collected(), live0)


class Overloads(unittest.TestCase):
    def test_dispatch(self):
        cal = ConventionSet.quant_lib().calendar("TARGET")
        d = datetime.date(2026, 10, 30)
        self.assertEqual(cal.advance(d, 1, TimeUnit.Days), datetime.date(2026, 11, 2))
        self.assertEqual(cal.advance(d, Period(1, TimeUnit.Weeks)), datetime.date(2026, 11, 6))
        self.assertEqual(cal.advance(d, (1, TimeUnit.Weeks)), datetime.date(2026, 11, 6))
        with self.assertRaises(TypeError):
            cal.advance(d, "a week")

    def test_keywords_and_defaults(self):
        dc = ConventionSet.quant_lib().day_counter("Actual365Fixed")
        a = FlatForward(datetime.date(2023, 3, 15), 0.03, dc)
        b = FlatForward(datetime.date(2023, 3, 15), 0.03, dc, compounding=Compounding.Continuous, frequency=Frequency.Annual)
        self.assertEqual(a.discount(5.0), b.discount(5.0))
        self.assertEqual(a.discount(5.0), math.exp(-0.15))


class Misc(unittest.TestCase):
    def test_versions(self):
        self.assertEqual(nanoql.abi_version(), 2000)
        self.assertEqual(nanoql.version(), "2.0")
        today = nanoql.Date.todays_date()
        self.assertGreater(today, datetime.date(2025, 1, 1))

    def test_deprecated(self):
        with warnings.catch_warnings(record=True) as caught:
            warnings.simplefilter("always")
            self.assertEqual(Deprecations.old(2.0), 2.0)
        self.assertTrue(any(issubclass(w.category, DeprecationWarning) and "use Echo.Double" in str(w.message) for w in caught))

    def test_wheels_deprecated_templates(self):
        # QuantLib-Python's four deprecated names warn with its reasons, naming nanoql's replacements
        qs = ConventionSet.quant_lib()
        dc = qs.day_counter("Actual365Fixed")
        dates = [datetime.date(2024, 1, 15), datetime.date(2025, 1, 15), datetime.date(2026, 1, 15), datetime.date(2027, 1, 15)]
        context = Fixtures.context()
        made = [
            ("LogLinearZeroCurve", "Use ZeroCurve.", lambda: nanoql.LogLinearZeroCurve(dates, [0.03, 0.031, 0.032, 0.033], dc)),
            ("LogCubicZeroCurve", "Use CubicZeroCurve.", lambda: nanoql.LogCubicZeroCurve(dates, [0.03, 0.031, 0.032, 0.033], dc)),
            ("NaturalCubicDiscountCurve", "Use LogCubicDiscountCurve with a natural spline.",
             lambda: nanoql.NaturalCubicDiscountCurve(dates, [1.0, 0.97, 0.94, 0.91], dc)),
            ("PiecewiseSplineCubicDiscount", "Use PiecewiseLogCubicDiscount with a natural spline.",
             lambda: nanoql.PiecewiseSplineCubicDiscount(context.evaluation_date, Fixtures.deposit_helpers(), dc, context)),
            # the closing's Create the class carries, taking an interpolator
            ("PiecewiseSplineCubicDiscount", "Use PiecewiseLogCubicDiscount with a natural spline.",
             lambda: nanoql.PiecewiseSplineCubicDiscount.create(0, qs.calendar("TARGET"), Fixtures.deposit_helpers(), dc, context,
                                                               i=nanoql.Cubic())),
        ]
        for name, replacement, make in made:
            with warnings.catch_warnings(record=True) as caught:
                warnings.simplefilter("always")
                make()
            messages = [str(w.message) for w in caught if issubclass(w.category, DeprecationWarning)]
            self.assertTrue(any(m.startswith(f"QuantLib-Python deprecates {name}: ") and m.endswith(replacement) for m in messages),
                            (name, messages))

    def test_use_library(self):
        path = nanoql.library_path()
        o = EchoObject(1.0, "one")
        with self.assertRaises(RuntimeError):
            nanoql.use_library(path)
        self.assertEqual(o.value, 1.0)
        del o
        if collected() == 0:
            nanoql.use_library(path)
            self.assertEqual(Echo.double(1.5), 1.5)
            with self.assertRaises(OSError):
                nanoql.use_library(path + ".missing")
        self.assertEqual(Echo.double(2.5), 2.5)


class UsersPackage(unittest.TestCase):
    """A user's assembly in a package of its own, its classes extending nanoql's, its objects passed to nanoql's members."""

    def test_classes_extend_nanoqls(self):
        self.assertNotIn("Echo", nanoql.__all__)
        self.assertIn("Echo", nqlt.__all__)
        provider = nqlt.UserProvider()
        self.assertIsInstance(provider, nanoql.ConventionProvider)
        nanoql.ConventionProvider.set_default(provider)
        try:
            with self.assertRaises(NanoQLError) as e:
                ConventionSet.day_counter(None, "Actual365Fixed")
            self.assertEqual(e.exception.code, 5)
        finally:
            nanoql.ConventionProvider.set_default(nanoql.ConventionProvider.quant_lib())
        self.assertEqual(ConventionSet.day_counter(None, "Actual365Fixed").name, "Actual/365 (Fixed)")

    def test_a_class_named_like_nanoqls(self):
        # The user's Calendar beside nanoql's, each keeping its own class.
        mine = nqlt.Calendar("TARGET")
        self.assertIsInstance(mine, nqlt.Calendar)
        self.assertNotIsInstance(mine, nanoql.Calendar)
        cal = ConventionSet.quant_lib().calendar("TARGET")
        self.assertIsInstance(cal, nanoql.Calendar)
        self.assertTrue(cal.is_business_day(datetime.date(2026, 10, 30)))
        theirs = mine.nanoqls()
        self.assertIsInstance(theirs, nanoql.Calendar)
        self.assertEqual(theirs, cal)


class Conventions(unittest.TestCase):
    def test_set(self):
        qs = ConventionSet.quant_lib()
        dc = qs.day_counter("Actual365Fixed")
        self.assertEqual(dc.name, "Actual/365 (Fixed)")
        self.assertIsInstance(dc, nanoql.DayCounter)

    def test_presets_with_arguments(self):
        # Each by its lookup, the group's dispatch picking the form by the arguments; the lookups by name alone too.
        qs, lag = ConventionSet.quant_lib(), Period(3, TimeUnit.Months)
        usd = qs.currency("USD")
        daily = qs.ibor_index("DailyTenorUSDLibor", 2)
        self.assertIsInstance(daily, nanoql.DailyTenorUSDLibor)
        self.assertEqual(daily.fixing_days, 2)
        self.assertEqual(qs.ibor_index("Euribor", Period(6, TimeUnit.Months)).tenor, Period(6, TimeUnit.Months))
        self.assertIsInstance(qs.interest_rate_index("BMAIndex"), nanoql.BMAIndex)
        self.assertIsInstance(qs.zero_inflation_index("AUCPI", Frequency.Quarterly, False), nanoql.AUCPI)
        self.assertIsInstance(qs.yoy_inflation_index("YYAUCPI", Frequency.Quarterly, False), nanoql.YYAUCPI)
        import nanoql.experimental as x

        self.assertIsInstance(qs.zero_inflation_index("GenericCPI", Frequency.Monthly, True, lag, usd), x.GenericCPI)
        self.assertIsInstance(qs.yoy_inflation_index("YYGenericCPI", Frequency.Monthly, True, lag, usd), x.YYGenericCPI)
        self.assertIsInstance(qs.zero_inflation_index("UKRPI"), nanoql.UKRPI)
        self.assertIsInstance(qs.yoy_inflation_index("YYUKRPI"), nanoql.YYUKRPI)
        with self.assertRaises(ArgumentError):
            qs.ibor_index("Euribor6M", 2)


class Engines(unittest.TestCase):
    def test_swap(self):
        live0 = collected()
        ctx, rf = Fixtures.context(), Fixtures.risk_free()
        engine = nanoql.DiscountingSwapEngine.bind(rf)
        self.assertIsInstance(engine, nanoql.BoundEngine)
        swap = Fixtures.swap_builder().with_pricing_engine(engine).to_vanilla_swap(ctx)
        self.assertEqual(swap.fixed_rate, References.swap_fixed_rate())
        heston_engine = nanoql.AnalyticHestonEngine.bind(None, None)
        with self.assertRaises(HandleError) as e:
            Fixtures.swap_builder().with_pricing_engine(heston_engine)
        self.assertEqual(e.exception.code, 10)
        helper = Fixtures.heston_helper().set_pricing_engine(heston_engine)
        self.assertEqual(helper.model_value(Fixtures.heston(), ctx), References.heston_model_value())
        del ctx, rf, engine, swap, heston_engine, helper
        self.assertEqual(collected(), live0)

    def test_calibration_helpers(self):
        ctx, gsr, hw = Fixtures.calibration_context(), Fixtures.gsr(), Fixtures.calibration_hull_white()
        g1d = nanoql.Gaussian1dSwaptionEngine.bind()
        priced = [s.set_pricing_engine(g1d) for s in Fixtures.swaptions()[:3]]
        self.assertGreater(priced[1].model_value(gsr, ctx), 0)
        with self.assertRaises(NanoQLError) as e:
            priced[0].model_value(hw, ctx)
        self.assertEqual(e.exception.code, 6)


class Contract(unittest.TestCase):
    """The contract's later forms: the tuple forms both ways, a bound engine's open nullable inputs, optional parameters of a type
    that doesn't cross, generator bases, each class's own constructor, and statics under their declaring type."""

    def test_tuples_in(self):
        # A tuple as a tuple, None for C#'s default; its elements in their own forms.
        self.assertEqual(tuple(Tuples.single()), (None, None))
        self.assertEqual(tuple(Tuples.single((0.5, None))), (0.5, None))
        self.assertEqual(tuple(Tuples.single(c_point=(None, 0.25))), (None, 0.25))
        self.assertEqual(Tuples.scalars((-3, 2**63 - 1, True)), "-3|9223372036854775807|true")
        self.assertEqual(Tuples.labels(("x\u00e9", datetime.date(2023, 3, 15), Period(1, TimeUnit.Years))), "x\u00e9|45000|1Y")
        self.assertEqual(Tuples.labels((None, datetime.date(2023, 3, 15), (1, TimeUnit.Years))), "null|45000|1Y")
        self.assertEqual(Tuples.kinds((nanoql.Weekday.Friday, complex(1.5, -2.0))), "6|1.5|-2")
        with self.assertRaises(TypeError):
            Tuples.single((0.5,))
        with self.assertRaises(TypeError):
            Tuples.scalars([1, 2, True, 4])

    def test_collections_of_tuples(self):
        live0 = collected()
        a, b = EchoObject(1.0, "one"), EchoObject(2.0, "two")
        rows = Tuples.rows([(a, 0.25), (b, 0.75)])
        self.assertEqual([r.weight for r in rows], [0.25, 0.75])
        self.assertEqual([r.item for r in rows], [a, b])
        self.assertEqual(Tuples.rows([]), [])
        # A nullable element None by its flag; None is a null collection, an empty sequence an empty one.
        self.assertEqual(Tuples.describe([(2.5, 7, datetime.date(2023, 3, 15)), (None, 8, None), (-1.0, None, datetime.date(2023, 3, 16))]),
                         "2.5:7:45000;-:8:-;-1:-:45001")
        self.assertEqual(Tuples.describe(None), "null")
        self.assertEqual(Tuples.describe([]), "")
        with self.assertRaises(TypeError):
            Tuples.describe([(1.0, 2, None), (1.0,)])
        self.assertEqual([tuple(r) for r in Tuples.days([("a", nanoql.Weekday.Monday), (None, nanoql.Weekday.Sunday)])],
                         [("a", nanoql.Weekday.Monday), (None, nanoql.Weekday.Sunday)])
        terms = Tuples.terms([(Period(3, TimeUnit.Months), -(2**63), False), ((2, TimeUnit.Years), 5, True)])
        self.assertEqual((terms[1].tenor, terms[0].count, terms[1].flag), (Period(2, TimeUnit.Years), -(2**63), True))
        numbers = Tuples.numbers([(complex(1, 2), datetime.date(2023, 3, 15)), (complex(-0.0, math.nan), None)])
        self.assertTrue(numbers[0].z == complex(1, 2) and math.isnan(numbers[1].z.imag) and numbers[1].when is None)
        # A returned collection of nullable tuples: None for a null one.
        gaps = Tuples.gaps([(1, 10), (2, 20), (3, 30), (4, 40)], 2)
        self.assertEqual([None if g is None else tuple(g) for g in gaps], [None, (2, 20), None, (4, 40)])
        del a, b, rows
        self.assertEqual(collected(), live0)

    def test_tuple_dispatch(self):
        # The single tuple or the collection by the argument, keywords included.
        single = nanoql.Concentrating1dMesher(0.0, 1.0, 11, (0.5, 0.1), False)
        many = nanoql.Concentrating1dMesher(0.0, 1.0, 11, [(0.3, 0.1, True), (0.7, 0.2, False)], 1e-8)
        keyword = nanoql.Concentrating1dMesher(start=0.0, end=1.0, size=11, c_points=[(0.3, 0.1, True), (0.7, 0.2, False)])
        self.assertEqual(len(single.locations()), 11)
        self.assertEqual(list(many.locations()), list(keyword.locations()))

    def test_bound_nullable_inputs(self):
        ctx, quanto, bsm = Fixtures.context(), Fixtures.quanto(), Fixtures.black_scholes()
        foreign, fxvol = Fixtures.foreign(), Fixtures.exchange_volatility()
        # The steps per year and the tolerance neither bound nor supplied: null, C#'s value for them.
        mc = McShapedEngine.bind(None, 4, None, 2000, None, 42)
        npv = nanoql.QuantoEngine.calculate(quanto, ctx, bsm, foreign, fxvol, 0.3, mc).npv
        self.assertEqual(bits(npv), bits(References.mc_shaped_quanto_npv(False)))
        shaped = McShapedEngine.bind(None, 4, None, 2000, None, 42, [(0.5, 1.5), (1.0, 0.5)])
        npv = nanoql.QuantoEngine.calculate(quanto, ctx, bsm, foreign, fxvol, 0.3, shaped).npv
        self.assertEqual(bits(npv), bits(References.mc_shaped_quanto_npv(True)))
        # A required input left open is still refused.
        with self.assertRaises(HandleError) as e:
            nanoql.QuantoEngine.calculate(quanto, ctx, bsm, foreign, fxvol, 0.3, McShapedEngine.bind(None, 4, None, 2000))
        self.assertEqual(e.exception.code, 10)
        self.assertIn("'seed' is neither bound nor supplied", str(e.exception))

    def test_defaults_left_out(self):
        # BFGS's line search, which doesn't cross, at its default; the calibration set read back as tuples.
        cs = Fixtures.calibration_set()
        self.assertEqual(cs[0]._fields, ("option", "volatility"))
        I, CT = nanoql.AndreasenHugeVolatilityInterplInterpolationType, nanoql.AndreasenHugeVolatilityInterplCalibrationType
        rf, q = Fixtures.risk_free(), Fixtures.dividend()
        ah = nanoql.AndreasenHugeVolatilityInterpl(cs, 100.0, rf, q, I.CubicSpline, CT.Call, 100)
        self.assertEqual(bits(ah.local_vol(1.0, 110.0)), bits(References.andreasen_huge_local_vol(False)))
        ahb = nanoql.AndreasenHugeVolatilityInterpl(cs, 100.0, rf, q, I.CubicSpline, CT.Call, 100, None, None, nanoql.BFGS())
        self.assertEqual(bits(ahb.local_vol(1.0, 110.0)), bits(References.andreasen_huge_local_vol(True)))

    def test_generator_base(self):
        uniform = nanoql.MersenneTwisterUniformRsg(3, 42)
        gaussian = nanoql.InvCumulativeMersenneTwisterGaussianRsg(uniform)
        values, _, _ = gaussian.draws(4)
        self.assertEqual(np.asarray(values).shape, (4, 3))
        self.assertEqual([bits(x) for x in np.asarray(values).ravel()], [bits(x) for x in References.gaussian_draws()])
        with self.assertRaises(HandleError) as e:
            nanoql.InvCumulativeMersenneTwisterGaussianRsg(nanoql.KnuthUniformRsg(3, 42))
        self.assertEqual(e.exception.code, 10)

    def test_own_constructors(self):
        # A preset's refusal names its lookup; a class with no crossing constructor refuses, never builds its base.
        with self.assertRaises(TypeError) as e:
            nanoql.USDCurrency()
        self.assertEqual(str(e.exception), "USDCurrency is made by ConventionSet.currency")
        with self.assertRaises(TypeError) as e:
            nanoql.EuriborSwapIsdaFixA("Fake", Period(10, TimeUnit.Years), 2, nanoql.EURCurrency, None, Period(1, TimeUnit.Years), 0, None, None)
        self.assertIn("EuriborSwapIsdaFixA is made by ConventionSet.", str(e.exception))
        self.assertIsInstance(ConventionSet.quant_lib().currency("USD"), nanoql.USDCurrency)
        with self.assertRaises(TypeError) as e:
            nanoql.YieldTermStructure()
        self.assertIn("has no constructor that crosses the C ABI", str(e.exception))

    def test_statics_under_their_declaring_type(self):
        import nanoql.experimental as x

        # The named latent model reaches its base's factory through Python's inheritance, with no copy of its own.
        self.assertNotIn("from_correlation", vars(x.GaussianDefProbLM))
        model = x.GaussianDefProbLM.from_correlation(0.3, 2)
        self.assertEqual(type(model).__name__, "LatentModel_GaussianCopulaPolicy_GaussianCopulaPolicyInitTraits")


class Experimental(unittest.TestCase):
    def test_opt_in(self):
        import nanoql.experimental as x

        import nqlt.experimental  # noqa: F401 - the test types' experimental members, a user's opt-in

        # A user's class over nanoql's experimental interface: experimental, not in nqlt's stable module.
        self.assertNotIn("Walk", nqlt.__all__)
        self.assertIsInstance(nqlt.experimental.Walk(), x.IRandomWalkDistribution)
        # A stable class's experimental member attached on the opt-in, then a nanoql enum default read.
        self.assertIsInstance(nqlt.Calendar.make_walk(), nqlt.experimental.Walk)
        self.assertEqual(nqlt.experimental.Walk.per_year(), 1)
        # A stable class's experimental constructor added on the opt-in beside its stable one.
        self.assertEqual(nqlt.Calendar(nqlt.experimental.Walk()).name, "walk")
        self.assertEqual(nqlt.Calendar("TARGET").name, "TARGET")
        self.assertTrue(hasattr(x, "__all__") and len(x.__all__) > 100)
        self.assertIsInstance(Fixtures.callable_bond(), x.CallableFixedRateBond)

    def test_hints_resolve(self):
        import typing

        import nanoql.experimental  # noqa: F401
        import nqlt.experimental

        # Every annotated function of a user's two modules resolves its hints.
        resolved = 0
        for module in (nqlt._generated, nqlt.experimental):
            for cls in [v for v in vars(module).values() if isinstance(v, type) and v.__module__ == module.__name__]:
                for member in vars(cls).values():
                    f = member.__func__ if isinstance(member, staticmethod) else member.fget if isinstance(member, property) else member
                    if callable(f) and getattr(f, "__annotations__", None):
                        typing.get_type_hints(f)
                        resolved += 1
        self.assertGreater(resolved, 50)


if __name__ == "__main__":
    unittest.main()
