// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The extended binomial trees on a time-dependent process, QuantLib's design kept (the S-xrt plan's XRR6, ruled
/// 2026-10-28), and their two route (§2.5). A tree built directly on the process reads its volatility at each step's
/// time for the whole history to that step, so its spread at step i is a constant σ(tᵢ)'s, not the integrated one;
/// QuantLib's binomial engine flattens its process to constant parameters before it builds any tree, so through the
/// engine each extended tree prices as the standard tree, to the rounding of the flat process's reads at each step's
/// time, 5e-13 relative (QuantLib 1.44's extended and standard Jarrow-Rudd at 101, 401 and 1,601 steps agree to every
/// printed digit, the S-xrt planner's <c>xp_c3.cpp</c>).
/// </summary>
public class ExtendedTreeTimeDependenceTests
{
    private static readonly Date Today = new(15, Month.June, 2026);

    [Fact]
    public void ATreeOnATimeDependentProcessSpreadsAtEachStepsVolatility()
    {
        GeneralizedBlackScholesProcess process = TimeDependent();
        var tree = new ExtendedJarrowRudd(process, 2.0, 40, 100.0);
        double dt = 2.0 / 40;
        foreach (int i in new[] { 10, 30 })
        {
            double ti = i * dt;
            double spread = Math.Log(tree.Underlying(i, i) / tree.Underlying(i, 0)) / (2.0 * i);
            Assert.Equal(process.StdDeviation(ti, 100.0, dt), spread, 12);
        }

        // the integrated spread to step 30 differs from the step's own
        double integrated = process.StdDeviation(0.0, 100.0, 30 * dt) / Math.Sqrt(30);
        Assert.True(Math.Abs(process.StdDeviation(30 * dt, 100.0, dt) - integrated) > 0.01);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(401)]
    public void ThroughTheEngineEachExtendedTreeIsTheStandardOne(int steps)
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        GeneralizedBlackScholesProcess process = TimeDependent();
        var option = new EuropeanOption(
            new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + new Period(2, TimeUnit.Years)));
        Assert.Equal(1.0, Ratio<ExtendedJarrowRudd, JarrowRudd>(option, context, process, steps), 11);
        Assert.Equal(1.0, Ratio<ExtendedCoxRossRubinstein, CoxRossRubinstein>(option, context, process, steps), 11);
        Assert.Equal(1.0, Ratio<ExtendedTian, Tian>(option, context, process, steps), 11);
        Assert.Equal(1.0, Ratio<ExtendedLeisenReimer, LeisenReimer>(option, context, process, steps), 11);
    }

    private static double Ratio<TExtended, TStandard>(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, int steps)
        where TExtended : Tree, IBinomialTree<TExtended>
        where TStandard : Tree, IBinomialTree<TStandard> =>
        BinomialVanillaEngine.Calculate<TExtended>(option, context, process, steps).NPV!.Value
        / BinomialVanillaEngine.Calculate<TStandard>(option, context, process, steps).NPV!.Value;

    // The planner's volatility: 0.10 to a year, 0.30 to two.
    private static GeneralizedBlackScholesProcess TimeDependent()
    {
        DayCounter dc = new Actual365Fixed();
        var vol = new BlackVarianceCurve(
            Today, [Today + new Period(1, TimeUnit.Years), Today + new Period(2, TimeUnit.Years)], [0.10, 0.2236068], dc);
        return new GeneralizedBlackScholesProcess(100.0, new FlatForward(Today, 0.0, dc), new FlatForward(Today, 0.03, dc), vol);
    }
}
