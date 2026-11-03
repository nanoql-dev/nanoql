// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// The standard model's g-function: G(x) = x / (1 + x/q)^δ / (1 − (1 + x/q)^−n), n = q·L, for a swap of length L years
/// paying q times a year, the payment δ periods after the swap's start.
/// </summary>
/// <remarks>
/// QuantLib passes the swap's tenor as its length in the tenor's own units (<c>conundrumpricer.cpp:130</c>,
/// <c>swapTenor_.length()</c> into a <c>Size</c>), so an 18-month index reads as 18 years; here the length is in years,
/// QuantLib's bits on whole years. The model presumes n equal periods, so the Hagan pricers refuse a tenor that isn't a
/// whole number of fixed-leg periods.
/// </remarks>
internal sealed class GFunctionStandard : GFunction
{
    // number of period per year
    private readonly int q_;

    // fraction of a period between the swap start date and the pay date
    private readonly double delta_;

    // length of swap, in years
    private readonly double swapLength_;

    public GFunctionStandard(int q, double delta, double swapLength)
    {
        q_ = q;
        delta_ = delta;
        swapLength_ = swapLength;
    }

    public override double Evaluate(double x)
    {
        double n = swapLength_ * q_;
        return x / Math.Pow(1.0 + (x / q_), delta_) * 1.0 / (1.0 - (1.0 / Math.Pow(1.0 + (x / q_), n)));
    }

    public override double FirstDerivative(double x)
    {
        double n = swapLength_ * q_;
        double a = 1.0 + (x / q_);
        double AA = a - (delta_ / q_ * x);
        double B = Math.Pow(a, n - delta_ - 1.0) / (Math.Pow(a, n) - 1.0);

        double secNum = n * x * Math.Pow(a, n - 1.0);
        double secDen = q_ * Math.Pow(a, delta_) * (Math.Pow(a, n) - 1.0) * (Math.Pow(a, n) - 1.0);
        double sec = secNum / secDen;

        return (AA * B) - sec;
    }

    public override double SecondDerivative(double x)
    {
        double n = swapLength_ * q_;
        double a = 1.0 + (x / q_);
        double AA = a - (delta_ / q_ * x);
        double A1 = (1.0 - delta_) / q_;
        double B = Math.Pow(a, n - delta_ - 1.0) / (Math.Pow(a, n) - 1.0);
        double Num = ((1.0 + delta_ - n) * Math.Pow(a, n - delta_ - 2.0))
            - ((1.0 + delta_) * Math.Pow(a, (2.0 * n) - delta_ - 2.0));
        double Den = (Math.Pow(a, n) - 1.0) * (Math.Pow(a, n) - 1.0);
        double B1 = 1.0 / q_ * Num / Den;

        double C = x / Math.Pow(a, delta_);
        double C1 = (Math.Pow(a, delta_) - (delta_ / q_ * x * Math.Pow(a, delta_ - 1.0))) / Math.Pow(a, 2 * delta_);

        double D = Math.Pow(a, n - 1.0) / ((Math.Pow(a, n) - 1.0) * (Math.Pow(a, n) - 1.0));
        double D1 = (((n - 1.0) * Math.Pow(a, n - 2.0) * (Math.Pow(a, n) - 1.0)) - (2 * n * Math.Pow(a, 2 * (n - 1.0))))
            / (q_ * (Math.Pow(a, n) - 1.0) * (Math.Pow(a, n) - 1.0) * (Math.Pow(a, n) - 1.0));

        return (A1 * B) + (AA * B1) - (n / q_ * ((C1 * D) + (C * D1)));
    }
}
