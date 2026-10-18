// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// Checks of enum arguments: C# lets any integer be cast to an enum, so a member that switches on, stores or computes
/// with an enum argument refuses a value outside the type's members, naming the parameter.
/// </summary>
/// <remarks>
/// The checks compare against the members explicitly rather than calling <see cref="Enum.IsDefined{TEnum}(TEnum)"/>,
/// as the .NET design guidelines advise, and use QuantLib's message for the same failure. A member that also refuses
/// some of the type's members checks those itself (<see cref="Period.FromFrequency"/> refuses
/// <see cref="Frequency.OtherFrequency"/>).
/// </remarks>
internal static class EnumArguments
{
    /// <summary>Refuses a compounding rule outside <see cref="Compounding"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(Compounding value, string paramName)
    {
        if (value is < Compounding.Simple or > Compounding.CompoundedThenSimple)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown compounding convention");
        }
    }

    /// <summary>Refuses a frequency outside <see cref="Frequency"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(Frequency value, string paramName)
    {
        switch (value)
        {
            case Frequency.NoFrequency:
            case Frequency.Once:
            case Frequency.Annual:
            case Frequency.Semiannual:
            case Frequency.EveryFourthMonth:
            case Frequency.Quarterly:
            case Frequency.Bimonthly:
            case Frequency.Monthly:
            case Frequency.EveryFourthWeek:
            case Frequency.Biweekly:
            case Frequency.Weekly:
            case Frequency.Daily:
            case Frequency.OtherFrequency:
                return;
            default:
                throw new ArgumentOutOfRangeException(paramName, value, $"unknown frequency ({(int)value})");
        }
    }

    /// <summary>Refuses a time unit outside <see cref="TimeUnit"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(TimeUnit value, string paramName)
    {
        if (value is < TimeUnit.Days or > TimeUnit.Microseconds)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown time unit ({(int)value})");
        }
    }

    /// <summary>Refuses a business-day convention outside <see cref="BusinessDayConvention"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(BusinessDayConvention value, string paramName)
    {
        if (value is < BusinessDayConvention.Following or > BusinessDayConvention.Nearest)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown business-day convention");
        }
    }

    /// <summary>Refuses a date-generation rule outside <see cref="DateGenerationRule"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(DateGenerationRule value, string paramName)
    {
        if (value is < DateGenerationRule.Backward or > DateGenerationRule.CDS2015)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown rule ({(int)value})");
        }
    }

    /// <summary>Refuses a weekday outside <see cref="Weekday"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(Weekday value, string paramName)
    {
        if (value is < Weekday.Sunday or > Weekday.Saturday)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown weekday");
        }
    }

    /// <summary>Refuses a derivative approximation outside <see cref="CubicInterpolationDerivativeApprox"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(CubicInterpolationDerivativeApprox value, string paramName)
    {
        if (value is < CubicInterpolationDerivativeApprox.Spline or > CubicInterpolationDerivativeApprox.Harmonic)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown scheme");
        }
    }

    /// <summary>Refuses a boundary condition outside <see cref="CubicInterpolationBoundaryCondition"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(CubicInterpolationBoundaryCondition value, string paramName)
    {
        if (value is < CubicInterpolationBoundaryCondition.NotAKnot or > CubicInterpolationBoundaryCondition.Lagrange)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown end condition");
        }
    }

    /// <summary>Refuses a mixed-interpolation behavior outside <see cref="MixedInterpolationBehavior"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(MixedInterpolationBehavior value, string paramName)
    {
        // QuantLib's behavior has no stream operator: its refusal prints the integer.
        if (value is not (MixedInterpolationBehavior.ShareRanges or MixedInterpolationBehavior.SplitRanges))
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, $"unknown mixed-interpolation behavior: {(int)value}");
        }
    }

    /// <summary>Refuses a rounding method outside <see cref="RoundingType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(RoundingType value, string paramName)
    {
        if (value is < RoundingType.None or > RoundingType.Ceiling)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown rounding method");
        }
    }

    /// <summary>Refuses a volatility type outside <see cref="VolatilityType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(VolatilityType value, string paramName)
    {
        if (value is < VolatilityType.ShiftedLognormal or > VolatilityType.Normal)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Unknown volatility type ({(int)value})");
        }
    }

    /// <summary>Refuses an option type outside <see cref="OptionType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(OptionType value, string paramName)
    {
        if (value is not (OptionType.Call or OptionType.Put))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown option type");
        }
    }

    /// <summary>Refuses an averaging method outside <see cref="RateAveragingType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(RateAveragingType value, string paramName)
    {
        if (value is < RateAveragingType.Simple or > RateAveragingType.Compound)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown compounding convention ({(int)value})");
        }
    }

    /// <summary>Refuses a stub index convention outside <see cref="StubIndexSelectionConvention"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(StubIndexSelectionConvention value, string paramName)
    {
        if (value is not (StubIndexSelectionConvention.ClosestIndex or StubIndexSelectionConvention.Interpolated))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown stub index convention");
        }
    }

    /// <summary>Refuses a timing adjustment outside <see cref="BlackIborCouponPricerTimingAdjustment"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(BlackIborCouponPricerTimingAdjustment value, string paramName)
    {
        if (value is not (BlackIborCouponPricerTimingAdjustment.Black76 or BlackIborCouponPricerTimingAdjustment.BivariateLognormal))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown timing adjustment (code {(int)value})");
        }
    }

    /// <summary>Refuses a swap type outside <see cref="SwapType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(SwapType value, string paramName)
    {
        if (value is not (SwapType.Payer or SwapType.Receiver))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Unknown vanilla-swap type");
        }
    }

    /// <summary>Refuses a pillar choice outside <see cref="PillarChoice"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(PillarChoice value, string paramName)
    {
        if (value is not (PillarChoice.MaturityDate or PillarChoice.LastRelevantDate or PillarChoice.CustomDate))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown Pillar::Choice({(int)value})");
        }
    }
}
