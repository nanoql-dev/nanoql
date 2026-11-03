// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Coupons;

/// <summary>
/// The g-function of the forwarding curve's shifts, parallel or shaped by a mean reversion: G(Rs) = Rs·Z(x(Rs)), x the
/// shift that reprices the swap at the rate Rs, solved by Newton's method.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib memoizes the last rate's shift (<c>calibratedShift_</c>, <c>tmpRs_</c>), a function of the rate alone,
/// since the solve's guess is computed from the rate; the memo here is one immutable pair replaced atomically, so the
/// function computes the same values whatever was asked before and serves any thread. Its objective function's
/// derivative, which QuantLib keeps in a mutable member its value sets, is computed from the same terms in the same
/// order, so Newton's steps are QuantLib's.
/// </para>
/// <para>
/// The shift's shape, (1 − e^(−a·x))/a, loses digits as the reversion a nears 0 from above, and QuantLib reads a
/// negative reversion as 0; below a reversion of 1e-4 the shape is −expm1(−a·x)/a, negative reversions included, and x
/// at 0, with QuantLib's form at and above 1e-4.
/// </para>
/// </remarks>
internal sealed class GFunctionWithShifts : GFunction
{
    private const double Accuracy = 1.0e-14;

    private readonly double swapStartTime_;
    private readonly double shapedPaymentTime_;
    private readonly double[] shapedSwapPaymentTimes_;
    private readonly double[] accruals_;
    private readonly double[] swapPaymentDiscounts_;
    private readonly double discountAtStart_;
    private readonly double discountRatio_;
    private readonly double swapRateValue_;
    private readonly double meanReversion_;

    // QuantLib's memo of the last rate's shift (tmpRs_, calibratedShift_), initially (1e7, 0.03).
    private Memo memo = new(10000000.0, 0.03);

    public GFunctionWithShifts(CmsCoupon coupon, double meanReversion, EvaluationContext context)
    {
        meanReversion_ = meanReversion;

        SwapIndex swapIndex = coupon.SwapIndex;
        VanillaSwap swap = swapIndex.UnderlyingSwap(coupon.FixingDate(), context);

        swapRateValue_ = swapIndex.UnderlyingSwapResults(swap, context).FairRate
            ?? throw new NanoQLException("result not available");

        Schedule schedule = swap.FixedSchedule;
        YieldTermStructure rateCurve = swapIndex.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        DayCounter dc = swapIndex.DayCounter;

        swapStartTime_ = dc.YearFraction(rateCurve.ReferenceDate, schedule.StartDate());
        discountAtStart_ = rateCurve.Discount(schedule.StartDate());

        double paymentTime = dc.YearFraction(rateCurve.ReferenceDate, coupon.Date);

        shapedPaymentTime_ = ShapeOfShift(paymentTime);

        var fixedLeg = swap.FixedLeg;
        int n = fixedLeg.Length;
        accruals_ = new double[n];
        shapedSwapPaymentTimes_ = new double[n];
        swapPaymentDiscounts_ = new double[n];
        for (int i = 0; i < n; ++i)
        {
            var cpn = (Coupon)fixedLeg[i];
            accruals_[i] = cpn.AccrualPeriod();
            Date paymentDate = cpn.Date;
            double swapPaymentTime = dc.YearFraction(rateCurve.ReferenceDate, paymentDate);
            shapedSwapPaymentTimes_[i] = ShapeOfShift(swapPaymentTime);
            swapPaymentDiscounts_[i] = rateCurve.Discount(paymentDate);
        }

        discountRatio_ = swapPaymentDiscounts_[^1] / discountAtStart_;
    }

    // The forwarding curve's discount at the swap's start, P, which the shift models' G = P/A0 carries.
    internal double DiscountAtStart => discountAtStart_;

    public override double Evaluate(double x)
    {
        double calibratedShift = CalibrationOfShift(x);
        return x * FunctionZ(calibratedShift);
    }

    public override double FirstDerivative(double x)
    {
        double calibratedShift = CalibrationOfShift(x);
        return FunctionZ(calibratedShift) + (x * DerZ_derX(calibratedShift) / DerRs_derX(calibratedShift));
    }

    public override double SecondDerivative(double x)
    {
        double calibratedShift = CalibrationOfShift(x);
        return (2.0 * DerZ_derX(calibratedShift) / DerRs_derX(calibratedShift))
            + (x * Der2Z_derX2(calibratedShift) / Math.Pow(DerRs_derX(calibratedShift), 2.0))
            - (x * DerZ_derX(calibratedShift) * Der2Rs_derX2(calibratedShift) / Math.Pow(DerRs_derX(calibratedShift), 3.0));
    }

    private double FunctionZ(double x) =>
        Math.Exp(-shapedPaymentTime_ * x) / (1.0 - (discountRatio_ * Math.Exp(-shapedSwapPaymentTimes_[^1] * x)));

    private double DerRs_derX(double x)
    {
        double sqrtDenominator = 0;
        double derSqrtDenominator = 0;
        for (int i = 0; i < accruals_.Length; i++)
        {
            sqrtDenominator += accruals_[i] * swapPaymentDiscounts_[i] * Math.Exp(-shapedSwapPaymentTimes_[i] * x);
            derSqrtDenominator -= shapedSwapPaymentTimes_[i] * accruals_[i] * swapPaymentDiscounts_[i]
                * Math.Exp(-shapedSwapPaymentTimes_[i] * x);
        }

        double denominator = sqrtDenominator * sqrtDenominator;

        double numerator = 0;
        numerator += shapedSwapPaymentTimes_[^1] * swapPaymentDiscounts_[^1]
            * Math.Exp(-shapedSwapPaymentTimes_[^1] * x) * sqrtDenominator;
        numerator -= (discountAtStart_ - (swapPaymentDiscounts_[^1] * Math.Exp(-shapedSwapPaymentTimes_[^1] * x)))
            * derSqrtDenominator;
        if (!(denominator != 0))
        {
            throw new NanoQLException("GFunctionWithShifts::derRs_derX: denominator == 0");
        }

        return numerator / denominator;
    }

    private double Der2Rs_derX2(double x)
    {
        double denOfRfunztion = 0.0;
        double derDenOfRfunztion = 0.0;
        double der2DenOfRfunztion = 0.0;
        for (int i = 0; i < accruals_.Length; i++)
        {
            denOfRfunztion += accruals_[i] * swapPaymentDiscounts_[i] * Math.Exp(-shapedSwapPaymentTimes_[i] * x);
            derDenOfRfunztion -= shapedSwapPaymentTimes_[i] * accruals_[i] * swapPaymentDiscounts_[i]
                * Math.Exp(-shapedSwapPaymentTimes_[i] * x);
            der2DenOfRfunztion += shapedSwapPaymentTimes_[i] * shapedSwapPaymentTimes_[i] * accruals_[i]
                * swapPaymentDiscounts_[i] * Math.Exp(-shapedSwapPaymentTimes_[i] * x);
        }

        double denominator = Math.Pow(denOfRfunztion, 4);

        double numOfDerR = 0;
        numOfDerR += shapedSwapPaymentTimes_[^1] * swapPaymentDiscounts_[^1]
            * Math.Exp(-shapedSwapPaymentTimes_[^1] * x) * denOfRfunztion;
        numOfDerR -= (discountAtStart_ - (swapPaymentDiscounts_[^1] * Math.Exp(-shapedSwapPaymentTimes_[^1] * x)))
            * derDenOfRfunztion;

        double denOfDerR = Math.Pow(denOfRfunztion, 2);

        double derNumOfDerR = 0.0;
        derNumOfDerR -= shapedSwapPaymentTimes_[^1] * shapedSwapPaymentTimes_[^1] * swapPaymentDiscounts_[^1]
            * Math.Exp(-shapedSwapPaymentTimes_[^1] * x) * denOfRfunztion;
        derNumOfDerR += shapedSwapPaymentTimes_[^1] * swapPaymentDiscounts_[^1]
            * Math.Exp(-shapedSwapPaymentTimes_[^1] * x) * derDenOfRfunztion;

        derNumOfDerR -= shapedSwapPaymentTimes_[^1] * swapPaymentDiscounts_[^1]
            * Math.Exp(-shapedSwapPaymentTimes_[^1] * x) * derDenOfRfunztion;
        derNumOfDerR -= (discountAtStart_ - (swapPaymentDiscounts_[^1] * Math.Exp(-shapedSwapPaymentTimes_[^1] * x)))
            * der2DenOfRfunztion;

        double derDenOfDerR = 2 * denOfRfunztion * derDenOfRfunztion;

        double numerator = (derNumOfDerR * denOfDerR) - (numOfDerR * derDenOfDerR);
        if (!(denominator != 0))
        {
            throw new NanoQLException("GFunctionWithShifts::der2Rs_derX2: denominator == 0");
        }

        return numerator / denominator;
    }

    private double DerZ_derX(double x)
    {
        double sqrtDenominator = 1.0 - (discountRatio_ * Math.Exp(-shapedSwapPaymentTimes_[^1] * x));
        double denominator = sqrtDenominator * sqrtDenominator;
        if (!(denominator != 0))
        {
            throw new NanoQLException("GFunctionWithShifts::derZ_derX: denominator == 0");
        }

        double numerator = 0;
        numerator -= shapedPaymentTime_ * Math.Exp(-shapedPaymentTime_ * x) * sqrtDenominator;
        numerator -= shapedSwapPaymentTimes_[^1] * Math.Exp(-shapedPaymentTime_ * x) * (1.0 - sqrtDenominator);

        return numerator / denominator;
    }

    private double Der2Z_derX2(double x)
    {
        double denOfZfunction = 1.0 - (discountRatio_ * Math.Exp(-shapedSwapPaymentTimes_[^1] * x));
        double derDenOfZfunction = shapedSwapPaymentTimes_[^1] * discountRatio_ * Math.Exp(-shapedSwapPaymentTimes_[^1] * x);
        double denominator = Math.Pow(denOfZfunction, 4);
        if (!(denominator != 0))
        {
            throw new NanoQLException("GFunctionWithShifts::der2Z_derX2: denominator == 0");
        }

        double numOfDerZ = 0;
        numOfDerZ -= shapedPaymentTime_ * Math.Exp(-shapedPaymentTime_ * x) * denOfZfunction;
        numOfDerZ -= shapedSwapPaymentTimes_[^1] * Math.Exp(-shapedPaymentTime_ * x) * (1.0 - denOfZfunction);

        double denOfDerZ = Math.Pow(denOfZfunction, 2);
        double derNumOfDerZ = (-shapedPaymentTime_ * Math.Exp(-shapedPaymentTime_ * x)
                * (-shapedPaymentTime_ + (((shapedPaymentTime_ * discountRatio_) - (shapedSwapPaymentTimes_[^1] * discountRatio_))
                    * Math.Exp(-shapedSwapPaymentTimes_[^1] * x))))
            - (shapedSwapPaymentTimes_[^1] * Math.Exp(-shapedPaymentTime_ * x)
                * ((shapedPaymentTime_ * discountRatio_) - (shapedSwapPaymentTimes_[^1] * discountRatio_))
                * Math.Exp(-shapedSwapPaymentTimes_[^1] * x));

        double derDenOfDerZ = 2 * denOfZfunction * derDenOfZfunction;
        double numerator = (derNumOfDerZ * denOfDerZ) - (numOfDerZ * derDenOfDerZ);

        return numerator / denominator;
    }

    private double ShapeOfShift(double s)
    {
        double x = s - swapStartTime_;
        double meanReversion = meanReversion_;
        if (meanReversion >= 1.0e-4)
        {
            return (1.0 - Math.Exp(-meanReversion * x)) / meanReversion;
        }
        else if (meanReversion != 0.0)
        {
            // QuantLib's form cancels below a reversion of 1e-4, and it reads a negative one as 0
            return -Expm1Functions.StdExpm1(-meanReversion * x) / meanReversion;
        }
        else
        {
            return x;
        }
    }

    private double CalibrationOfShift(double Rs)
    {
        Memo last = memo;
        if (Rs == last.Rs)
        {
            return last.Shift;
        }

        double initialGuess, N = 0, D = 0;
        for (int i = 0; i < accruals_.Length; i++)
        {
            N += accruals_[i] * swapPaymentDiscounts_[i];
            D += accruals_[i] * swapPaymentDiscounts_[i] * shapedSwapPaymentTimes_[i];
        }

        N *= Rs;
        D *= Rs;
        N += (accruals_[^1] * swapPaymentDiscounts_[^1]) - discountAtStart_;
        D += accruals_[^1] * swapPaymentDiscounts_[^1] * shapedSwapPaymentTimes_[^1];
        initialGuess = N / D;

        Newton solver = new Newton().SetMaxEvaluations(1000);

        // these boundaries migth not be big enough if the volatility
        // of big swap rate values is too high . In this case the G function
        // is not even integrable, so better to fix the vol than increasing
        // these values
        const double lower = -20, upper = 20.0;

        double calibratedShift;
        try
        {
            calibratedShift = solver.Solve(
                new ObjectiveFunction(this, Rs),
                Accuracy,
                Math.Max(Math.Min(initialGuess, upper * 0.99), lower * 0.99),
                lower,
                upper);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            throw new NanoQLException(
                $"meanReversion: {DataFormattersFunctions.General(meanReversion_)}, swapRateValue: "
                + $"{DataFormattersFunctions.General(swapRateValue_)}, swapStartTime: "
                + $"{DataFormattersFunctions.General(swapStartTime_)}, shapedPaymentTime: "
                + $"{DataFormattersFunctions.General(shapedPaymentTime_)}\n error message: {e.Message}",
                e);
        }

        memo = new Memo(Rs, calibratedShift);
        return calibratedShift;
    }

    // The memo's pair, replaced as one reference.
    private sealed record Memo(double Rs, double Shift);

    // The shift's objective, Rs·Σ τᵢPᵢe^(−sᵢx) + Pₙe^(−sₙx) − P₀, and its derivative from the same terms.
    private readonly struct ObjectiveFunction(GFunctionWithShifts o, double rs) : IDerivativeSolver1DFunction
    {
        public double Evaluate(double x)
        {
            double result = 0;
            for (int i = 0; i < o.accruals_.Length; i++)
            {
                double temp = o.accruals_[i] * o.swapPaymentDiscounts_[i] * Math.Exp(-o.shapedSwapPaymentTimes_[i] * x);
                result += temp;
            }

            result *= rs;
            double temp2 = o.swapPaymentDiscounts_[^1] * Math.Exp(-o.shapedSwapPaymentTimes_[^1] * x);

            result += temp2 - o.discountAtStart_;
            return result;
        }

        public double Derivative(double x)
        {
            double derivative = 0;
            for (int i = 0; i < o.accruals_.Length; i++)
            {
                double temp = o.accruals_[i] * o.swapPaymentDiscounts_[i] * Math.Exp(-o.shapedSwapPaymentTimes_[i] * x);
                derivative -= o.shapedSwapPaymentTimes_[i] * temp;
            }

            derivative *= rs;
            double temp2 = o.swapPaymentDiscounts_[^1] * Math.Exp(-o.shapedSwapPaymentTimes_[^1] * x);

            derivative -= o.shapedSwapPaymentTimes_[^1] * temp2;
            return derivative;
        }
    }
}
