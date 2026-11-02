// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertNotSame;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import org.junit.jupiter.api.Test;
import org.nanoql.bind.testtypes.Echo;
import org.nanoql.bind.testtypes.EchoObject;
import org.nanoql.bind.testtypes.Fixtures;
import org.nanoql.bind.testtypes.McShapedEngine;
import org.nanoql.bind.testtypes.References;
import org.nanoql.bind.testtypes.Refusals;
import org.nanoql.bind.testtypes.Tuples;
import org.nanoql.experimental.GaussianDefProbLM;
import org.nanoql.experimental.LatentModel_GaussianCopulaPolicy_GaussianCopulaPolicyInitTraits;
import org.nanoql.extensions.conventions.ConventionSet;
import org.nanoql.instruments.VanillaOption;
import org.nanoql.models.BlackCalibrationHelper;
import org.nanoql.models.CalibratedModel;
import org.nanoql.models.CalibratedModelExtensions;
import org.nanoql.models.CalibrationHelper;
import org.nanoql.models.CapHelper;
import org.nanoql.models.Gsr;
import org.nanoql.models.HestonModel;
import org.nanoql.models.HullWhite;
import org.nanoql.models.MarkovFunctionalModelSettings;
import org.nanoql.models.MarkovFunctionalModelSettingsAdjustments;
import org.nanoql.models.ICalibratedModel;
import org.nanoql.models.SwaptionHelper;
import org.nanoql.numerics.BackwardFlat;
import org.nanoql.numerics.BFGS;
import org.nanoql.numerics.BoundaryConstraint;
import org.nanoql.numerics.InvCumulativeMersenneTwisterGaussianRsg;
import org.nanoql.numerics.KnuthUniformRsg;
import org.nanoql.numerics.MersenneTwisterUniformRng;
import org.nanoql.numerics.MersenneTwisterUniformRsg;
import org.nanoql.numerics.SobolRsg;
import org.nanoql.numerics.SobolRsgDirectionIntegers;
import org.nanoql.pricingengines.AnalyticCapFloorEngine;
import org.nanoql.pricingengines.AnalyticHestonEngine;
import org.nanoql.pricingengines.DiscountingSwapEngine;
import org.nanoql.pricingengines.Gaussian1dSwaptionEngine;
import org.nanoql.pricingengines.QuantoEngine;
import org.nanoql.termstructures.AndreasenHugeVolatilityInterpl;
import org.nanoql.termstructures.AndreasenHugeVolatilityInterplCalibrationType;
import org.nanoql.termstructures.AndreasenHugeVolatilityInterplInterpolationType;
import org.nanoql.termstructures.FlatForward;
import org.nanoql.termstructures.IterativeBootstrap;
import org.nanoql.termstructures.PiecewiseFlatForward;
import org.nanoql.time.Calendar;
import org.nanoql.time.DayCounter;
import org.nanoql.time.Frequency;
import org.nanoql.time.Period;
import org.nanoql.time.TimeUnit;
import org.nanoql.time.Weekday;

/**
 * The Java layer's conformance suite (docs/bindings.md, "Checking the bindings"): every kind both ways, the status codes
 * as exceptions, closing and the cleaner, the loaded library, null objects, an object as another class, flags enums'
 * combinations, overloads and telescoped defaults, interfaces, generators across threads, bound engines, calibrations
 * to C#'s bits, the experimental classes, and a user's classes extending nanoql's from a package beside it: the test
 * types' layer, compiled with these tests against nanoql's main classes over its own jextract class. It runs on the
 * test build (its library through {@code nanoql.library}, jextract's layer over its {@code nqlt.h}).
 */
class ConformanceTest {
    private static long bits(double x) {
        return Double.doubleToRawLongBits(x);
    }

    @Test
    void kinds() {
        assertEquals(3.25, Echo.double_(3.25));
        assertTrue(Double.isNaN(Echo.double_(Double.NaN)));
        assertEquals(bits(-0.0), bits(Echo.double_(-0.0)));
        assertEquals(Integer.MIN_VALUE, Echo.int_(Integer.MIN_VALUE));
        assertEquals(Long.MAX_VALUE, Echo.long_(Long.MAX_VALUE));
        assertTrue(Echo.bool(true));
        String text = "Zürich € 📈";
        assertEquals(text, Echo.string(text));
        assertEquals(11, Echo.stringLength(text));
        assertNull(Echo.string(null));
        assertEquals(-1, Echo.stringLength(null));
        assertEquals(LocalDate.of(1901, 1, 1), Echo.date(LocalDate.of(1901, 1, 1)));
        assertEquals(LocalDate.of(2199, 12, 31), Echo.date(LocalDate.of(2199, 12, 31)));
        assertNull(Echo.optDate(null));
        assertEquals(new Period(6, TimeUnit.Months), Echo.period(new Period(6, TimeUnit.Months)));
        assertNull(Echo.optPeriod(null));
        assertEquals(TimeUnit.Years, Echo.enum_(TimeUnit.Years));
        assertNull(Echo.optEnum(null));
        assertEquals(new Complex(1.5, -2.25), Echo.complex(new Complex(1.5, -2.25)));
        assertEquals(2.5, Echo.optDouble(2.5));
        assertNull(Echo.optDouble(null));
        assertEquals(7, Echo.optInt(7));
        assertEquals(1L << 40, Echo.optLong(1L << 40));
        assertNull(Echo.optBool(null));
        assertNull(Echo.optComplex(null));
        ConventionSet qs = ConventionSet.quantLib();
        Money money = new Money(qs.currency("USD"), 12.5);
        assertEquals(12.5, Echo.money(money).value());
        assertEquals(money, Echo.money(money));
        assertNull(Echo.optMoney(null));
    }

    @Test
    void arrays() {
        double[] out = Echo.doubles(new double[] {1.0, -0.0, Double.NaN, 4.5});
        assertEquals(bits(-0.0), bits(out[1]));
        assertTrue(Double.isNaN(out[2]));
        assertEquals(-1, Echo.doublesCount(null));
        assertEquals(0, Echo.doublesCount(new double[0]));
        assertArrayEquals(new int[] {3, -1, 2}, Echo.ints(new int[] {3, -1, 2}));
        assertArrayEquals(new boolean[] {true, false, true}, Echo.bools(new boolean[] {true, false, true}));
        List<LocalDate> dates = List.of(LocalDate.of(2023, 3, 15), LocalDate.of(1901, 1, 1));
        assertEquals(dates, Echo.dates(dates));
        assertEquals(Arrays.asList(1.0, null, 3.0), Echo.optDoubles(Arrays.asList(1.0, null, 3.0)));
        assertEquals(List.of("a", "été", ""), Echo.strings(List.of("a", "été", "")));
        assertEquals(List.of(TimeUnit.Days, TimeUnit.Years), Echo.enums(List.of(TimeUnit.Days, TimeUnit.Years)));
        assertEquals(List.of(new Complex(1, 2)), Echo.complexes(List.of(new Complex(1, 2))));
        List<double[]> rows = Echo.jagged(List.of(new double[] {1, 2}, new double[0], new double[] {3, 4, 5, 6}));
        assertEquals(3, rows.size());
        assertEquals(6.0, rows.get(2)[3]);
        Map<LocalDate, Double> nodes = new LinkedHashMap<>();
        nodes.put(LocalDate.of(2025, 12, 9), 0.02);
        nodes.put(LocalDate.of(2023, 3, 15), 0.01);
        assertEquals(List.of(LocalDate.of(2023, 3, 15), LocalDate.of(2025, 12, 9)), new ArrayList<>(Echo.map(nodes).keySet()));
        Tuple2<Integer, Double> split = Echo.split(3.75);
        assertEquals(3, split.item1());
        assertEquals(0.75, split.item2());
        double[][] t = Echo.transpose(new double[][] {{1, 2, 3}, {4, 5, 6}});
        assertEquals(3, t.length);
        assertEquals(4.0, t[0][1]);
        double[] span = new double[3];
        Echo.fill(span, 7.5);
        assertArrayEquals(new double[] {7.5, 7.5, 7.5}, span);
        double[] point = {0.5, 0.2};
        assertEquals(1.0, new BoundaryConstraint(-1.0, 1.0).update(point, new double[] {0.1, -0.3}, 1.0));
        assertArrayEquals(References.constraintUpdate(), point);
    }

    @Test
    void codes() {
        int[][] kinds = {{1, 1}, {2, 2}, {3, 3}, {4, 4}, {5, 5}, {6, 6}, {7, 7}, {8, 8}, {11, 11}, {12, 12}, {13, 13}, {14, 12}, {15, 13}, {0, 99}};
        for (int[] k : kinds) {
            RuntimeException e = assertThrows(RuntimeException.class, () -> Refusals.throw_(k[0]));
            assertEquals(k[1], ((NanoQLError) e).code());
            assertEquals(k[1] >= 2 && k[1] <= 4, e instanceof IllegalArgumentException);
        }
        NanoQLArgumentException e = assertThrows(NanoQLArgumentException.class, () -> Refusals.throw_(2));
        assertEquals("x", e.param());
        assertEquals("System.ArgumentException", e.typeName());
        assertTrue(e.getMessage().contains("a bad argument"));
        DayCounter none = null;
        NanoQLArgumentException missing = assertThrows(NanoQLArgumentException.class, () -> new FlatForward(LocalDate.of(2023, 3, 15), 0.03, none));
        assertEquals(3, missing.code());
        assertEquals(12, assertThrows(NanoQLException.class, () -> org.nanoql.extensions.conventions.ConventionsXml.read("no-such-conventions.xml")).code());
    }

    @Test
    void handles() {
        long live0 = NanoQL.liveHandles();
        EchoObject a = new EchoObject(2.0, "two");
        assertEquals("NanoQL.Bind.TestTypes.EchoObject", a.typeName());
        assertTrue(a.toString().contains("two"));
        EchoObject b = new EchoObject(2.0, "two");
        assertEquals(a, b);
        assertEquals(a.hashCode(), b.hashCode());
        assertEquals(6.0, a.scaled(3.0).value());
        assertEquals("three", a.withName("three").name());
        try (EchoObject c = new EchoObject(1.0, "one")) {
            assertEquals(1.0, c.value());
            c.close();
            assertTrue(c.isClosed());
            assertEquals(9, assertThrows(NanoQLException.class, c::value).code());
        }
        Calendar cal = ConventionSet.quantLib().calendar("TARGET");
        assertTrue(cal.typeName().contains("TARGET"));
        assertTrue(cal.isBusinessDay(LocalDate.of(2026, 10, 30)));
        List<EchoObject> objs = Echo.objects(List.of(a, b));
        assertEquals(2.0, objs.get(1).value());
        a.close();
        b.close();
        cal.close();
        for (EchoObject o : objs) {
            o.close();
        }
        assertTrue(NanoQL.liveHandles() <= live0 + 4, "handles closed");
    }

    @Test
    void flagsAndCasts() {
        MarkovFunctionalModelSettingsAdjustments both = MarkovFunctionalModelSettingsAdjustments.KahaleSmile.or(MarkovFunctionalModelSettingsAdjustments.SmileExponentialExtrapolation);
        assertEquals(48, both.value());
        assertEquals("KahaleSmile | SmileExponentialExtrapolation", both.toString());
        assertSame(MarkovFunctionalModelSettingsAdjustments.KahaleSmile, MarkovFunctionalModelSettingsAdjustments.of(16));
        try (MarkovFunctionalModelSettings defaults = new MarkovFunctionalModelSettings(); MarkovFunctionalModelSettings s = defaults.withAdjustments(both)) {
            assertEquals(both, s.adjustments());
            assertTrue(s.adjustments().has(MarkovFunctionalModelSettingsAdjustments.KahaleSmile));
            assertFalse(s.adjustments().has(MarkovFunctionalModelSettingsAdjustments.SabrSmile));
        }
        try (ConventionSet qs = ConventionSet.quantLib(); Calendar cal = qs.calendar("TARGET")) {
            NanoObject base = cal;
            long live = NanoQL.liveHandles();
            try (Calendar again = base.as(Calendar.class)) {
                assertNotSame(cal, again);
                assertEquals(cal, again);
                assertEquals(live + 1, NanoQL.liveHandles());
            }
            assertTrue(cal.isBusinessDay(LocalDate.of(2026, 10, 30)), "closing as()'s result leaves the object open");
            try (EchoObject wrong = base.as(EchoObject.class)) {
                assertEquals(10, assertThrows(NanoQLException.class, wrong::value).code());
            }
            assertEquals(live, NanoQL.liveHandles());
        }
        try (HullWhite hw = Fixtures.calibrationHullWhite()) {
            ICalibratedModel model = hw.as(ICalibratedModel.class);
            assertInstanceOf(ICalibratedModel.Of.class, model);
            assertArrayEquals(hw.params(), model.params());
            ((NanoObject) model).close();
        }
    }

    @Test
    void theCleanerReleasesDroppedObjects() throws InterruptedException {
        long live0 = NanoQL.liveHandles();
        for (int i = 0; i < 1000; i++) {
            new EchoObject(i, "dropped");
        }
        long deadline = System.nanoTime() + 30_000_000_000L;
        while (NanoQL.liveHandles() > live0 && System.nanoTime() < deadline) {
            System.gc();
            Thread.sleep(20);
        }
        assertTrue(NanoQL.liveHandles() <= live0, "the cleaner released the dropped objects' handles: " + (NanoQL.liveHandles() - live0) + " left");
    }

    @Test
    void theLoadedLibraryStays() {
        String loaded = NanoQL.libraryPath();
        assertDoesNotThrow(() -> NanoQL.useLibrary(loaded));
        assertThrows(IllegalStateException.class, () -> NanoQL.useLibrary(loaded + ".another"));
    }

    @Test
    void aUsersPackageBesideNanoqls() throws Exception {
        // The test types call their own jextract class; nanoql's holds none of their functions.
        assertTrue(Arrays.stream(org.nanoql.ffi.nanoql_h.class.getMethods()).noneMatch(m -> m.getName().startsWith("nqlt_")));
        assertTrue(Arrays.stream(Class.forName("nqlt.ffi.nqlt_h").getMethods()).anyMatch(m -> m.getName().equals("nqlt_Echo_Double")));
        // Compiled apart: nanoql's classes from the main tree, the test types' with the tests.
        assertNotEquals(Calendar.class.getProtectionDomain().getCodeSource().getLocation(), Echo.class.getProtectionDomain().getCodeSource().getLocation());
        // A user's object comes back as its own class through nanoql's interface, by the user's own registry.
        org.nanoql.bind.testtypes.experimental.Walk walk = new org.nanoql.bind.testtypes.experimental.Walk();
        org.nanoql.experimental.IRandomWalkDistribution copy = walk.clone_();
        assertInstanceOf(org.nanoql.bind.testtypes.experimental.Walk.class, copy);
        assertEquals(0.5, copy.next(new MersenneTwisterUniformRng(42)));
    }

    @Test
    void generators() throws Exception {
        MersenneTwisterUniformRng mt = new MersenneTwisterUniformRng(42);
        Draws<MersenneTwisterUniformRng> d1 = mt.draws(1000);
        assertArrayEquals(d1.values(), mt.draws(1000).values());
        assertEquals(1.0, d1.weights()[0]);
        double[] all = mt.draws(2000).values();
        double[] rest = d1.next().draws(1000).values();
        double[] joined = Arrays.copyOf(d1.values(), 2000);
        System.arraycopy(rest, 0, joined, 1000, 1000);
        assertArrayEquals(all, joined);
        assertEquals(4, assertThrows(NanoQLArgumentException.class, () -> mt.draws(-1)).code());
        ExecutorService pool = Executors.newFixedThreadPool(4);
        List<Future<double[]>> futures = new ArrayList<>();
        for (int i = 0; i < 4; i++) {
            futures.add(pool.submit(() -> mt.draws(1000).values()));
        }
        for (Future<double[]> f : futures) {
            assertArrayEquals(d1.values(), f.get());
        }
        pool.shutdown();
        SequenceDraws<SobolRsg> points = new SobolRsg(5, 1, SobolRsgDirectionIntegers.Jaeckel, true).draws(3);
        assertEquals(3, points.points().length);
        assertEquals(0.5, points.points()[0][4]);
    }

    @Test
    void overloadsAndDefaults() {
        Calendar cal = ConventionSet.quantLib().calendar("TARGET");
        LocalDate d = LocalDate.of(2026, 10, 30);
        assertEquals(LocalDate.of(2026, 11, 2), cal.advance(d, 1, TimeUnit.Days));
        assertEquals(LocalDate.of(2026, 11, 6), cal.advance(d, new Period(1, TimeUnit.Weeks)));
        assertEquals(2.5, Echo.total(2.5));
        assertEquals(6.5, Echo.total(new double[] {1.0, 2.0, 3.5}));
        assertEquals(3.5, Echo.total(List.of(new EchoObject(1.0, "a"), new EchoObject(2.5, "b"))));
        assertEquals(7.0, Echo.total_doubleArrayArray(List.of(new double[] {1, 2}, new double[0], new double[] {4})));
        DayCounter dc = ConventionSet.quantLib().dayCounter("Actual365Fixed");
        FlatForward a = new FlatForward(LocalDate.of(2023, 3, 15), 0.03, dc);
        FlatForward b = new FlatForward(LocalDate.of(2023, 3, 15), 0.03, dc, Compounding.Continuous, Frequency.Annual);
        assertEquals(a.discount(5.0), b.discount(5.0));
        assertEquals(Math.exp(-0.15), a.discount(5.0));
        ConventionSet qs = ConventionSet.quantLib();
        PiecewiseFlatForward curve = PiecewiseFlatForward.create(0, qs.calendar("TARGET"), Fixtures.depositHelpers(), qs.dayCounter("Actual365Fixed"), Fixtures.context());
        assertEquals(bits(References.piecewiseDiscount()), bits(curve.discount(2.0)));
        PiecewiseFlatForward explicit = PiecewiseFlatForward.create(0, qs.calendar("TARGET"), Fixtures.depositHelpers(), qs.dayCounter("Actual365Fixed"),
            Fixtures.context(), null, null, new BackwardFlat(), new IterativeBootstrap());
        assertEquals(bits(References.piecewiseDiscount()), bits(explicit.discount(2.0)));
    }

    @Test
    void periods() {
        Period y = new Period(1, TimeUnit.Years), m = new Period(6, TimeUnit.Months);
        assertEquals(y, new Period(12, TimeUnit.Months));
        assertEquals(y.hashCode(), new Period(12, TimeUnit.Months).hashCode());
        assertNotEquals(y, m);
        assertTrue(Period.opGreaterThan(y, m));
        assertEquals(new Period(18, TimeUnit.Months), Period.opAddition(y, m));
        assertEquals(LocalDate.of(2024, 3, 15), org.nanoql.time.Date.opAddition(LocalDate.of(2023, 3, 15), y));
        assertEquals(2023, org.nanoql.time.Date.year(LocalDate.of(2023, 3, 15)));
    }

    @Test
    void conventionsAndAUsersClasses() {
        DayCounter dc = ConventionSet.quantLib().dayCounter("Actual365Fixed");
        assertEquals("Actual/365 (Fixed)", dc.name());
        org.nanoql.bind.testtypes.UserProvider user = new org.nanoql.bind.testtypes.UserProvider();
        assertInstanceOf(ConventionProvider.class, user);
        ConventionProvider.setDefault(user);
        ConventionProvider.setDefault(ConventionProvider.quantLib());
        org.nanoql.bind.testtypes.collisions.Calendar mine = new org.nanoql.bind.testtypes.collisions.Calendar("TARGET");
        Calendar theirs = mine.nanoqls();
        assertTrue(theirs.isBusinessDay(LocalDate.of(2026, 10, 30)));
        assertEquals(theirs, ConventionSet.quantLib().calendar("TARGET"));
    }

    @Test
    void engines() {
        BoundEngine swapEngine = DiscountingSwapEngine.bind(Fixtures.riskFree());
        assertEquals(bits(References.swapFixedRate()), bits(Fixtures.swapBuilder().withPricingEngine(swapEngine).toVanillaSwap(Fixtures.context()).fixedRate()));
        BoundEngine heston = AnalyticHestonEngine.bind((HestonModel) null, (Integer) null);
        assertEquals(10, assertThrows(NanoQLException.class, () -> Fixtures.swapBuilder().withPricingEngine(heston)).code());
        assertEquals(bits(References.hestonModelValue()), bits(Fixtures.hestonHelper().setPricingEngine(heston).modelValue(Fixtures.heston(), Fixtures.context())));
    }

    @Test
    void tuples() {
        // A tuple as a Tuple2 or Tuple3, null for C#'s default; its elements in their own forms.
        assertEquals(new Tuple2<Double, Double>(null, null), Tuples.single());
        assertEquals(new Tuple2<Double, Double>(0.5, null), Tuples.single(new Tuple2<>(0.5, null)));
        assertEquals(new Tuple2<Double, Double>(null, null), Tuples.single(null));
        assertEquals("-3|9223372036854775807|true", Tuples.scalars(new Tuple3<>(-3, Long.MAX_VALUE, true)));
        assertEquals("x\u00e9|45000|1Y", Tuples.labels(new Tuple3<>("x\u00e9", LocalDate.of(2023, 3, 15), new Period(1, TimeUnit.Years))));
        assertEquals("null|45000|1Y", Tuples.labels(new Tuple3<>(null, LocalDate.of(2023, 3, 15), new Period(1, TimeUnit.Years))));
        assertEquals("6|1.5|-2", Tuples.kinds(new Tuple2<>(Weekday.Friday, new Complex(1.5, -2.0))));
        // A null in an element that takes none is refused, naming the parameter and the element.
        NullPointerException e = assertThrows(NullPointerException.class, () -> Tuples.scalars(new Tuple3<>(1, null, true)));
        assertEquals("x: its total is null", e.getMessage());

        // A collection of tuples: a List of them, in and out.
        EchoObject a = new EchoObject(1.0, "one");
        EchoObject b = new EchoObject(2.0, "two");
        List<Tuple2<EchoObject, Double>> rows = Tuples.rows(List.of(new Tuple2<>(a, 0.25), new Tuple2<>(b, 0.75)));
        assertEquals(2, rows.size());
        assertEquals(a, rows.get(0).item1());
        assertEquals(0.75, rows.get(1).item2());
        assertTrue(Tuples.rows(List.of()).isEmpty());
        List<Tuple3<Double, Integer, LocalDate>> described = new ArrayList<>();
        described.add(new Tuple3<>(2.5, 7, LocalDate.of(2023, 3, 15)));
        described.add(new Tuple3<>(null, 8, null));
        described.add(new Tuple3<>(-1.0, null, LocalDate.of(2023, 3, 16)));
        assertEquals("2.5:7:45000;-:8:-;-1:-:45001", Tuples.describe(described));
        assertEquals("null", Tuples.describe(null));
        assertEquals("", Tuples.describe(List.of()));
        List<Tuple2<String, Weekday>> days = new ArrayList<>();
        days.add(new Tuple2<>("a", Weekday.Monday));
        days.add(new Tuple2<>(null, Weekday.Sunday));
        assertEquals(days, Tuples.days(days));
        List<Tuple3<Period, Long, Boolean>> terms = List.of(new Tuple3<>(new Period(3, TimeUnit.Months), Long.MIN_VALUE, false),
            new Tuple3<>(new Period(2, TimeUnit.Years), 5L, true));
        assertEquals(terms, Tuples.terms(terms));
        e = assertThrows(NullPointerException.class, () -> Tuples.terms(List.of(new Tuple3<>(new Period(1, TimeUnit.Years), null, true))));
        assertEquals("rows: an element's count is null", e.getMessage());
        List<Tuple2<Complex, LocalDate>> numbers = Tuples.numbers(List.of(new Tuple2<>(new Complex(1.0, 2.0), LocalDate.of(2023, 3, 15))));
        assertEquals(new Complex(1.0, 2.0), numbers.get(0).item1());
        // A returned collection of nullable tuples: a null entry for a null one.
        List<Tuple2<Integer, Integer>> pairs = List.of(new Tuple2<>(1, 10), new Tuple2<>(2, 20), new Tuple2<>(3, 30), new Tuple2<>(4, 40));
        assertEquals(Arrays.asList(null, new Tuple2<>(2, 20), null, new Tuple2<>(4, 40)), Tuples.gaps(pairs, 2));
    }

    @Test
    void boundNullableInputs() {
        EvaluationContext ctx = Fixtures.context();
        // The steps per year and the tolerance neither bound nor supplied: null, C#'s value for them.
        BoundEngine mc = McShapedEngine.bind(null, 4, null, 2000, null, 42L);
        double npv = QuantoEngine.calculate(Fixtures.quanto(), ctx, Fixtures.blackScholes(), Fixtures.foreign(), Fixtures.exchangeVolatility(), 0.3, mc).NPV();
        assertEquals(bits(References.mcShapedQuantoNpv(false)), bits(npv));
        BoundEngine shaped = McShapedEngine.bind(null, 4, null, 2000, null, 42L, List.of(new Tuple2<>(0.5, 1.5), new Tuple2<>(1.0, 0.5)));
        npv = QuantoEngine.calculate(Fixtures.quanto(), ctx, Fixtures.blackScholes(), Fixtures.foreign(), Fixtures.exchangeVolatility(), 0.3, shaped).NPV();
        assertEquals(bits(References.mcShapedQuantoNpv(true)), bits(npv));
        // A required input left open is still refused.
        BoundEngine open = McShapedEngine.bind(null, 4, null, 2000);
        NanoQLException e = assertThrows(NanoQLException.class,
            () -> QuantoEngine.calculate(Fixtures.quanto(), ctx, Fixtures.blackScholes(), Fixtures.foreign(), Fixtures.exchangeVolatility(), 0.3, open));
        assertEquals(10, e.code());
        assertTrue(e.getMessage().contains("'seed' is neither bound nor supplied"), e.getMessage());
    }

    @Test
    void defaultsLeftOutAndGeneratorBases() {
        // BFGS's line search, which doesn't cross, at its default; the calibration set read back as tuples.
        List<Tuple2<VanillaOption, Double>> set = Fixtures.calibrationSet();
        assertEquals(9, set.size());
        AndreasenHugeVolatilityInterpl ah = new AndreasenHugeVolatilityInterpl(set, 100.0, Fixtures.riskFree(), Fixtures.dividend(),
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline, AndreasenHugeVolatilityInterplCalibrationType.Call, 100);
        assertEquals(bits(References.andreasenHugeLocalVol(false)), bits(ah.localVol(1.0, 110.0)));
        AndreasenHugeVolatilityInterpl ahb = new AndreasenHugeVolatilityInterpl(set, 100.0, Fixtures.riskFree(), Fixtures.dividend(),
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline, AndreasenHugeVolatilityInterplCalibrationType.Call, 100, null, null, new BFGS());
        assertEquals(bits(References.andreasenHugeLocalVol(true)), bits(ahb.localVol(1.0, 110.0)));

        // A closed generator construction as a parameter takes a deriving class's snapshot; another is refused.
        InvCumulativeMersenneTwisterGaussianRsg gaussian = new InvCumulativeMersenneTwisterGaussianRsg(new MersenneTwisterUniformRsg(3, 42));
        double[][] values = gaussian.draws(4).points();
        double[] flat = new double[12];
        for (int i = 0; i < 4; i++) {
            System.arraycopy(values[i], 0, flat, 3 * i, 3);
        }
        assertArrayEquals(References.gaussianDraws(), flat);
        assertEquals(10, assertThrows(NanoQLException.class, () -> new InvCumulativeMersenneTwisterGaussianRsg(new KnuthUniformRsg(3, 42))).code());
    }

    @Test
    void staticsUnderTheirDeclaringType() throws Exception {
        // The named latent model reaches its base's factory through Java's inheritance, with no method of its own.
        assertThrows(NoSuchMethodException.class, () -> GaussianDefProbLM.class.getDeclaredMethod("fromCorrelation", double.class, int.class));
        Object model = GaussianDefProbLM.fromCorrelation(0.3, 2);
        assertInstanceOf(LatentModel_GaussianCopulaPolicy_GaussianCopulaPolicyInitTraits.class, model);
        assertFalse(model instanceof GaussianDefProbLM);
    }

    @Test
    void calibrations() {
        EvaluationContext ctx = Fixtures.calibrationContext();
        Gsr gsr = Fixtures.gsr();
        HullWhite hw = Fixtures.calibrationHullWhite();
        BoundEngine g1d = Gaussian1dSwaptionEngine.bind();
        BoundEngine capEngine = AnalyticCapFloorEngine.bind();
        List<BlackCalibrationHelper> swaptions = new ArrayList<>();
        for (SwaptionHelper s : Fixtures.swaptions()) {
            swaptions.add(s.setPricingEngine(g1d));
        }
        assertEquals(bits(References.gsrSwaptionModelValue()), bits(((SwaptionHelper) swaptions.get(1)).modelValue(gsr, ctx)));
        assertEquals(6, assertThrows(NanoQLException.class, () -> ((SwaptionHelper) swaptions.get(0)).modelValue(hw, ctx)).code());
        List<CalibrationHelper> caps = new ArrayList<>();
        for (CapHelper c : Fixtures.caps()) {
            caps.add(c.setPricingEngine(capEngine));
        }
        Gsr calibrated = gsr.calibrateVolatilitiesIterative(swaptions, Fixtures.method(), Fixtures.criteria(), ctx).model();
        assertArrayEquals(References.gsrVolatilities(), calibrated.params());
        ICalibratedModel model = hw;
        ICalibratedModel hwCalibrated = CalibratedModelExtensions.calibrate(model, caps, Fixtures.method(), Fixtures.criteria(), ctx).model();
        assertArrayEquals(References.hullWhiteCaps(), ((CalibratedModel) hwCalibrated).params());
    }

    @Test
    void experimental() {
        org.nanoql.experimental.CallableFixedRateBond bond = org.nanoql.bind.testtypes.experimental.FixturesExperimental.callableBond();
        assertEquals("NanoQL.Experimental.CallableFixedRateBond", bond.typeName());
        org.nanoql.bind.testtypes.experimental.Walk walk = new org.nanoql.bind.testtypes.experimental.Walk();
        assertEquals(0.5, ((org.nanoql.experimental.IRandomWalkDistribution) walk).next(new MersenneTwisterUniformRng(42)));
        assertEquals("walk", org.nanoql.bind.testtypes.collisions.experimental.CalendarExperimental.create(walk).name());
        Distribution dist = new org.nanoql.experimental.LossDistBinomial(10, 10.0).evaluate(5, 1.0, 0.3);
        assertEquals(10, dist.x().length);
        assertEquals(1.0, dist.excess()[0]);
    }

    @Test
    void versions() {
        assertEquals(2000, NanoQL.abiVersion());
        assertEquals("2.0", NanoQL.version());
        assertTrue(org.nanoql.time.Date.todaysDate().isAfter(LocalDate.of(2025, 1, 1)));
        assertFalse(NanoQL.liveHandles() < 0);
    }
}
