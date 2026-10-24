// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The numeric Hagan pricer's integrand: a vanilla option's price times F″, F(x) = (x − K)(G(x)/G(R0) − 1), Hagan's
/// replication weight.
/// </summary>
internal sealed class ConundrumIntegrand
{
    private readonly VanillaOptionPricer vanillaOptionPricer_;
    private readonly double forwardValue_;
    private readonly double annuity_;
    private readonly double strike_;
    private readonly OptionType optionType_;
    private readonly GFunction gFunction_;

    public ConundrumIntegrand(
        VanillaOptionPricer o,
        GFunction gFunction,
        Date fixingDate,
        Date paymentDate,
        double annuity,
        double forwardValue,
        double strike,
        OptionType optionType)
    {
        vanillaOptionPricer_ = o;
        forwardValue_ = forwardValue;
        annuity_ = annuity;
        FixingDate = fixingDate;
        PaymentDate = paymentDate;
        strike_ = strike;
        optionType_ = optionType;
        gFunction_ = gFunction;
    }

    public Date FixingDate { get; }

    public Date PaymentDate { get; }

    public double Evaluate(double x)
    {
        double option = vanillaOptionPricer_.Evaluate(x, optionType_, annuity_);
        return option * SecondDerivativeOfF(x);
    }

    public double FunctionF(double x)
    {
        double Gx = gFunction_.Evaluate(x);
        double GR = gFunction_.Evaluate(forwardValue_);
        return (x - strike_) * ((Gx / GR) - 1.0);
    }

    public double FirstDerivativeOfF(double x)
    {
        double Gx = gFunction_.Evaluate(x);
        double GR = gFunction_.Evaluate(forwardValue_);
        double G1 = gFunction_.FirstDerivative(x);
        return ((Gx / GR) - 1.0) + (G1 / GR * (x - strike_));
    }

    public double SecondDerivativeOfF(double x)
    {
        double GR = gFunction_.Evaluate(forwardValue_);
        double G1 = gFunction_.FirstDerivative(x);
        double G2 = gFunction_.SecondDerivative(x);
        return (2.0 * G1 / GR) + ((x - strike_) * G2 / GR);
    }
}
