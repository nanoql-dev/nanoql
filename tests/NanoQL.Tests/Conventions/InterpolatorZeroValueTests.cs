// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The interpolation factories' zero values (playbook §2.6): <c>default(T)</c>, <c>new T()</c>, an array element and a
/// consumer's <c>TInterpolator i = default</c> are QuantLib's default construction, or are refused where QuantLib has
/// none; and QuantLib's presets convert to the <see cref="LogCubic"/> they derive from. The factories expose no
/// parameters, as QuantLib's don't, so the interpolations they build show their settings. The 2-D factory
/// <see cref="Bilinear"/> is stateless, as QuantLib's is.
/// </summary>
public class InterpolatorZeroValueTests
{
    private static readonly (double[] X, double[] Y)[] NodeSets =
    [
        ([0.0, 1.0], [1.0, 3.0]),
        ([0.0, 0.7, 2.0, 2.5, 4.0], [1.0, 0.2, 1.5, 1.4, 3.0]),
        ([-2.0, -1.2, -0.1, 0.4, 1.9, 3.0, 4.5], [0.5, 0.8, 0.8, 1.6, 1.2, 2.5, 2.4]),
    ];

    private static readonly double[] Points = [-2.5, -1.0, 0.0, 0.35, 1.3, 2.2, 3.9, 6.0];

    [Fact]
    public void CubicZeroValueIsQuantLibsDefault()
    {
        // QuantLib's Cubic(): Kruger, not monotonic, second-derivative conditions of 0.
        var spelledOut = new Cubic(
            CubicInterpolationDerivativeApprox.Kruger,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);
        Cubic[] zeros = [default, new Cubic(), (new Cubic[1])[0]];
        foreach ((double[] x, double[] y) in NodeSets)
        {
            Interpolation expected = spelledOut.Interpolate(x, y);
            AssertSame(expected, new KrugerCubic(x, y));
            AssertSame(expected, Consumer<Cubic>(x, y));
            foreach (Cubic zero in zeros)
            {
                AssertSame(expected, zero.Interpolate(x, y));
            }
        }
    }

    [Fact]
    public void LogCubicZeroValueIsRefused()
    {
        (double[] x, double[] y) = NodeSets[1];
        LogCubic[] zeros = [default, new LogCubic(), (new LogCubic[1])[0]];
        foreach (LogCubic zero in zeros)
        {
            Assert.Throws<InvalidOperationException>(() => zero.Interpolate(x, y));
        }

        Assert.Throws<InvalidOperationException>(() => Consumer<LogCubic>(x, y));

        // A LogCubic built with its derivative approximation alone takes QuantLib's defaults for the rest: monotonic,
        // with second-derivative conditions of 0.
        AssertSame(new MonotonicLogCubicNaturalSpline(x, y), new LogCubic(CubicInterpolationDerivativeApprox.Spline).Interpolate(x, y));
    }

    [Fact]
    public void PresetsConvertToTheLogCubicTheyAre()
    {
        var monotonic = new LogCubic(
            CubicInterpolationDerivativeApprox.Spline,
            true,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);
        var kruger = new LogCubic(
            CubicInterpolationDerivativeApprox.Kruger,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);
        foreach ((double[] x, double[] y) in NodeSets)
        {
            LogCubic fromMonotonic = new MonotonicLogCubic(), fromKruger = new KrugerLog();
            LogCubic fromMonotonicZero = default(MonotonicLogCubic), fromKrugerZero = default(KrugerLog);
            AssertSame(monotonic.Interpolate(x, y), fromMonotonic.Interpolate(x, y));
            AssertSame(monotonic.Interpolate(x, y), fromMonotonicZero.Interpolate(x, y));
            AssertSame(monotonic.Interpolate(x, y), new MonotonicLogCubic().Interpolate(x, y));
            AssertSame(monotonic.Interpolate(x, y), Consumer<MonotonicLogCubic>(x, y));
            AssertSame(kruger.Interpolate(x, y), fromKruger.Interpolate(x, y));
            AssertSame(kruger.Interpolate(x, y), fromKrugerZero.Interpolate(x, y));
            AssertSame(kruger.Interpolate(x, y), new KrugerLog().Interpolate(x, y));
            AssertSame(kruger.Interpolate(x, y), Consumer<KrugerLog>(x, y));
        }
    }

    [Fact]
    public void MixedLinearCubicZeroValueIsRefused()
    {
        // QuantLib's MixedLinearCubic has no default: its switch point, behavior and derivative approximation have none.
        (double[] x, double[] y) = NodeSets[1];
        MixedLinearCubic[] zeros = [default, new MixedLinearCubic(), (new MixedLinearCubic[1])[0]];
        foreach (MixedLinearCubic zero in zeros)
        {
            Assert.Throws<InvalidOperationException>(() => zero.Interpolate(x, y));
            Assert.Throws<InvalidOperationException>(() => zero.CheckPoints(5));
        }

        Assert.Throws<InvalidOperationException>(() => Consumer<MixedLinearCubic>(x, y));

        // A MixedLinearCubic built with its first three parameters takes QuantLib's defaults for the rest: monotonic,
        // with second-derivative conditions of 0.
        AssertSame(
            new MixedLinearMonotonicCubicNaturalSpline(x, y, 2),
            new MixedLinearCubic(2, MixedInterpolationBehavior.ShareRanges, CubicInterpolationDerivativeApprox.Spline)
                .Interpolate(x, y));
    }

    [Fact]
    public void LogMixedLinearCubicAndPresetsZeroValuesAreRefused()
    {
        // QuantLib's LogMixedLinearCubic has no default, and its presets take the switch point, which has none.
        (double[] x, double[] y) = NodeSets[2];
        LogMixedLinearCubic[] zeros = [default, new LogMixedLinearCubic(), (new LogMixedLinearCubic[1])[0]];
        foreach (LogMixedLinearCubic zero in zeros)
        {
            Assert.Throws<InvalidOperationException>(() => zero.Interpolate(x, y));
            Assert.Throws<InvalidOperationException>(() => zero.CheckPoints(5));
        }

        MonotonicLogMixedLinearCubic[] monotonic =
            [default, new MonotonicLogMixedLinearCubic(), (new MonotonicLogMixedLinearCubic[1])[0]];
        foreach (MonotonicLogMixedLinearCubic zero in monotonic)
        {
            Assert.Throws<InvalidOperationException>(() => zero.Interpolate(x, y));
            Assert.Throws<InvalidOperationException>(() => zero.CheckPoints(5));
            Assert.Throws<InvalidOperationException>(() => (LogMixedLinearCubic)zero);
        }

        KrugerLogMixedLinearCubic[] kruger =
            [default, new KrugerLogMixedLinearCubic(), (new KrugerLogMixedLinearCubic[1])[0]];
        foreach (KrugerLogMixedLinearCubic zero in kruger)
        {
            Assert.Throws<InvalidOperationException>(() => zero.Interpolate(x, y));
            Assert.Throws<InvalidOperationException>(() => zero.CheckPoints(5));
            Assert.Throws<InvalidOperationException>(() => (LogMixedLinearCubic)zero);
        }

        Assert.Throws<InvalidOperationException>(() => Consumer<LogMixedLinearCubic>(x, y));
        Assert.Throws<InvalidOperationException>(() => Consumer<MonotonicLogMixedLinearCubic>(x, y));
        Assert.Throws<InvalidOperationException>(() => Consumer<KrugerLogMixedLinearCubic>(x, y));
    }

    [Fact]
    public void LogMixedLinearCubicPresetsConvertToTheFactoryTheyAre()
    {
        (double[] x, double[] y) = NodeSets[2];
        foreach (MixedInterpolationBehavior behavior in new[] { MixedInterpolationBehavior.ShareRanges, MixedInterpolationBehavior.SplitRanges })
        {
            var monotonic = new LogMixedLinearCubic(
                2,
                behavior,
                CubicInterpolationDerivativeApprox.Spline,
                true,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0);
            var kruger = new LogMixedLinearCubic(
                2,
                behavior,
                CubicInterpolationDerivativeApprox.Kruger,
                false,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0);
            LogMixedLinearCubic fromMonotonic = new MonotonicLogMixedLinearCubic(2, behavior);
            LogMixedLinearCubic fromKruger = new KrugerLogMixedLinearCubic(2, behavior);
            AssertSame(monotonic.Interpolate(x, y), fromMonotonic.Interpolate(x, y));
            AssertSame(monotonic.Interpolate(x, y), new MonotonicLogMixedLinearCubic(2, behavior).Interpolate(x, y));
            AssertSame(kruger.Interpolate(x, y), fromKruger.Interpolate(x, y));
            AssertSame(kruger.Interpolate(x, y), new KrugerLogMixedLinearCubic(2, behavior).Interpolate(x, y));
            AssertSame(
                new LogMixedLinearCubicNaturalSpline(x, y, 2, behavior),
                new LogMixedLinearCubic(2, behavior, CubicInterpolationDerivativeApprox.Spline, false).Interpolate(x, y));
        }

        // the presets' behavior defaults to ShareRanges, as QuantLib's
        AssertSame(
            new MonotonicLogMixedLinearCubic(2, MixedInterpolationBehavior.ShareRanges).Interpolate(x, y),
            new MonotonicLogMixedLinearCubic(2).Interpolate(x, y));
        AssertSame(
            new KrugerLogMixedLinearCubic(2, MixedInterpolationBehavior.ShareRanges).Interpolate(x, y),
            new KrugerLogMixedLinearCubic(2).Interpolate(x, y));
    }

    [Fact]
    public void BilinearZeroValueIsQuantLibsDefault()
    {
        // QuantLib's Bilinear(), stateless: every instance builds the BilinearInterpolation of the grid.
        double[] x = [0.0, 0.7, 2.0], y = [-1.0, 1.0];
        var z = new Matrix(new[,] { { 1.0, 0.2, 1.5 }, { 0.4, 3.0, -2.0 } });
        var expected = new BilinearInterpolation(x, y, z);
        Bilinear[] zeros = [default, new Bilinear(), (new Bilinear[1])[0]];
        foreach (Interpolation2D actual in zeros.Select(b => b.Interpolate(x, y, z)).Append(Consumer2D<Bilinear>(x, y, z)))
        {
            Assert.IsType<BilinearInterpolation>(actual);
            foreach (double p in Points)
            {
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(expected.Evaluate(p, p / 2.0, true)),
                    BitConverter.DoubleToInt64Bits(actual.Evaluate(p, p / 2.0, true)));
            }
        }
    }

    [Fact]
    public void SabrZeroValueIsRefused()
    {
        // QuantLib's SABR factory has no default constructor.
        double[] strikes = [0.03, 0.035, 0.04];
        double[] vols = [0.21, 0.19, 0.2];
        SABR[] zeros = [default, new SABR(), (new SABR[1])[0]];
        foreach (SABR zero in zeros)
        {
            Assert.Throws<InvalidOperationException>(() => zero.Interpolate(strikes, vols));
            Assert.Throws<InvalidOperationException>(() => zero.CheckPoints(3));
        }

        Assert.Throws<InvalidOperationException>(() => Consumer<SABR>(strikes, vols));

        // A factory built with its parameters fits as the interpolation does, its vega weighting off by default.
        var factory = new SABR(1.0, 0.035, null, 0.5, null, null, false, true, false, false);
        var fit = (SABRInterpolation)Consumer(strikes, vols, factory);
        var direct = new SABRInterpolation(strikes, vols, 1.0, 0.035, null, 0.5, null, null, false, true, false, false, false);
        Assert.Equal((direct.Alpha, direct.Beta, direct.Nu, direct.Rho), (fit.Alpha, fit.Beta, fit.Nu, fit.Rho));
        Assert.True(SABR.Global);
        Assert.Equal(1, SABR.RequiredPoints);
        factory.CheckPoints(1);
        Assert.Throws<ArgumentException>(() => factory.CheckPoints(0));
    }

    // A consumer generic over the 2-D factory, taking QuantLib's const Interpolator& i = {} as TInterpolator i = default.
    private static Interpolation2D Consumer2D<TInterpolator>(double[] x, double[] y, Matrix z, TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator2D =>
        interpolator.Interpolate(x, y, z);

    // A consumer generic over the factory, taking QuantLib's const Interpolator& i = {} as TInterpolator i = default.
    private static Interpolation Consumer<TInterpolator>(double[] x, double[] y, TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator =>
        interpolator.Interpolate(x, y);

    private static void AssertSame(Interpolation expected, Interpolation actual)
    {
        foreach (double p in Points)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Evaluate(p, true)), BitConverter.DoubleToInt64Bits(actual.Evaluate(p, true)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.SecondDerivative(p, true)), BitConverter.DoubleToInt64Bits(actual.SecondDerivative(p, true)));
        }
    }
}
