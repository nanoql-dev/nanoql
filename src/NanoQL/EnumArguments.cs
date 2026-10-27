// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
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

    /// <summary>Refuses a QD+ solver type outside <see cref="QdPlusAmericanEngineSolverType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(QdPlusAmericanEngineSolverType value, string paramName)
    {
        if (value is < QdPlusAmericanEngineSolverType.Brent or > QdPlusAmericanEngineSolverType.SuperHalley)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown solver type");
        }
    }

    /// <summary>Refuses a fixed-point equation outside <see cref="QdFpAmericanEngineFixedPointEquation"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib has no text for it: its engine reads any other value as equation B.</remarks>
    internal static void Check(QdFpAmericanEngineFixedPointEquation value, string paramName)
    {
        if (value is < QdFpAmericanEngineFixedPointEquation.FpA or > QdFpAmericanEngineFixedPointEquation.Auto)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown fixed point equation");
        }
    }

    /// <summary>Refuses an exercise type outside <see cref="ExerciseType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib has no text for it: <c>Exercise::Type</c> has no stream operator and no switch refuses it.</remarks>
    internal static void Check(ExerciseType value, string paramName)
    {
        if (value is < ExerciseType.American or > ExerciseType.European)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown exercise type ({(int)value})");
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

    /// <summary>Refuses a duration type outside <see cref="DurationType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(DurationType value, string paramName)
    {
        if (value is < DurationType.Simple or > DurationType.Modified)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown duration type");
        }
    }

    /// <summary>Refuses a replication strategy outside <see cref="ReplicationType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib stores any value and refuses it in its stream operator: its text is that refusal's.</remarks>
    internal static void Check(ReplicationType value, string paramName)
    {
        if (value is < ReplicationType.Sub or > ReplicationType.Super)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown Replication Type ({(int)value})");
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

    /// <summary>Refuses a yield-curve model outside <see cref="GFunctionFactoryYieldCurveModel"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "unknown/illegal
    /// gFunction type" (<c>conundrumpricer.cpp:144</c>).</exception>
    internal static void Check(GFunctionFactoryYieldCurveModel value, string paramName)
    {
        if (value is < GFunctionFactoryYieldCurveModel.Standard or > GFunctionFactoryYieldCurveModel.NonParallelShifts)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown/illegal gFunction type");
        }
    }

    /// <summary>Refuses a calibration type outside <see cref="CmsMarketCalibrationCalibrationType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "unknown/illegal
    /// calibration type" (<c>cmsmarketcalibration.cpp:156</c>).</exception>
    internal static void Check(CmsMarketCalibrationCalibrationType value, string paramName)
    {
        if (value is < CmsMarketCalibrationCalibrationType.OnSpread or > CmsMarketCalibrationCalibrationType.OnForwardCmsPrice)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown/illegal calibration type");
        }
    }

    /// <summary>Refuses a strategy outside <see cref="LinearTsrPricerSettingsStrategy"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "Unknown strategy"
    /// (<c>lineartsrpricer.cpp:346</c>).</exception>
    internal static void Check(LinearTsrPricerSettingsStrategy value, string paramName)
    {
        if (value is < LinearTsrPricerSettingsStrategy.RateBound or > LinearTsrPricerSettingsStrategy.BSStdDevs)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Unknown strategy ({(int)value})");
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

    /// <summary>Refuses a settlement type outside <see cref="SettlementType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(SettlementType value, string paramName)
    {
        if (value is not (SettlementType.Physical or SettlementType.Cash))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown Settlement::Type({(int)value})");
        }
    }

    /// <summary>Refuses a settlement method outside <see cref="SettlementMethod"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(SettlementMethod value, string paramName)
    {
        if (value is < SettlementMethod.PhysicalOTC or > SettlementMethod.ParYieldCurve)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown Settlement::Method({(int)value})");
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

    /// <summary>Refuses a futures type outside <see cref="FuturesType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(FuturesType value, string paramName)
    {
        if (value is not (FuturesType.IMM or FuturesType.ASX or FuturesType.Custom))
        {
            // QuantLib's text embeds the type's stream text, "Unknown(n)" for a value outside the members
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown futures type (Unknown({(int)value}))");
        }
    }

    /// <summary>Refuses a bond price type outside <see cref="BondPriceType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>
    /// QuantLib stores any value and refuses it only where a switch reads it: its text is the callable bond's
    /// (<c>callablebond.cpp:130</c>).
    /// </remarks>
    internal static void Check(BondPriceType value, string paramName)
    {
        if (value is not (BondPriceType.Dirty or BondPriceType.Clean))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown price type");
        }
    }

    /// <summary>Refuses a callability type outside <see cref="CallabilityType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>
    /// QuantLib stores any value and refuses it where the callable bond's tree applies it: its text is that refusal's
    /// (<c>discretizedcallablefixedratebond.cpp:191</c>).
    /// </remarks>
    internal static void Check(CallabilityType value, string paramName)
    {
        if (value is not (CallabilityType.Call or CallabilityType.Put))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown callability type");
        }
    }

    /// <summary>
    /// Refuses a CPI interpolation type outside <see cref="CPIInterpolationType"/>, QuantLib's deprecated
    /// <c>AsIndex</c> (0) among them, which isn't ported.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(CPIInterpolationType value, string paramName)
    {
        if (value is not (CPIInterpolationType.Flat or CPIInterpolationType.Linear))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown CPI interpolation type: {(int)value}");
        }
    }

    /// <summary>Refuses a time extrapolation outside <see cref="BlackVolTimeExtrapolationType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>
    /// QuantLib stores any value and refuses it where the structure extrapolates: its text is that refusal's
    /// (<c>blackvoltimeextrapolation.cpp:75</c>).
    /// </remarks>
    internal static void Check(BlackVolTimeExtrapolationType value, string paramName)
    {
        if (value is < BlackVolTimeExtrapolationType.FlatVolatility or > BlackVolTimeExtrapolationType.LinearVariance)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown extrapolation type");
        }
    }

    /// <summary>Refuses a strike extrapolation outside <see cref="BlackVarianceSurfaceExtrapolation"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>
    /// QuantLib stores any value and compares it with <c>ConstantExtrapolation</c> alone, so any other value
    /// extrapolates as the interpolator does; nanoql refuses a value outside the type, as for every stored enum.
    /// </remarks>
    internal static void Check(BlackVarianceSurfaceExtrapolation value, string paramName)
    {
        if (value is not (BlackVarianceSurfaceExtrapolation.ConstantExtrapolation
            or BlackVarianceSurfaceExtrapolation.InterpolatorDefaultExtrapolation))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown extrapolation type");
        }
    }

    /// <summary>Refuses an eigenvector calculation outside
    /// <see cref="TqrEigenDecompositionEigenVectorCalculation"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib computes the first row's eigenvectors for any value but the other two members.</remarks>
    internal static void Check(TqrEigenDecompositionEigenVectorCalculation value, string paramName)
    {
        if (value is < TqrEigenDecompositionEigenVectorCalculation.WithEigenVector
            or > TqrEigenDecompositionEigenVectorCalculation.OnlyFirstRowEigenVector)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown eigenvector calculation");
        }
    }

    /// <summary>Refuses a shift strategy outside <see cref="TqrEigenDecompositionShiftStrategy"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib overrelaxes for any value but the other two members.</remarks>
    internal static void Check(TqrEigenDecompositionShiftStrategy value, string paramName)
    {
        if (value is < TqrEigenDecompositionShiftStrategy.NoShift or > TqrEigenDecompositionShiftStrategy.CloseEigenValue)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown shift strategy");
        }
    }

    /// <summary>Refuses an implicit scheme's solver outside <see cref="ImplicitEulerSchemeSolverType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib fails "unknown/illegal solver type" at a multi-directional step.</remarks>
    internal static void Check(ImplicitEulerSchemeSolverType value, string paramName)
    {
        if (value is < ImplicitEulerSchemeSolverType.BiCGstab or > ImplicitEulerSchemeSolverType.GMRES)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown/illegal solver type");
        }
    }

    /// <summary>Refuses a TR-BDF2 scheme's solver outside <see cref="TrBDF2SchemeSolverType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib fails "unknown/illegal solver type" at a multi-directional step.</remarks>
    internal static void Check(TrBDF2SchemeSolverType value, string paramName)
    {
        if (value is < TrBDF2SchemeSolverType.BiCGstab or > TrBDF2SchemeSolverType.GMRES)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown/illegal solver type");
        }
    }

    /// <summary>Refuses a scheme type outside <see cref="FdmSchemeDescFdmSchemeType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's backward solver fails "Unknown scheme type" at its rollback.</remarks>
    internal static void Check(FdmSchemeDescFdmSchemeType value, string paramName)
    {
        if (value is < FdmSchemeDescFdmSchemeType.HundsdorferType or > FdmSchemeDescFdmSchemeType.CrankNicolsonType)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Unknown scheme type");
        }
    }

    /// <summary>Refuses a cash dividend model outside
    /// <see cref="FdBlackScholesVanillaEngineCashDividendModel"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's engine fails "unknwon cash dividend model" at its calculation.</remarks>
    internal static void Check(FdBlackScholesVanillaEngineCashDividendModel value, string paramName)
    {
        if (value is < FdBlackScholesVanillaEngineCashDividendModel.Spot
            or > FdBlackScholesVanillaEngineCashDividendModel.Escrowed)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknwon cash dividend model");
        }
    }

    /// <summary>Refuses a Heston discretization outside <see cref="HestonProcessDiscretization"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's process fails "unknown discretization schema" when it evolves.</remarks>
    internal static void Check(HestonProcessDiscretization value, string paramName)
    {
        if (value is < HestonProcessDiscretization.PartialTruncation
            or > HestonProcessDiscretization.BroadieKayaExactSchemeTrapezoidal)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown discretization schema");
        }
    }

    /// <summary>Refuses a GJR-GARCH discretization outside <see cref="GJRGARCHProcessDiscretization"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's process fails "unknown discretization schema" when it evolves.</remarks>
    internal static void Check(GJRGARCHProcessDiscretization value, string paramName)
    {
        if (value is < GJRGARCHProcessDiscretization.PartialTruncation or > GJRGARCHProcessDiscretization.Reflection)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown discretization schema");
        }
    }

    /// <summary>Refuses a hybrid Heston-Hull-White discretization outside
    /// <see cref="HybridHestonHullWhiteProcessDiscretization"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's process fails "unknown discretization scheme" when it evolves.</remarks>
    internal static void Check(HybridHestonHullWhiteProcessDiscretization value, string paramName)
    {
        if (value is < HybridHestonHullWhiteProcessDiscretization.Euler or > HybridHestonHullWhiteProcessDiscretization.BSMHullWhite)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown discretization scheme");
        }
    }

    /// <summary>Refuses a position outside <see cref="PositionType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's replicating variance-swap engine fails "Unknown position" when it prices.</remarks>
    internal static void Check(PositionType value, string paramName)
    {
        if (value is < PositionType.Long or > PositionType.Short)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Unknown position");
        }
    }

    /// <summary>Refuses a complex-log formula outside <see cref="AnalyticHestonEngineComplexLogFormula"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's engine fails "unknown complex log formula" when it prices.</remarks>
    internal static void Check(AnalyticHestonEngineComplexLogFormula value, string paramName)
    {
        if (value is < AnalyticHestonEngineComplexLogFormula.Gatheral or > AnalyticHestonEngineComplexLogFormula.OptimalCV)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown complex log formula");
        }
    }

    /// <summary>Refuses a formulation outside <see cref="AnalyticPTDHestonEngineComplexLogFormula"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's engine fails "unknown complex log formula" when it prices.</remarks>
    internal static void Check(AnalyticPTDHestonEngineComplexLogFormula value, string paramName)
    {
        if (value is < AnalyticPTDHestonEngineComplexLogFormula.Gatheral or > AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown complex log formula");
        }
    }

    /// <summary>Refuses a route outside <see cref="AnalyticRoughHestonEngineApproximation"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's engine fails "unknown approximation: " and the value when it prices.</remarks>
    internal static void Check(AnalyticRoughHestonEngineApproximation value, string paramName)
    {
        if (value is < AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector or > AnalyticRoughHestonEngineApproximation.Lifted)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown approximation");
        }
    }

    /// <summary>Refuses an expansion outside <see cref="HestonExpansionEngineHestonExpansionFormula"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's engine fails "unknown expansion formula" when it prices.</remarks>
    internal static void Check(HestonExpansionEngineHestonExpansionFormula value, string paramName)
    {
        if (value is < HestonExpansionEngineHestonExpansionFormula.LPP2 or > HestonExpansionEngineHestonExpansionFormula.Forde)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown expansion formula");
        }
    }

    /// <summary>Refuses a conversion type outside <see cref="MoneyConversionType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>
    /// QuantLib reads any other value as no conversion, refusing a mismatch with "currency mismatch and no conversion
    /// specified" (<c>money.cpp:62</c>, <c>:81</c>); the text names the value.
    /// </remarks>
    internal static void Check(MoneyConversionType value, string paramName)
    {
        if (value is < MoneyConversionType.NoConversion or > MoneyConversionType.AutomatedConversion)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"unknown conversion type ({(int)value})");
        }
    }

    /// <summary>Refuses an exchange-rate type outside <see cref="ExchangeRateType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib's <c>ExchangeRate::exchange</c> fails "unknown exchange-rate type"
    /// (<c>exchangerate.cpp:46</c>).</remarks>
    internal static void Check(ExchangeRateType value, string paramName)
    {
        if (value is < ExchangeRateType.Direct or > ExchangeRateType.Derived)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown exchange-rate type");
        }
    }

    /// <summary>Refuses a swap type outside <see cref="MtMCrossCurrencyBasisSwapType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib reads any value but <c>PayFxBaseCurrency</c> as receiving the base-currency leg
    /// (<c>mtmcrosscurrencybasisswap.hpp:159</c>).</remarks>
    internal static void Check(MtMCrossCurrencyBasisSwapType value, string paramName)
    {
        if (value is < MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency
            or > MtMCrossCurrencyBasisSwapType.ReceiveFxBaseCurrency)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown MtM cross-currency basis swap type");
        }
    }

    /// <summary>Refuses a barrier type outside <see cref="BarrierType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "unknown type"
    /// (<c>barrieroption.cpp:108-117</c>).</exception>
    internal static void Check(BarrierType value, string paramName)
    {
        if (value is < BarrierType.DownIn or > BarrierType.UpOut)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown type");
        }
    }

    /// <summary>Refuses an average type outside <see cref="AverageType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "Unrecognised average
    /// type, must be Average::Arithmetic or Average::Geometric" (<c>asianoption.cpp:49</c>).</exception>
    internal static void Check(AverageType value, string paramName)
    {
        if (value is < AverageType.Arithmetic or > AverageType.Geometric)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, "Unrecognised average type, must be Average::Arithmetic or Average::Geometric");
        }
    }

    /// <summary>Refuses a GARCH(1,1) mode outside <see cref="Models.Garch11Mode"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(Models.Garch11Mode value, string paramName)
    {
        if (value is < Models.Garch11Mode.MomentMatchingGuess or > Models.Garch11Mode.DoubleOptimization)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown GARCH(1,1) mode");
        }
    }

    /// <summary>Refuses an interval price type outside <see cref="IntervalPriceType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "Unknown price type"
    /// (<c>prices.cpp:75</c>).</exception>
    internal static void Check(IntervalPriceType value, string paramName)
    {
        if (value is < IntervalPriceType.Open or > IntervalPriceType.Low)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Unknown price type");
        }
    }

    /// <summary>Refuses a double barrier type outside <see cref="DoubleBarrierType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "Invalid barrier type"
    /// (<c>doublebarrieroption.cpp:98-102</c>).</exception>
    internal static void Check(DoubleBarrierType value, string paramName)
    {
        if (value is < DoubleBarrierType.KnockIn or > DoubleBarrierType.KOKI)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Invalid barrier type");
        }
    }

    /// <summary>Refuses a partial-time barrier range outside <see cref="PartialBarrierRange"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "invalid barrier range"
    /// (<c>analyticpartialtimebarrieroptionengine.cpp:55-56</c>).</exception>
    internal static void Check(PartialBarrierRange value, string paramName)
    {
        if (value is not (PartialBarrierRange.Start or PartialBarrierRange.EndB1 or PartialBarrierRange.EndB2))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "invalid barrier range");
        }
    }

    /// <summary>Refuses a delta convention outside <see cref="DeltaVolQuoteDeltaType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "invalid delta type"
    /// (<c>blackdeltacalculator.cpp:70</c>).</exception>
    internal static void Check(DeltaVolQuoteDeltaType value, string paramName)
    {
        if (value is < DeltaVolQuoteDeltaType.Spot or > DeltaVolQuoteDeltaType.PaFwd)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "invalid delta type");
        }
    }

    /// <summary>Refuses an at-the-money convention outside <see cref="DeltaVolQuoteAtmType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "invalid atm type"
    /// (<c>blackdeltacalculator.cpp:183</c>).</exception>
    internal static void Check(DeltaVolQuoteAtmType value, string paramName)
    {
        if (value is < DeltaVolQuoteAtmType.AtmNull or > DeltaVolQuoteAtmType.AtmPutCall50)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "invalid atm type");
        }
    }

    /// <summary>Refuses a strike extrapolation outside <see cref="FixedLocalVolSurfaceExtrapolation"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    internal static void Check(FixedLocalVolSurfaceExtrapolation value, string paramName)
    {
        if (value is < FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation
            or > FixedLocalVolSurfaceExtrapolation.InterpolatorDefaultExtrapolation)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown extrapolation type");
        }
    }

    /// <summary>Refuses a smile interpolation outside <see cref="BlackVolatilitySurfaceDeltaSmileInterpolationMethod"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's "Invalid method", which
    /// QuantLib raises where the smile is built (<c>blackvolsurfacedelta.cpp:200</c>).</exception>
    internal static void Check(BlackVolatilitySurfaceDeltaSmileInterpolationMethod value, string paramName)
    {
        if (value is < BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear
            or > BlackVolatilitySurfaceDeltaSmileInterpolationMethod.CubicSpline)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Invalid method {(int)value}");
        }
    }

    /// <summary>Refuses an approximation order outside <see cref="OperatorSplittingSpreadEngineOrder"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's text, which QuantLib
    /// raises where the engine prices (<c>operatorsplittingspreadengine.cpp:70</c>).</exception>
    internal static void Check(OperatorSplittingSpreadEngineOrder value, string paramName)
    {
        if (value is < OperatorSplittingSpreadEngineOrder.First or > OperatorSplittingSpreadEngineOrder.Second)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown approximation type");
        }
    }

    /// <summary>Refuses a cash-dividend model outside <see cref="CashDividendEuropeanEngineCashDividendModel"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member.</exception>
    /// <remarks>QuantLib has no text for it: its engine reads any other value as the spot model.</remarks>
    internal static void Check(CashDividendEuropeanEngineCashDividendModel value, string paramName)
    {
        if (value is < CashDividendEuropeanEngineCashDividendModel.Spot or > CashDividendEuropeanEngineCashDividendModel.Escrowed)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown cash dividend model");
        }
    }

    /// <summary>Refuses a payoff type outside <see cref="PerpetualFuturesPayoffType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's text, which QuantLib
    /// raises where the arguments are validated (<c>perpetualfutures.cpp:57</c>).</exception>
    internal static void Check(PerpetualFuturesPayoffType value, string paramName)
    {
        if (value is < PerpetualFuturesPayoffType.Linear or > PerpetualFuturesPayoffType.Quanto)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown payoff type");
        }
    }

    /// <summary>Refuses a funding type outside <see cref="PerpetualFuturesFundingType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's text, which QuantLib
    /// raises where the arguments are validated (<c>perpetualfutures.cpp:65</c>).</exception>
    internal static void Check(PerpetualFuturesFundingType value, string paramName)
    {
        if (value is < PerpetualFuturesFundingType.FundingWithPreviousSpot or > PerpetualFuturesFundingType.FundingWithCurrentSpot)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "unknown funding type");
        }
    }

    /// <summary>Refuses an interpolation type outside <see cref="DiscountingPerpetualFuturesEngineInterpolationType"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a member, with QuantLib's text, which QuantLib
    /// raises where the engine prices (<c>discountingperpetualfuturesengine.cpp:256</c>).</exception>
    internal static void Check(DiscountingPerpetualFuturesEngineInterpolationType value, string paramName)
    {
        if (value is < DiscountingPerpetualFuturesEngineInterpolationType.PiecewiseConstant
            or > DiscountingPerpetualFuturesEngineInterpolationType.CubicSpline)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Unknown interpolation type");
        }
    }
}
