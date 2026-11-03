// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Structures dated or counted apart through the local-volatility readers. QuantLib's Andreasen-Huge interpolation
/// reads the dividend curve at the risk-free curve's times (<c>andreasenhugevolatilityinterpl.cpp:299</c>, <c>:414</c>,
/// <c>:462</c>, <c>:547</c>, <c>:623</c>); its Black-Scholes, generalized Black-Scholes and local-volatility density
/// calculators read their curves and volatility at one time; and its forward operators read like
/// <c>FdmBlackScholesOp</c>. So a dividend curve on Actual/360 under a market on Actual/365 (Fixed) is read at other
/// instants: from 15 October 2026, curves at 3% and 1%, the Andreasen-Huge forward at half a year is 101.0050167084168
/// where the dividend curve restated on Actual/365 (Fixed), its rate times 365/360 and so the same discount at every
/// date, gives 100.99800271468963; the density calculators' cumulative probabilities at 105 are 0.6208898362432407,
/// where the restated market gives 0.6210392949047169 (QuantLib-Python 1.44). nanoql reads each structure at the
/// instant a time denotes, on its own clock: the risk-free curve's for the interpolation and the Black-Scholes
/// calculators, the local volatility's for its calculator, and <c>FdmBlackScholesOp</c>'s for the forward operators.
/// QuantLib's check, its readers on the restated market, gives nanoql's values. Where every structure shares the clock,
/// QuantLib's bits stand (the vectors). On its default grid the local-volatility calculator solves to its local
/// volatility's last date, the last representable one for a structure without a maximum date, which a curve dated later
/// reads too.
/// </summary>
public class LocalVolMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure R = new FlatForward(Today, 0.03, A365);
    private static readonly YieldTermStructure Q = new FlatForward(Today, 0.01, new Actual360());

    [Fact]
    public void AndreasenHugeReadsTheDividendCurveAtTheInstants()
    {
        (VanillaOption, double)[] options =
        [
            .. new[] { 80.0, 100.0, 120.0 }.SelectMany(k => new[] { 91, 365, 730 }.Select(d => (
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, k), new EuropeanExercise(Today + d)),
                0.2 + (0.1 * ((k - 100.0) / 100.0) * ((k - 100.0) / 100.0))))),
        ];
        var ah = new AndreasenHugeVolatilityInterpl(
            options,
            100.0,
            R,
            Q,
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            AndreasenHugeVolatilityInterplCalibrationType.Call,
            120);

        // QuantLib's interpolation on the restated market
        AssertClose(100.99800271468963, ah.Fwd(0.5), 1e-14);
        AssertClose(0.20542208480796698, ah.LocalVol(0.5, 90.0), 1e-10);
        AssertClose(5.480532721326754, ah.OptionPrice(0.75, 105.0, OptionType.Call), 1e-10);
    }

    [Fact]
    public void TheDensityCalculatorsReadEachStructureAtTheInstants()
    {
        var process = new BlackScholesMertonProcess(100.0, Q, R, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

        // QuantLib's calculators on the restated market
        var bsm = new BSMRNDCalculator(process);
        AssertClose(2.152074492086501, bsm.Pdf(Math.Log(105.0), 0.5), 1e-13);
        AssertClose(0.6210392949047169, bsm.Cdf(Math.Log(105.0), 0.5), 1e-13);
        AssertClose(4.506773951794273, bsm.InvCdf(0.3, 0.5), 1e-13);

        var gbsm = new GBSMRNDCalculator(process);
        AssertClose(0.020495873315353106, gbsm.Pdf(105.0, 0.5), 1e-13);
        AssertClose(0.621039294904718, gbsm.Cdf(105.0, 0.5), 1e-13);
        AssertClose(90.62897297112225, gbsm.InvCdf(0.3, 0.5), 1e-12);

        var localVol = new LocalVolRNDCalculator(100.0, R, Q, FixedLocalVol(), 61, 21, 0.1, 1e-6, 10000);
        AssertClose(2.4112112146914795, localVol.Pdf(Math.Log(105.0), 0.5), 1e-10);
        AssertClose(0.628988412250709, localVol.Cdf(Math.Log(105.0), 0.5), 1e-10);
        AssertClose(4.520256523029065, localVol.InvCdf(0.3, 0.5), 1e-10);
    }

    [Fact]
    public void TheForwardOperatorsReadEachStructureAtTheInstants()
    {
        var process = new BlackScholesMertonProcess(100.0, Q, R, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var mesher = new FdmMesherComposite(new Uniform1dMesher(Math.Log(50.0), Math.Log(200.0), 21));
        var u = new Vector(21);
        for (int i = 0; i < 21; ++i)
        {
            double x = Math.Log(50.0) + (i * (Math.Log(200.0) - Math.Log(50.0)) / 20) - Math.Log(100.0);
            u[i] = Math.Exp(-30.0 * x * x);
        }

        // QuantLib's operators on the restated market, after setTime(0.5, 0.75)
        var black = new FdmBlackScholesFwdOp(mesher, process, 100.0, false);
        black.SetTime(0.5, 0.75);
        AssertAllClose(
            [
                1.306871351726944e-06, 0.0005421645954271168, 0.0044135611712000245, 0.026231570499248236,
                0.11229186487130156, 0.3378704731967718, 0.6786898604620653, 0.7853085112248426, 0.14874803938412917,
                -1.06780030599809, -1.7461374709994288, -1.139793535802177, 0.05139939243005154, 0.7093666291534727,
                0.6382607287630822, 0.322415183675845, 0.10795834381992464, 0.025331233158588387, 0.004274229930661346,
                0.0005260584669636452, -1.3068713517270089e-06,
            ],
            black.Apply(u).AsSpan().ToArray());

        var local = new FdmLocalVolFwdOp(mesher, 100.0, R, Q, FixedLocalVol());
        local.SetTime(0.5, 0.75);
        AssertAllClose(
            [
                1.1663672398558967e-06, 0.0005156132316987662, 0.004157041185371517, 0.0244396644967846,
                0.10331382245502727, 0.30613886998194884, 0.6015460329649769, 0.6631877347390618, 0.06512044013861296,
                -0.9975289666455289, -1.3389966990506075, -1.0300682135971728, -0.0031164429438230723, 0.6022015781332944,
                0.5715749424511603, 0.29669400144256425, 0.10160761518127792, 0.024373898449449087, 0.004206147697862869,
                0.0005298388474067072, -1.4313456666617877e-06,
            ],
            local.Apply(u).AsSpan().ToArray());
    }

    [Fact]
    public void OnItsDefaultGridToTheLastDateTheLocalVolCalculatorReadsACurveDatedLater()
    {
        // a local volatility without a maximum date: the default grid ends at the last representable date, whose
        // instant a curve dated two days late reads on its own clock; QuantLib's calculator on the curve restated on
        // today's clock, nothing accruing before its date and the flat rate after (log-linear, exact on Actual/365)
        var localVol = new LocalConstantVol(Today, 0.2, A365);
        var late = new FlatForward(Today + 2, 0.02, A365);
        var restated = new DiscountCurve(
            [Today, Today + 2, Date.MaxDate], [1.0, 1.0, Math.Exp(-0.02 * A365.YearFraction(Today + 2, Date.MaxDate))], A365);

        var onLate = new LocalVolRNDCalculator(100.0, R, late, localVol, 101, 51);
        var onRestated = new LocalVolRNDCalculator(100.0, R, restated, localVol, 101, 51);
        Assert.Equal(localVol.MaxTime, onLate.TimeGrid[onLate.TimeGrid.Size - 1]);
        foreach (double t in new[] { 0.5, 1.0, 10.0 })
        {
            AssertClose(onRestated.Cdf(Math.Log(110.0), t), onLate.Cdf(Math.Log(110.0), t), 1e-12);
            AssertClose(onRestated.InvCdf(0.3, t), onLate.InvCdf(0.3, t), 1e-12);
        }
    }

    private static FixedLocalVolSurface FixedLocalVol()
    {
        double[] times = [0.25, 0.5, 1.0, 2.0];
        double[] strikes = [20.0, 50.0, 80.0, 100.0, 125.0, 200.0, 500.0];
        var m = new Matrix(strikes.Length, times.Length);
        for (int i = 0; i < strikes.Length; ++i)
        {
            for (int j = 0; j < times.Length; ++j)
            {
                m[i, j] = 0.22 + (0.0004 * Math.Pow(Math.Abs(strikes[i] - 100.0), 0.9)) + (0.01 * j);
            }
        }

        return new FixedLocalVolSurface(
            Today,
            times,
            strikes,
            m,
            A365,
            FixedLocalVolSurfaceExtrapolation.InterpolatorDefaultExtrapolation,
            FixedLocalVolSurfaceExtrapolation.InterpolatorDefaultExtrapolation);
    }

    private static void AssertAllClose(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        double scale = expected.Max(Math.Abs);
        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.True(
                Math.Abs(actual[i] - expected[i]) <= 1e-12 * scale,
                $"[{i}] {actual[i]:R} differs from {expected[i]:R} by more than 1e-12 of {scale:R}");
        }
    }

    private static void AssertClose(double expected, double actual, double relative) =>
        Assert.True(
            Math.Abs(actual - expected) <= relative * Math.Abs(expected),
            $"{actual:R} differs from {expected:R} by more than {relative} relative");
}
