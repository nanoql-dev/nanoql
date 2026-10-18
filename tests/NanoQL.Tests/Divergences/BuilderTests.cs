// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's builders (<c>MakeSchedule</c>, <c>FixedRateLeg</c>) change themselves and return a reference to
/// themselves, and convert implicitly. nanoql's return a modified copy, so a builder kept and reused never changes
/// under its holder, and convert through <c>ToSchedule()</c> and <c>ToLeg()</c>, which throw where QuantLib's
/// conversions throw.
/// </summary>
public class BuilderTests
{
    private static readonly Date Start = new(15, Month.January, 2027);
    private static readonly Date End = new(15, Month.January, 2030);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Start };

    [Fact]
    public void AMakeScheduleReusedAfterAWithCallIsUnchanged()
    {
        MakeSchedule builder = new MakeSchedule().From(Start).To(End).WithFrequency(Frequency.Semiannual);
        ImmutableArray<Date> before = builder.ToSchedule().Dates;

        MakeSchedule other = builder.WithCalendar(new TARGET()).WithConvention(BusinessDayConvention.Following)
            .WithTenor(new Period(3, TimeUnit.Months)).Forwards().EndOfMonth();

        Assert.NotSame(builder, other);
        Schedule schedule = builder.ToSchedule();
        Assert.Equal(before, schedule.Dates);
        Assert.Equal(new NullCalendar(), schedule.Calendar);
        Assert.Equal(BusinessDayConvention.Unadjusted, schedule.BusinessDayConvention);
        Assert.Equal(new Period(6, TimeUnit.Months), schedule.Tenor());
        Assert.Equal(DateGenerationRule.Backward, schedule.Rule());
        Assert.False(schedule.EndOfMonth());

        Schedule changed = other.ToSchedule();
        Assert.Equal(new TARGET(), changed.Calendar);
        Assert.Equal(new Period(3, TimeUnit.Months), changed.Tenor());
        Assert.Equal(DateGenerationRule.Forward, changed.Rule());
    }

    [Fact]
    public void AFixedRateLegReusedAfterAWithCallIsUnchanged()
    {
        Schedule schedule = new MakeSchedule().From(Start).To(End).WithFrequency(Frequency.Annual).ToSchedule();
        FixedRateLeg builder = new FixedRateLeg(schedule).WithNotionals(100.0).WithCouponRates(0.03, new Actual360());
        double[] before = [.. builder.ToLeg().Select(cf => cf.Amount(Context))];

        FixedRateLeg other = builder.WithNotionals(200.0).WithCouponRates(0.05, new Actual365Fixed())
            .WithPaymentLag(2).WithFirstPeriodDayCounter(new Thirty360(Thirty360Convention.BondBasis));

        Assert.NotSame(builder, other);
        Assert.Equal(before, builder.ToLeg().Select(cf => cf.Amount(Context)));
        Assert.All(builder.ToLeg(), cf => Assert.Equal(100.0, ((Coupon)cf).Nominal(Context)));
        Assert.All(other.ToLeg(), cf => Assert.Equal(200.0, ((Coupon)cf).Nominal(Context)));
    }

    [Fact]
    public void TheInterestRateOverloadsBuildTheSameLeg()
    {
        // withCouponRates(const InterestRate&) and (const std::vector<InterestRate>&) set the same rates as the
        // overloads taking a rate and its conventions (fixedratecoupon.cpp).
        Schedule schedule = new MakeSchedule().From(Start).To(End).WithFrequency(Frequency.Semiannual).ToSchedule();
        FixedRateLeg builder = new FixedRateLeg(schedule).WithNotionals(100.0);
        DayCounter dc = new Actual360();
        var rate = new InterestRate(0.03, dc, Compounding.Compounded, Frequency.Semiannual);
        var other = new InterestRate(0.04, dc, Compounding.Compounded, Frequency.Semiannual);

        Assert.Equal(
            Amounts(builder.WithCouponRates(0.03, dc, Compounding.Compounded, Frequency.Semiannual)),
            Amounts(builder.WithCouponRates(rate)));
        Assert.Equal(
            Amounts(builder.WithCouponRates([0.03, 0.04], dc, Compounding.Compounded, Frequency.Semiannual)),
            Amounts(builder.WithCouponRates([rate, other])));
        Assert.All(
            builder.WithCouponRates([rate, other]).ToLeg().Skip(1),
            cf => Assert.Equal(other, ((FixedRateCoupon)cf).InterestRate));
    }

    [Fact]
    public void ToScheduleThrowsWhereQuantLibsConversionThrows()
    {
        // schedule.cpp's operator Schedule: QL_REQUIRE on the effective date, the termination date and the tenor.
        var noEffective = Assert.Throws<NanoQLException>(() =>
            new MakeSchedule().To(End).WithFrequency(Frequency.Annual).ToSchedule());
        Assert.Equal("effective date not provided", noEffective.Message);

        var noTermination = Assert.Throws<NanoQLException>(() =>
            new MakeSchedule().From(Start).WithFrequency(Frequency.Annual).ToSchedule());
        Assert.Equal("termination date not provided", noTermination.Message);

        var noTenor = Assert.Throws<NanoQLException>(() => new MakeSchedule().From(Start).To(End).ToSchedule());
        Assert.Equal("tenor/frequency not provided", noTenor.Message);
    }

    [Fact]
    public void ToLegThrowsWhereQuantLibsConversionThrows()
    {
        // fixedratecoupon.cpp's operator Leg: QL_REQUIRE on the coupon rates and the notionals.
        Schedule schedule = new MakeSchedule().From(Start).To(End).WithFrequency(Frequency.Annual).ToSchedule();

        var noRates = Assert.Throws<NanoQLException>(() => new FixedRateLeg(schedule).WithNotionals(100.0).ToLeg());
        Assert.Equal("no coupon rates given", noRates.Message);

        var noNotional = Assert.Throws<NanoQLException>(() =>
            new FixedRateLeg(schedule).WithCouponRates(0.03, new Actual360()).ToLeg());
        Assert.Equal("no notional given", noNotional.Message);
    }

    private static double[] Amounts(FixedRateLeg builder) => [.. builder.ToLeg().Select(cf => cf.Amount(Context))];
}
