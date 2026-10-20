// SPDX-FileCopyrightText: Copyright (C) 2008, 2016 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/probabilitytraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

// QuantLib's detail constants of the default-probability traits, and the base's hazard rate the survival-probability
// and default-density kinds keep.
internal static class ProbabilityTraitsDetail
{
    public const double AvgHazardRate = 0.01;

    public const double MaxHazardRate = 1.0;

    // DefaultProbabilityTermStructure::hazardRateImpl: the density over the survival probability, both through the
    // curve's public members, 0 where the probability is.
    public static double BaseHazardRate(DefaultProbabilityTermStructure curve, double t)
    {
        double s = curve.SurvivalProbability(t, true);
        return s == 0.0 ? 0.0 : curve.DefaultDensity(t, true) / s;
    }
}
