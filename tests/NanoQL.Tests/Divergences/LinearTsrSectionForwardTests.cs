// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The linear TSR pricer's forward on a section with an at-the-money level of its own: QuantLib wraps a section without
/// an at-the-money level at the coupon's forward and keeps one that has its own (<c>lineartsrpricer.cpp:177-181</c>),
/// so on such a section the options price on the section's forward while the model's split and singular terms take the
/// coupon's. The SABR and ZABR cubes build each section on an interpolated forward
/// (<c>sabrswaptionvolatilitycube.hpp:1015-1029</c>): on QuantLib's cms.cpp SABR cube over a 1%–5% zero curve, a 1-year
/// coupon fixing in 5 years has a section forward 5.7e-3 below its own, and QuantLib's TSR prices its caplet at F + 1%
/// 1.83e-3 below QuantLib's numeric Hagan pricer and its floorlet at F − 1% 1.29e-3 above. nanoql wraps every section
/// at the coupon's forward, so every TSR price on those cubes moves; a matrix's sections have no at-the-money level,
/// and the interpolated cube's carry the coupon's forward, so there the prices are QuantLib's. A ZABR section, whose
/// own price is its kernel's, is priced through the volatility it reports. QuantLib's checks: its values on a matrix
/// and the interpolated cube, and its numeric Hagan pricer on the SABR cube.
/// </summary>
public class LinearTsrSectionForwardTests
{
    private static readonly double[][] VolSpreads =
    [
        [0.0599, 0.0049, 0.0000, -0.0001, 0.0127],
        [0.0729, 0.0086, 0.0000, -0.0024, 0.0098],
        [0.0738, 0.0102, 0.0000, -0.0039, 0.0065],
        [0.0465, 0.0063, 0.0000, -0.0032, -0.0010],
        [0.0558, 0.0084, 0.0000, -0.0050, -0.0057],
        [0.0576, 0.0083, 0.0000, -0.0043, -0.0014],
        [0.0437, 0.0059, 0.0000, -0.0030, -0.0006],
        [0.0533, 0.0078, 0.0000, -0.0045, -0.0046],
        [0.0545, 0.0079, 0.0000, -0.0042, -0.0020],
    ];

    private static readonly Period[] OptionTenors = [1 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];
    private static readonly Period[] SwapTenors = [2 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];
    private static readonly double[] StrikeSpreads = [-0.020, -0.005, 0.000, 0.005, 0.020];

    [Fact]
    public void AMatrixAndTheInterpolatedCubeKeepQuantLibsValues()
    {
        SwapIndex index = new EuriborSwapIsdaFixA(10 * TimeUnit.Years, CmsInputs.CmsCurve);
        SwaptionVolatilityStructure cube = new InterpolatedSwaptionVolatilityCube(
            CmsInputs.CmsAtm(), OptionTenors, SwapTenors, StrikeSpreads, VolSpreads, index,
            new EuriborSwapIsdaFixA(2 * TimeUnit.Years, CmsInputs.CmsCurve), false, CmsInputs.CmsContext).EnableExtrapolation();
        foreach ((int start, SwaptionVolatilityStructure vol, double rate, double caplet, double floorlet) in new[]
        {
            (5, CmsInputs.CmsAtm(), 0.05230576307428443, 0.00336401785893881, 0.0018187099686580617),
            (5, cube, 0.08096086539003897, 0.031787405141196816, 0.002539403929079183),
            (20, CmsInputs.CmsAtm(), 0.05447558225712464, 0.00899546852263383, 0.004724163111141688),
            (20, cube, 0.054789473580495025, 0.008297804434017965, 0.005873208942380505),
        })
        {
            CmsCoupon c = CmsInputs.CmsCoupon(index, start, 12);
            double f = index.Fixing(c.FixingDate(), CmsInputs.CmsContext);
            var pricer = new LinearTsrPricer(vol, 0.0);
            AssertClose(rate, c.SetPricer(pricer).Rate(CmsInputs.CmsContext));
            AssertClose(caplet, pricer.CapletRate(c, f + 0.01, CmsInputs.CmsContext));
            AssertClose(floorlet, pricer.FloorletRate(c, f - 0.01, CmsInputs.CmsContext));
        }
    }

    [Fact]
    public void OnTheSabrCubeOverASlopedCurveTheOptionsAgreeWithHagan()
    {
        Date today = CmsInputs.CmsToday;
        var curve = new ZeroCurve(
            [today, today + (2 * TimeUnit.Years), today + (10 * TimeUnit.Years), today + (60 * TimeUnit.Years)],
            [0.01, 0.02, 0.04, 0.05],
            new Actual365Fixed()).EnableExtrapolation();
        SwapIndex index = new EuriborSwapIsdaFixA(10 * TimeUnit.Years, curve);
        SwaptionVolatilityStructure cube = new SabrSwaptionVolatilityCube(
            CmsInputs.CmsAtm(),
            OptionTenors,
            SwapTenors,
            StrikeSpreads,
            VolSpreads,
            index,
            new EuriborSwapIsdaFixA(2 * TimeUnit.Years, curve),
            false,
            [.. Enumerable.Range(0, 9).Select(_ => new[] { 0.2, 0.5, 0.4, 0.0 })],
            [false, true, false, false],
            false,
            CmsInputs.CmsContext).EnableExtrapolation();
        CmsCoupon c = CmsInputs.CmsCoupon(index, 5, 12);
        double f = index.Fixing(c.FixingDate(), CmsInputs.CmsContext);
        var tsr = new LinearTsrPricer(cube, 0.0);
        var hagan = new NumericHaganPricer(cube, GFunctionFactoryYieldCurveModel.Standard, 0.0);
        double caplet = tsr.CapletRate(c, f + 0.01, CmsInputs.CmsContext) - hagan.CapletRate(c, f + 0.01, CmsInputs.CmsContext);
        double floorlet = tsr.FloorletRate(c, f - 0.01, CmsInputs.CmsContext) - hagan.FloorletRate(c, f - 0.01, CmsInputs.CmsContext);
        Assert.True(Math.Abs(caplet) <= 2e-5, $"caplet {caplet}");
        Assert.True(Math.Abs(floorlet) <= 2e-5, $"floorlet {floorlet}");
    }

    [Fact]
    public void AZabrSectionIsPricedThroughItsVolatility()
    {
        var zabr = new ZabrShortMaturityNormalSmileSection(5.0, 0.03, [0.08, 0.7, 0.2, -0.3, 1.0]);
        var wrapped = new AtmSmileSection(zabr, 0.031);
        foreach (double k in new[] { 0.02, 0.03, 0.045 })
        {
            double black = BlackFormulaFunctions.BlackFormula(OptionType.Call, k, 0.031, Math.Sqrt(zabr.Variance(k)));
            Assert.Equal(black, wrapped.OptionPrice(k, OptionType.Call));
        }

        // at its own forward the wrap prices as the section, to its volatility's inversion
        var own = new AtmSmileSection(zabr, 0.03);
        Assert.True(Math.Abs(own.OptionPrice(0.03, OptionType.Call) - zabr.OptionPrice(0.03, OptionType.Call)) <= 1e-8);
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= 1e-13 * Math.Abs(expected), $"{actual} against QuantLib's {expected}");
}
