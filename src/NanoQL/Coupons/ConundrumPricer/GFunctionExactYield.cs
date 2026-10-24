// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The exact-yield model's g-function: the standard model on the fixed leg's own accrual periods,
/// G(x) = x (1 + τ₀x)^−δ / (1 − ∏ 1/(1 + τᵢx)).
/// </summary>
internal sealed class GFunctionExactYield : GFunction
{
    // fraction of a period between the swap start date and the pay date
    private readonly double delta_;

    // accruals fraction
    private readonly double[] accruals_;

    public GFunctionExactYield(CmsCoupon coupon, EvaluationContext context)
    {
        SwapIndex swapIndex = coupon.SwapIndex;
        VanillaSwap swap = swapIndex.UnderlyingSwap(coupon.FixingDate(), context);

        Schedule schedule = swap.FixedSchedule;
        YieldTermStructure rateCurve = swapIndex.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");

        DayCounter dc = swapIndex.DayCounter;

        double swapStartTime = dc.YearFraction(rateCurve.ReferenceDate, schedule.StartDate());
        double swapFirstPaymentTime = dc.YearFraction(rateCurve.ReferenceDate, schedule.Date(1));

        double paymentTime = dc.YearFraction(rateCurve.ReferenceDate, coupon.Date);

        delta_ = (paymentTime - swapStartTime) / (swapFirstPaymentTime - swapStartTime);

        var fixedLeg = swap.FixedLeg;
        int n = fixedLeg.Length;
        accruals_ = new double[n];
        for (int i = 0; i < n; ++i)
        {
            accruals_[i] = ((Coupon)fixedLeg[i]).AccrualPeriod();
        }
    }

    public override double Evaluate(double x)
    {
        double product = 1.0;
        foreach (double accrual in accruals_)
        {
            product *= 1.0 / (1.0 + (accrual * x));
        }

        return x * Math.Pow(1.0 + (accruals_[0] * x), -delta_) * (1.0 / (1.0 - product));
    }

    public override double FirstDerivative(double x)
    {
        double c = -1.0;
        double derC = 0.0;
        var b = new double[accruals_.Length];
        for (int i = 0; i < accruals_.Length; i++)
        {
            double accrual = accruals_[i];
            double temp = 1.0 / (1.0 + (accrual * x));
            b[i] = temp;
            c *= temp;
            derC += accrual * temp;
        }

        c += 1.0;
        c = 1.0 / c;
        derC *= c - (c * c);

        return (-delta_ * accruals_[0] * Math.Pow(b[0], delta_ + 1.0) * x * c)
            + (Math.Pow(b[0], delta_) * c) + (Math.Pow(b[0], delta_) * x * derC);
    }

    public override double SecondDerivative(double x)
    {
        double c = -1.0;
        double sum = 0.0;
        double sumOfSquare = 0.0;
        var b = new double[accruals_.Length];
        for (int i = 0; i < accruals_.Length; i++)
        {
            double accrual = accruals_[i];
            double temp = 1.0 / (1.0 + (accrual * x));
            b[i] = temp;
            c *= temp;
            sum += accrual * temp;
            sumOfSquare += Math.Pow(accrual * temp, 2.0);
        }

        c += 1.0;
        c = 1.0 / c;
        double derC = sum * (c - (c * c));

        return (((-delta_ * accruals_[0] * Math.Pow(b[0], delta_ + 1.0) * c) + (Math.Pow(b[0], delta_) * derC))
                * ((-delta_ * accruals_[0] * b[0] * x) + 1.0 + (x * (1.0 - c) * sum)))
            + (Math.Pow(b[0], delta_) * c * ((delta_ * Math.Pow(accruals_[0] * b[0], 2.0) * x) - (delta_ * accruals_[0] * b[0])
                - (x * derC * sum) + ((1.0 - c) * sum) - (x * (1.0 - c) * sumOfSquare)));
    }
}
