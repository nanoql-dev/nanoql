// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The interpolation factories' zero values (playbook §2.6): <c>default(T)</c>, <c>new T()</c>, an array element and a
/// consumer's <c>TInterpolator i = default</c> are QuantLib's default construction, or are refused where QuantLib has
/// none; and QuantLib's presets convert to the <see cref="LogCubic"/> they derive from. The factories expose no
/// parameters, as QuantLib's don't, so the interpolations they build show their settings.
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
