// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/ratehelpers.hpp ql/termstructures/yield/ratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// The base of the helpers a yield curve is bootstrapped on: QuantLib's <c>RateHelper</c>, a typedef of
/// <c>BootstrapHelper&lt;YieldTermStructure&gt;</c>.
/// </summary>
/// <remarks>
/// A C# alias is no part of an assembly's API, so QuantLib's typedef is an abstract class. A yield helper may derive
/// from it or from <see cref="BootstrapHelper{TTermStructure}"/> of <see cref="YieldTermStructure"/> directly, which
/// is the same type in QuantLib; the curves take either. An extension point.
/// </remarks>
public abstract class RateHelper : BootstrapHelper<YieldTermStructure>
{
    /// <summary>Initializes a new instance of the <see cref="RateHelper"/> class.</summary>
    /// <param name="quote">The quote the helper reprices.</param>
    protected RateHelper(double quote)
        : base(quote)
    {
    }

    // For RelativeDateRateHelper, which QuantLib derives from RelativeDateBootstrapHelper instead.
    private protected RateHelper(double quote, EvaluationContext? context, bool updateDates)
        : base(quote, context, updateDates)
    {
    }
}
