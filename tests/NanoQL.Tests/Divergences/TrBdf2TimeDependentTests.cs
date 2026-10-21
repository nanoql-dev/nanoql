// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="TrBDF2Scheme{TTrapezoidalScheme}"/> with time-dependent coefficients (the S-eq plan's SEQR9). QuantLib's
/// step sets the operator over its trapezoidal sub-step's interval, [t − α·dt, t], and solves its BDF2 stage with that
/// operator (<c>trbdf2scheme.hpp:106-124</c>), so where a curve's rate moves within the step the stage reads the wrong
/// one: first order, or stalling. nanoql sets the operator once over the whole step, [t − dt, t], the interval
/// QuantLib's other schemes set, for both stages. On a zero curve whose forward rate jumps at half a year, an
/// at-the-money call priced on 800 points lies within 1e-3 of the analytic engine at 365 and 640 steps, where
/// QuantLib's engine errs by 2.7e-2 and 2.0e-2. QuantLib's TR-BDF2 cases are all on flat inputs, where the two forms
/// differ in the last bits only (the scheme's vectors, within R7).
/// </summary>
public class TrBdf2TimeDependentTests
{
    [Fact]
    public void BothStagesOverTheWholeStepConvergeOnATimeDependentCurve()
    {
        var today = new Date(15, Month.May, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(today, 0.0, dc),
            new ZeroCurve([today, today + 182, today + 365], [0.0, 0.0, 0.40], dc),
            new BlackConstantVol(today, null, 0.30, dc));
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(today + 365));

        // QuantLib's check: its analytic engine, 34.0011710091
        double analytic = AnalyticEuropeanEngine.Calculate(call, context, process).NPV!.Value;

        // QuantLib's engine reads 34.0284632 at 365 steps and 34.0214764 at 640, 2.7e-2 and 2.0e-2 above
        foreach (int steps in new[] { 365, 640 })
        {
            double fd = FdBlackScholesVanillaEngine.Calculate(
                call, context, process, tGrid: steps, xGrid: 800, schemeDesc: FdmSchemeDesc.TrBDF2()).NPV!.Value;
            Assert.Equal(analytic, fd, 1e-3);
        }
    }
}
