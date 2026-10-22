// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The equity index's forecast on curves dated before the evaluation date. QuantLib's formula is
/// I(t, T) = I(t, t) P_D(t, T) / P_R(t, T) from the evaluation date t (<c>equityindex.hpp:42-50</c>), but its forecast
/// reads each curve's discount at T from the curve's own reference date (<c>equityindex.cpp:84-88</c>), so a curve
/// dated before t carries its discount from its own date to t into the forward. nanoql reads each curve from t, its
/// discount at T over its discount at t, nothing accruing before its reference date; QuantLib's own forecast on the
/// curves restated at t is the check, and on a curve dated on or after t the read is QuantLib's bit for bit. A fixing
/// before a later-dated curve's reference date reads that curve as accruing nothing yet, where QuantLib's discount
/// refuses the date ("negative time").
/// </summary>
public class EquityIndexDatedCurveTests
{
    private static readonly Date Today = new(21, Month.October, 2026);
    private static readonly Date FixingDate = new(21, Month.October, 2027);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void BothCurvesDatedBeforeTheEvaluationDate()
    {
        // QuantLib: 103.2998515876698; its own on the curves restated at the evaluation date, 103.0454533953517
        double forecast = Forecast(Today - 30, Today - 30);
        Assert.NotEqual(103.2998515876698, forecast);
        AssertClose(103.0454533953517, forecast);
    }

    [Fact]
    public void OnlyTheRiskFreeCurveDatedBeforeTheEvaluationDate()
    {
        // QuantLib: 103.46979920758841; its own on the restated curve, 103.0454533953517
        double forecast = Forecast(Today - 30, Today);
        Assert.NotEqual(103.46979920758841, forecast);
        AssertClose(103.0454533953517, forecast);
    }

    [Fact]
    public void ACurveDatedAfterTheEvaluationDateReadsAsQuantLibs()
    {
        // QuantLib: 103.05674666657667, and 103.0454533953517 with both curves dated at the evaluation date
        Assert.Equal(103.05674666657667, Forecast(Today, Today + 2));
        Assert.Equal(103.0454533953517, Forecast(Today, Today));

        // a fixing before the later curve's date reads that curve as accruing nothing yet, where QuantLib's discount
        // refuses the date ("negative time"): the forward is the risk-free curve's alone
        double early = Forecast(Today, Today + 2, Today + 1);
        YieldTermStructure r = new FlatForward(Today, 0.05, new Actual365Fixed());
        Assert.Equal(100.0 * 1.0 / r.Discount(Today + 1), early);
    }

    [Fact]
    public void ACurveWhoseDiscountAtItsOwnDateIsntOneReadsAsQuantLibs()
    {
        // QuantLib's forecast reads each curve's discount at T alone; a curve dated at the evaluation date is read so,
        // whatever its discount at its own date (here 0, as QuantLib's unconstrained fitted bond curve's)
        YieldTermStructure r = new ZeroAtItsDate(Today, 0.05);
        YieldTermStructure q = new ZeroAtItsDate(Today + 2, 0.02);
        var index = new EquityIndex("EQX", new TARGET(), new EURCurrency(), r, q, 100.0);
        Assert.Equal(0.0, r.Discount(Today));
        Assert.Equal(100.0 * q.Discount(FixingDate) / r.Discount(FixingDate), index.Fixing(FixingDate, Context));
    }

    private static double Forecast(Date riskFreeDate, Date dividendDate, Date? fixingDate = null)
    {
        YieldTermStructure r = new FlatForward(riskFreeDate, 0.05, new Actual365Fixed());
        YieldTermStructure q = new FlatForward(dividendDate, 0.02, new Actual365Fixed());
        var index = new EquityIndex("EQX", new TARGET(), new EURCurrency(), r, q, 100.0);
        return index.Fixing(fixingDate ?? FixingDate, Context);
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.True(
            Math.Abs(actual - expected) <= 1e-12 * Math.Abs(expected),
            $"expected {expected:R}, got {actual:R}");

    // A flat curve whose discount at its own date is 0.
    private sealed class ZeroAtItsDate(Date referenceDate, double rate)
        : YieldTermStructure(referenceDate, null, new Actual365Fixed())
    {
        public override Date MaxDate => Date.MaxDate;

        protected override double DiscountImpl(double t) => t == 0.0 ? 0.0 : Math.Exp(-rate * t);
    }
}
