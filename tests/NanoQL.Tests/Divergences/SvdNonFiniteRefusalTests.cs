// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The singular value decomposition of a non-finite matrix. QuantLib's main loop has no test for too many iterations
/// (<c>svd.cpp:276-280</c>, "Here is where a test for too many iterations would go"): on a 5 × 3 matrix with one NaN or
/// one ∞ it runs past 20 seconds (QuantLib 1.44). And the American Monte Carlo engine accepts the second-kind Chebyshev
/// basis, √w(x)Pₙ(x) with w = (1 − x)^½(1 + x)^½, NaN above 1: a call regresses its in-the-money paths, S/K &gt; 1, so
/// every regression meets NaN columns and QuantLib's engine hangs. nanoql refuses a non-finite entry on entry, naming
/// it, and the engine's call reaches its caller refused, as raised. A finite matrix whose singular values lie past the
/// range of doubles never ends either, in QuantLib and in the port of its arithmetic; nanoql refuses it at the main
/// loop's first non-finite singular value or superdiagonal entry, which none of 578 matrices QuantLib decomposes
/// reaches (a probe over entries from 1e290 to the limit, four shapes). Each fact runs under a timeout, since without
/// the refusal it doesn't return.
/// </summary>
public class SvdNonFiniteRefusalTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ANonFiniteEntryIsRefusedNamingIt()
    {
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity })
        {
            var m = new Matrix(5, 3);
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    m[i, j] = (i + 1) * (j + 2) * 0.1;
                }
            }

            m[1, 2] = bad;
            Exception? e = await Task.Run(() => Record.Exception(() => new SVD(m))).WaitAsync(Timeout, TestContext.Current.CancellationToken);
            ArgumentException a = Assert.IsType<ArgumentException>(e);
            Assert.Equal("matrix", a.ParamName);
            Assert.StartsWith($"matrix entry (1, 2) is {bad}: the singular value decomposition of a non-finite matrix is refused", a.Message);
        }
    }

    [Fact]
    public async Task AFiniteMatrixWhoseSingularValuesLeaveTheDoublesIsRefused()
    {
        // the 5 × 3 matrix (1 + ij + 0.1i)·c: QuantLib's decomposition returns at c = 1e307, its largest singular value
        // 1.5770025263428427e308, and is still running after 20 s at 1.8e307, where that value is about 2.8e308
        // (QuantLib-Python 1.44, in child processes)
        static Matrix Scaled(double c)
        {
            var m = new Matrix(5, 3);
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    m[i, j] = (1 + (i * j) + (0.1 * i)) * c;
                }
            }

            return m;
        }

        double largest = new SVD(Scaled(1e307)).SingularValues()[0];
        Assert.Equal(1.5770025263428427e308, largest, 1.5770025263428427e308 * 1e-15);

        Exception? e = await Task.Run(() => Record.Exception(() => new SVD(Scaled(1.8e307)))).WaitAsync(Timeout, TestContext.Current.CancellationToken);
        ArgumentException a = Assert.IsType<ArgumentException>(e);
        Assert.Equal("matrix", a.ParamName);
        Assert.StartsWith("the singular value decomposition leaves the range of doubles (", a.Message);
    }

    [Fact]
    public async Task TheSecondKindChebyshevBasisOnACallIsRefusedThroughIt()
    {
        var today = new Date(15, October, 2026);
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.0, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new AmericanExercise(today, today + 365));
        var context = new EvaluationContext { EvaluationDate = today };

        Exception? e = await Task.Run(() => Record.Exception(() => MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
            option, context, process, 20, null, false, false, 1000, null, null, 42, 2, LsmBasisSystemPolynomialType.Chebyshev2nd)))
            .WaitAsync(Timeout, TestContext.Current.CancellationToken);
        ArgumentException a = Assert.IsType<ArgumentException>(e);
        Assert.Equal("matrix", a.ParamName);
        Assert.Contains("the singular value decomposition of a non-finite matrix is refused", a.Message);

        // the put regresses its in-the-money paths, S/K < 1, where the basis is finite
        OneAssetOptionResults put = MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(today, today + 365)),
            context, process, 20, null, false, false, 1000, null, null, 42, 2, LsmBasisSystemPolynomialType.Chebyshev2nd);
        Assert.True(double.IsFinite(put.NPV!.Value));
    }
}
