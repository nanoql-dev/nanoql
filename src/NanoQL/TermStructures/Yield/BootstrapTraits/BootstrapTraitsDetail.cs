// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

// QuantLib's detail constants, and std::min_element and std::max_element as the traits use them.
internal static class BootstrapTraitsDetail
{
    public const double AvgRate = 0.05;

    public const double MaxRate = 1.0;

    // *std::min_element: the first element no later element compares smaller than, so a NaN first is kept and a NaN
    // later is passed over.
    public static double MinElement(ReadOnlySpan<double> v)
    {
        double m = v[0];
        foreach (double x in v[1..])
        {
            if (x < m)
            {
                m = x;
            }
        }

        return m;
    }

    // *std::max_element: the first element no later element compares larger than.
    public static double MaxElement(ReadOnlySpan<double> v)
    {
        double m = v[0];
        foreach (double x in v[1..])
        {
            if (m < x)
            {
                m = x;
            }
        }

        return m;
    }
}
