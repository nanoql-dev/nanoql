// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's <c>testMonteCarloCalibration</c> means to bound the calibration's maximum error by 15 bp, but tests the
/// average against 15 (<c>hestonslvmodel.cpp:2074</c>); its port in <see cref="TestSuite.HestonSlvModelTests"/> keeps
/// QuantLib's check, and this fact checks the maximum itself on QuantLib's case.
/// </summary>
public class HestonSlvMcQualityMaximumTests
{
    [Fact]
    public void TheCalibrationsLargestErrorIsWithinFifteenBasisPoints()
    {
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(5, January, 2016);
        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);
        var context = new EvaluationContext { EvaluationDate = todaysDate };
        const double s0 = 100;

        var rTS = new FlatForward(todaysDate, 0.05, dc);
        var qTS = new FlatForward(todaysDate, 0.02, dc);
        var localVol = new LocalConstantVol(todaysDate, 0.3, dc);
        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, s0, 0.09, 1.0, 0.06, 0.4, -0.75));

        LocalVolTermStructure leverageFct = new HestonSLVMCModel(
            localVol,
            hestonModel,
            new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 1234, SobolRsgDirectionIntegers.JoeKuoD7),
            maturityDate,
            91,
            400,
            40000).LeverageFunction;

        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, new BlackConstantVol(todaysDate, new NullCalendar(), 0.3, dc));

        double maxQualityFactor = 0.0;
        foreach (Date maturity in new[]
        {
            todaysDate + new Period(3, TimeUnit.Months), todaysDate + new Period(6, TimeUnit.Months),
            todaysDate + new Period(12, TimeUnit.Months),
        })
        {
            double maturityTime = dc.YearFraction(todaysDate, maturity);
            var exercise = new EuropeanExercise(maturity);
            foreach (double strike in new double[] { 50, 80, 100, 120, 150, 200 })
            {
                var option = new VanillaOption(new PlainVanillaPayoff(strike < s0 ? OptionType.Put : OptionType.Call, strike), exercise);
                OneAssetOptionResults bs = AnalyticEuropeanEngine.Calculate(option, context, bsProcess);
                if (bs.NPV!.Value > 0.02)
                {
                    double fdmNPV = FdHestonVanillaEngine.Calculate(
                        option, context, hestonModel, Math.Max(26, (int)(maturityTime * 51)), 201, 51, 0, FdmSchemeDesc.ModifiedCraigSneyd(), leverageFct).NPV!.Value;
                    maxQualityFactor = Math.Max(maxQualityFactor, Math.Abs(fdmNPV - bs.NPV.Value) / bs.Vega!.Value * 1e4);
                }
            }
        }

        Assert.True(maxQualityFactor <= 15.0, $"maximum calibration error {maxQualityFactor} bp");
    }
}
