// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference SLV model's mandatory dates (the S-hes plan's HR7 (xx)). QuantLib appends their times to its
/// exponential grid unsorted and builds the leverage surface on that list (<c>hestonslvfdmmodel.cpp:325-330</c>,
/// <c>:412</c>): a mandatory date inside the period refuses ("dates must be sorted unique!"), one at the end date
/// ("need strikes for every time step"), and one on the grid's own time ("dates must be sorted unique!"), each on
/// QuantLib-SWIG 1.44. nanoql runs the surface and the loop on the grid's times, sorted and made unique: a date at the
/// end or on the grid's own time is QuantLib's calibration without it, bit for bit, and one inside the period prices
/// the local volatility's options as the calibration without it does, QuantLib's own measure of a calibration.
/// </summary>
public class HestonSlvFdmMandatoryDatesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly (double T, double K)[] Points = [(0.1, 95.0), (0.5, 100.0), (0.75, 110.0), (1.0, 90.0)];

    [Fact]
    public void AMandatoryDateAtTheEndIsTheCalibrationWithoutIt()
    {
        AssertSameLeverage(Model([]), Model([Today + 365]));
    }

    [Fact]
    public void AMandatoryDateOnTheGridsOwnTimeIsTheCalibrationWithoutIt()
    {
        // at 365 steps a year the grid's first time is a day, 1/365
        AssertSameLeverage(Model([]), Model([Today + 1]));
    }

    [Fact]
    public void AMandatoryDateInsideThePeriodCalibratesToTheLocalVolatility()
    {
        HestonSLVFDMModel withDate = Model([Today + 100]);
        double without = LargestError(Model([]));
        double with = LargestError(withDate);
        Assert.True(with <= Math.Max(1.5 * without, 2.0), $"largest error {with} bp, {without} bp without the date");
    }

    private static HestonSLVFDMModel Model(Date[] mandatoryDates)
    {
        var process = new HestonProcess(
            new FlatForward(Today, 0.04, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);
        var parameters = new HestonSLVFokkerPlanckFdmParams(
            51, 51, 365, 50, 100.0, 5, 2, 0.1, 1e-4, 10000, 1e-5, 1e-5, 0.0000025, 1.0, 0.1, 0.9, 1e-5,
            FdmHestonGreensFctAlgorithm.ZeroCorrelation, FdmSquareRootFwdOpTransformationType.Log, FdmSchemeDesc.ModifiedCraigSneyd());
        return new HestonSLVFDMModel(
            new LocalConstantVol(Today, 0.25, A365), new HestonModel(process), Today + 365, parameters, false, mandatoryDates);
    }

    private static void AssertSameLeverage(HestonSLVFDMModel expected, HestonSLVFDMModel actual)
    {
        foreach ((double t, double k) in Points)
        {
            Assert.Equal(expected.LeverageFunction.LocalVol(t, k, true), actual.LeverageFunction.LocalVol(t, k, true));
        }
    }

    // The largest difference, in basis points of vega, of the SLV prices from the local volatility's (Black-Scholes at
    // 25%) over calls and puts out of the money at 3, 6 and 12 months
    private static double LargestError(HestonSLVFDMModel model)
    {
        var process = new GeneralizedBlackScholesProcess(
            100.0, model.HestonProcess.DividendYield, model.HestonProcess.RiskFreeRate,
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        double largest = 0.0;
        foreach (int days in new[] { 91, 182, 365 })
        {
            var exercise = new EuropeanExercise(Today + days);
            foreach (double strike in new[] { 80.0, 90.0, 100.0, 110.0, 125.0 })
            {
                var option = new VanillaOption(new PlainVanillaPayoff(strike < 100.0 ? OptionType.Put : OptionType.Call, strike), exercise);
                OneAssetOptionResults bs = AnalyticEuropeanEngine.Calculate(option, Context, process);
                double slv = FdHestonVanillaEngine.Calculate(
                    option, Context, new HestonModel(model.HestonProcess), 51, 201, 51, 0, FdmSchemeDesc.ModifiedCraigSneyd(), model.LeverageFunction).NPV!.Value;
                largest = Math.Max(largest, Math.Abs(slv - bs.NPV!.Value) / bs.Vega!.Value * 1e4);
            }
        }

        return largest;
    }
}
