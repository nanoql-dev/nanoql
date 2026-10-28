// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/zerocondition.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The zero exercise condition, used in CEV models: each value floored at 0.</summary>
/// <remarks>
/// <c>std::max(a, 0.0)</c>, which keeps a value unless it is less than 0: −0 and NaN stay as they are.
/// </remarks>
public sealed class ZeroCondition : StepCondition
{
    /// <inheritdoc/>
    public override void ApplyTo(Span<double> a, double t)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] < 0.0)
            {
                a[i] = 0.0;
            }
        }
    }
}
