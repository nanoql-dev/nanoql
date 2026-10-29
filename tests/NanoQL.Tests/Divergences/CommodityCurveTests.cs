// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The commodity curve's node times from its reference date, and its basis curve read by date (the S-xrt plan's XRR5
/// (x) and (xi), fixed under R8). QuantLib's moving-date curve follows the evaluation date, and its <c>setPrices</c>
/// times the nodes from the first price's date (<c>commoditycurve.cpp:68-87</c>): with prices 1, 2 and 3 at ten, forty
/// and seventy days it prices 2 at 35 and 39 days and 3 at 65 and 69, the next node's, where its constructor from the
/// same dates prices 1 and 2 (the run's <c>probes\xi_c1.cpp</c>). And it reads a basis curve at its own time, on its own
/// reference date and day counter (<c>commoditycurve.hpp:205-219</c>): a basis dated thirty days later on Actual/360,
/// at 1, 2 and 3 from thirty, sixty and 120 days, adds 2 at 45 and 59 days and 3 at 119, where the basis curve's own
/// price there is 1, 1 and 2. nanoql times the nodes from the reference date (<see cref="CommodityCurve.Create"/>, a copy
/// from <see cref="CommodityCurve.SetPrices"/>) and reads a basis at the date on its own clock; where the curves share
/// their reference date and day counter both readings are QuantLib's.
/// </summary>
public class CommodityCurveTests
{
    [Theory]
    [InlineData(0, 1.0, 1.0)]
    [InlineData(20, 1.0, 1.0)]
    [InlineData(35, 1.0, 2.0)]
    [InlineData(39, 1.0, 2.0)]
    [InlineData(40, 2.0, 2.0)]
    [InlineData(65, 2.0, 3.0)]
    [InlineData(69, 2.0, 3.0)]
    [InlineData(70, 3.0, 3.0)]
    [InlineData(100, 3.0, 3.0)]
    public void TheMovingCurvePricesAsTheCurveFromItsDates(int days, double expected, double quantLib)
    {
        CommodityCurve moving = CommodityCurve.Create(
                "C2", None, new USDCurrency(), new BarrelUnitOfMeasure(), new NullCalendar(), Context())
            .SetPrices(new Dictionary<Date, double> { [Today + 10] = 1.0, [Today + 40] = 2.0, [Today + 70] = 3.0 });
        var fromDates = new CommodityCurve(
            "C1", None, new USDCurrency(), new BarrelUnitOfMeasure(), new NullCalendar(),
            [Today + 10, Today + 40, Today + 70], [1.0, 2.0, 3.0]);
        Assert.Equal(Today, moving.ReferenceDate);
        Assert.Equal(expected, moving.Price(Today + days, null, 0));
        Assert.Equal(fromDates.Price(Today + days, null, 0), moving.Price(Today + days, null, 0));
        Assert.Equal(days is 35 or 39 or 65 or 69, expected != quantLib);
    }

    [Theory]
    [InlineData(0, 1.0, 1.0)]
    [InlineData(45, 1.0, 2.0)]
    [InlineData(59, 1.0, 2.0)]
    [InlineData(60, 2.0, 2.0)]
    [InlineData(119, 2.0, 3.0)]
    [InlineData(120, 3.0, 3.0)]
    public void ABasisCurveDatedApartIsReadAtTheDate(int days, double basis, double quantLibBasis)
    {
        var outer = new CommodityCurve(
            "OUT", None, new USDCurrency(), new BarrelUnitOfMeasure(), new NullCalendar(), [Today, Today + 365], [70.0, 72.0]);
        var basisCurve = new CommodityCurve(
            "BAS", None, new USDCurrency(), new BarrelUnitOfMeasure(), new NullCalendar(),
            [Today + 30, Today + 60, Today + 120], [1.0, 2.0, 3.0], new Actual360());
        CommodityCurve curve = outer.SetBasisOfCurve(basisCurve, Context());
        Assert.Equal(basis, curve.BasisOfPrice(Today + days));
        Assert.Equal(basisCurve.Price(Today + days, null, 0), curve.BasisOfPrice(Today + days));
        Assert.Equal(70.0 + basis, curve.Price(Today + days, null, 0));
        Assert.Equal(days is 45 or 59 or 119, basis != quantLibBasis);
    }
}
