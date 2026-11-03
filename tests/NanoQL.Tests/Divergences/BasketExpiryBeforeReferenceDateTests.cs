// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The basket engines that read their structures by date on an option expiring before a later structure's date.
/// QuantLib reads each discount, variance and deviation by date from the structure's own reference date
/// (<c>vectorbsmprocessextractor.cpp:48</c>, <c>:71</c>, <c>:80</c>, <c>:89-90</c>, which Choi's, the single-factor and
/// the n-dimensional engines read, and <c>choibasketengine.cpp:174-188</c>, Choi's nodes), so an option expiring before
/// a risk-free curve's or a volatility's date is refused with "negative time". Each read here accrues nothing before
/// its structure's date (the playbook's row for structures on another clock): the option is priced as QuantLib prices
/// the market restated from the evaluation date, a curve's discount 1 and a volatility's variance 0 up to its date, and
/// as nanoql's 2-D finite-difference engine prices it on its clock.
/// </summary>
public class BasketExpiryBeforeReferenceDateTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ARiskFreeCurveDatedAfterExpiryDiscountsNothing()
    {
        // the risk-free curve dated four days on; the option expires in two
        var later = new FlatForward(Today + 4, 0.05, A365);
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), later, Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), later, Vol(0.3));

        // restated from the evaluation date: no discount up to the curve's date
        GeneralizedBlackScholesProcess r1 = Process(100.0, Flat(0.02), Flat(0.0), Vol(0.25));
        GeneralizedBlackScholesProcess r2 = Process(95.0, Flat(0.03), Flat(0.0), Vol(0.3));

        var option = new BasketOption(AveragePut(), new EuropeanExercise(Today + 2));
        Assert.Equal(Choi(option, r1, r2), Choi(option, p1, p2));
        Assert.Equal(SingleFactor(option, r1, r2), SingleFactor(option, p1, p2));
        Assert.Equal(Ndim(option, r1, r2), Ndim(option, p1, p2));

        // the 2-D engine on its clock, within its grid's error
        double fd2d = Fd2dBlackScholesVanillaEngine.Calculate(option, Context, p1, p2, 0.5).NPV!.Value;
        Assert.Equal(fd2d, Choi(option, p1, p2), 2e-3 * fd2d);
    }

    [Fact]
    public void AVolatilityDatedAfterExpiryAccruesNoVariance()
    {
        // the second asset's volatility dated four days on; the option expires in two
        var later = new BlackConstantVol(Today + 4, new NullCalendar(), 0.3, A365);
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), Flat(0.05), later);

        // restated from the evaluation date: no variance up to the volatility's date
        GeneralizedBlackScholesProcess r2 = Process(95.0, Flat(0.03), Flat(0.05), Vol(0.0));

        var option = new BasketOption(AveragePut(), new EuropeanExercise(Today + 2));
        double choi = Choi(option, p1, r2);
        Assert.Equal(choi, Choi(option, p1, p2), 1e-12 * choi);
        double singleFactor = SingleFactor(option, p1, r2);
        Assert.Equal(singleFactor, SingleFactor(option, p1, p2), 1e-12 * singleFactor);
        double ndim = Ndim(option, p1, r2);
        Assert.Equal(ndim, Ndim(option, p1, p2), 1e-12 * ndim);
    }

    private static double Choi(BasketOption option, GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2) =>
        ChoiBasketEngine.Calculate(option, Context, [p1, p2], Rho()).NPV!.Value;

    private static double SingleFactor(
        BasketOption option, GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2) =>
        SingleFactorBsmBasketEngine.Calculate(option, Context, [p1, p2]).NPV!.Value;

    private static double Ndim(BasketOption option, GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2) =>
        FdndimBlackScholesVanillaEngine.Calculate(option, Context, [p1, p2], Rho(), 31, 25).NPV!.Value;

    private static Matrix Rho() => new(new[,] { { 1.0, 0.5 }, { 0.5, 1.0 } });
}
