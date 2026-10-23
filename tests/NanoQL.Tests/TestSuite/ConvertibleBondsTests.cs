// SPDX-FileCopyrightText: Copyright (C) 2006, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/convertiblebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testBond, which waits for S-sr's ForwardSpreadedTermStructure

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ConvertibleBondsTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly Date RunnerDate = new(16, September, 2015);

    // Adapted: CommonVars' relinkable handles are the curves, the volatility and the process themselves, and its credit
    // spread quote, relinked to 0 after the engine is built on it (line 318), is the number 0 the engine is given; the
    // engines are the static BinomialConvertibleEngine.Calculate and BinomialCRRVanillaEngine.Calculate.
    [Fact]
    public void TestOption()
    {
        // Testing zero-coupon convertible bonds against vanilla option...

        var vars = new CommonVars(RunnerDate);

        Exercise euExercise = new EuropeanExercise(vars.MaturityDate);

        vars.SettlementDays = 0;

        int timeSteps = 2001;
        BondResults Engine(ConvertibleBond b) =>
            BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(b, vars.Context, vars.Process, timeSteps, 0.0);
        OneAssetOptionResults VanillaEngine(VanillaOption o) =>
            BinomialCRRVanillaEngine.Calculate(o, vars.Context, vars.Process, timeSteps);

        double conversionStrike = vars.Redemption / vars.ConversionRatio;
        var payoff = new PlainVanillaPayoff(OptionType.Call, conversionStrike);

        Schedule schedule = new MakeSchedule().From(vars.IssueDate)
                                              .To(vars.MaturityDate)
                                              .WithFrequency(Frequency.Once)
                                              .WithCalendar(vars.Calendar)
                                              .Backwards()
                                              .ToSchedule();

        var euZero = new ConvertibleZeroCouponBond(
            euExercise, vars.ConversionRatio, vars.NoCallability, vars.IssueDate, vars.SettlementDays, vars.DayCounter,
            schedule, vars.Redemption);

        var euOption = new VanillaOption(payoff, euExercise);

        double tolerance = 5.0e-2 * (vars.FaceAmount / 100.0);

        double expected = vars.FaceAmount / 100.0
            * ((vars.Redemption * vars.RiskFreeRate.Discount(vars.MaturityDate))
               + (vars.ConversionRatio * VanillaEngine(euOption).NPV!.Value));
        double calculated = Engine(euZero).NPV!.Value;
        double error = Math.Abs(calculated - expected);
        Assert.True(
            error <= tolerance,
            $"failed to reproduce plain-option price:\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error}\n    tolerance:      {tolerance}");
    }

    // Adapted: QuantLib builds the engine's arguments by hand, its cash flows a single SimpleCashFlow of the redemption
    // at maturity; nanoql's arguments are the bond's, so the case builds the zero-coupon convertible with those terms,
    // whose one cash flow is that redemption and whose settlement date is the case's (the issue date is before it).
    [Fact]
    public void TestDividendsSpanningSettlementDate()
    {
        // Testing convertible bond dividend values spanning the settlement date...

        var vars = new CommonVars(new Date(15, January, 2024));

        Exercise exercise = new EuropeanExercise(vars.MaturityDate);
        Schedule schedule = new MakeSchedule().From(vars.IssueDate)
                                              .To(vars.MaturityDate)
                                              .WithFrequency(Frequency.Once)
                                              .WithCalendar(vars.Calendar)
                                              .Backwards()
                                              .ToSchedule();
        var bond = new ConvertibleZeroCouponBond(
            exercise, vars.ConversionRatio, vars.NoCallability, vars.IssueDate, vars.SettlementDays, vars.DayCounter,
            schedule, vars.Redemption);
        Date settlementDate = vars.Calendar.Advance(vars.Today, vars.SettlementDays, TimeUnit.Days);
        Assert.Equal(settlementDate, bond.SettlementDate(vars.Context));

        Date beforeSettlement = vars.Calendar.Advance(vars.Today, 1, TimeUnit.Days);
        Date afterSettlement = vars.Calendar.Advance(settlementDate, 1, TimeUnit.Years);
        double futureAmount = 10.0;
        ImmutableArray<Dividend> dividends = [new FixedDividend(1.0, beforeSettlement), new FixedDividend(futureAmount, afterSettlement)];

        var convertible = new DiscretizedConvertible(bond, vars.Process, dividends, vars.CreditSpread, vars.Context);

        double expected = futureAmount * vars.RiskFreeRate.Discount(afterSettlement);
        Assert.Single(convertible.DividendValues);
        Assert.True(
            Math.Abs(convertible.DividendValues[0] - expected) <= 1.0e-14 * Math.Abs(expected),
            $"{convertible.DividendValues[0]} against {expected}");
    }

    [Fact]
    public void TestRegression()
    {
        // Testing fixed-coupon convertible bond in known regression case...

        var today = new Date(23, December, 2008);
        Date tomorrow = today + 1;

        var context = new EvaluationContext { EvaluationDate = tomorrow };

        double u = 2.9084382818797443;

        var dates = new Date[25];
        var forwards = new double[25];
        dates[0] = new Date(29, December, 2008); forwards[0] = 0.0025999342800;
        dates[1] = new Date(5, January, 2009); forwards[1] = 0.0025999342800;
        dates[2] = new Date(29, January, 2009); forwards[2] = 0.0053123275500;
        dates[3] = new Date(27, February, 2009); forwards[3] = 0.0197049598721;
        dates[4] = new Date(30, March, 2009); forwards[4] = 0.0220524845296;
        dates[5] = new Date(29, June, 2009); forwards[5] = 0.0217076395643;
        dates[6] = new Date(29, December, 2009); forwards[6] = 0.0230349627478;
        dates[7] = new Date(29, December, 2010); forwards[7] = 0.0087631647476;
        dates[8] = new Date(29, December, 2011); forwards[8] = 0.0219084299499;
        dates[9] = new Date(31, December, 2012); forwards[9] = 0.0244798766219;
        dates[10] = new Date(30, December, 2013); forwards[10] = 0.0267885498456;
        dates[11] = new Date(29, December, 2014); forwards[11] = 0.0266922867562;
        dates[12] = new Date(29, December, 2015); forwards[12] = 0.0271052126386;
        dates[13] = new Date(29, December, 2016); forwards[13] = 0.0268829891648;
        dates[14] = new Date(29, December, 2017); forwards[14] = 0.0264594744498;
        dates[15] = new Date(31, December, 2018); forwards[15] = 0.0273450367424;
        dates[16] = new Date(30, December, 2019); forwards[16] = 0.0294852614749;
        dates[17] = new Date(29, December, 2020); forwards[17] = 0.0285556119719;
        dates[18] = new Date(29, December, 2021); forwards[18] = 0.0305557764659;
        dates[19] = new Date(29, December, 2022); forwards[19] = 0.0292244738422;
        dates[20] = new Date(29, December, 2023); forwards[20] = 0.0263917004194;
        dates[21] = new Date(29, December, 2028); forwards[21] = 0.0239626970243;
        dates[22] = new Date(29, December, 2033); forwards[22] = 0.0216417108090;
        dates[23] = new Date(29, December, 2038); forwards[23] = 0.0228343838422;
        dates[24] = new Date(31, December, 2199); forwards[24] = 0.0228343838422;

        YieldTermStructure r = new ForwardCurve(dates, forwards, new Actual360());

        BlackVolTermStructure sigma = new BlackConstantVol(
            tomorrow, new NullCalendar(), 21.685235548092248, new Thirty360(Thirty360Convention.BondBasis));

        var process = new BlackProcess(u, r, sigma);

        double spread = 0.11498700678012874;

        var issueDate = new Date(23, July, 2008);
        var maturityDate = new Date(1, August, 2013);
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Schedule schedule = new MakeSchedule().From(issueDate)
                                              .To(maturityDate)
                                              .WithTenor(new Period(6, TimeUnit.Months))
                                              .WithCalendar(calendar)
                                              .WithConvention(BusinessDayConvention.Unadjusted)
                                              .ToSchedule();
        int settlementDays = 3;
        Exercise exercise = new EuropeanExercise(maturityDate);
        double conversionRatio = 100.0 / 20.3175;
        double[] coupons = [.. Enumerable.Repeat(0.05, schedule.Size - 1)];
        DayCounter dayCounter = new Thirty360(Thirty360Convention.BondBasis);
        ImmutableArray<Callability> noCallability = [];
        ImmutableArray<Dividend> noDividends = [];
        double redemption = 100.0;

        var bond = new ConvertibleFixedCouponBond(
            exercise, conversionRatio, noCallability, issueDate, settlementDays, coupons, dayCounter, schedule, redemption);

        // should throw; if not, an INF was not detected.
        Assert.ThrowsAny<NanoQLException>(
            () => BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(bond, context, process, 600, spread, noDividends));
    }

    private sealed class CommonVars
    {
        public CommonVars(Date today)
        {
            Today = today;
            Context = new EvaluationContext { EvaluationDate = today };
            Calendar = new TARGET();

            DayCounter = new Actual360();
            SettlementDays = 3;

            int fixingDays = 2;
            Date issueDate = Calendar.Advance(today, fixingDays, TimeUnit.Days);
            MaturityDate = Calendar.Advance(issueDate, 10, TimeUnit.Years);

            // reset to avoid inconsistencies as the schedule is backwards
            IssueDate = Calendar.Advance(MaturityDate, -10, TimeUnit.Years);

            double underlying = 50.0;
            YieldTermStructure dividendYield = new FlatForward(today, 0.02, DayCounter);
            RiskFreeRate = new FlatForward(today, 0.05, DayCounter);
            BlackVolTermStructure volatility = new BlackConstantVol(today, new NullCalendar(), 0.15, DayCounter);

            Process = new BlackScholesMertonProcess(underlying, dividendYield, RiskFreeRate, volatility);

            CreditSpread = 0.005;

            // it fails with 1000000
            // faceAmount = 1000000.0;
            FaceAmount = 100.0;
            Redemption = 100.0;
            ConversionRatio = Redemption / underlying;
        }

        public Date Today { get; }

        public EvaluationContext Context { get; }

        public Date IssueDate { get; }

        public Date MaturityDate { get; }

        public Calendar Calendar { get; }

        public DayCounter DayCounter { get; }

        public int SettlementDays { get; set; }

        public YieldTermStructure RiskFreeRate { get; }

        public GeneralizedBlackScholesProcess Process { get; }

        public double CreditSpread { get; }

        public ImmutableArray<Callability> NoCallability { get; } = [];

        public double FaceAmount { get; }

        public double Redemption { get; }

        public double ConversionRatio { get; }
    }
}
