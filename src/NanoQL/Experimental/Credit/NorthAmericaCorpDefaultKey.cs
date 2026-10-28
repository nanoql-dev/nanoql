// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultprobabilitykey.hpp ql/experimental/credit/defaultprobabilitykey.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// ISDA's standard default contractual key for US corporate debt: a failure to pay, a bankruptcy and, unless it is
/// <see cref="RestructuringType.NoRestructuring"/>, one restructuring type.
/// </summary>
[Experimental("NQL0001")]
public sealed class NorthAmericaCorpDefaultKey : DefaultProbKey
{
    /// <summary>Initializes a new instance of the <see cref="NorthAmericaCorpDefaultKey"/> class.</summary>
    /// <param name="currency">The currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="seniority">The seniority.</param>
    /// <param name="graceFailureToPay">The failure to pay's grace period; QuantLib's default is 30 days.</param>
    /// <param name="amountFailure">The failure to pay's required amount.</param>
    /// <param name="restructuringType">The restructuring type; full restructuring by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    public NorthAmericaCorpDefaultKey(
        Currency? currency,
        Seniority seniority,
        Period? graceFailureToPay = null,
        double amountFailure = 1.0e6,
        RestructuringType restructuringType = RestructuringType.CR)
        : base(Types(graceFailureToPay ?? new Period(30, TimeUnit.Days), amountFailure, restructuringType), currency, seniority)
    {
    }

    private static List<DefaultType> Types(Period grace, double amount, RestructuringType restructuringType)
    {
        EnumArguments.Check(restructuringType, nameof(restructuringType));
        var types = new List<DefaultType>
        {
            new FailureToPay(grace, amount),

            // no specifics for Bankruptcy
            new(AtomicDefaultType.Bankruptcy, RestructuringType.XR),
        };
        if (restructuringType != RestructuringType.NoRestructuring)
        {
            types.Add(new DefaultType(AtomicDefaultType.Restructuring, restructuringType));
        }

        return types;
    }
}
