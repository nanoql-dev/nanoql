// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomdefaultlatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Experimental;

/// <summary>
/// Marks a loss model whose <see cref="IDefaultLossModel.LossDistribution"/> is the tranche's loss distribution, as
/// QuantLib's random models' histograms of tranched path losses are (<c>randomdefaultlatentmodel.hpp:401-439</c>),
/// where the recursive model's is the pool's: a basket with realized defaults shifts it by the realized tranche loss
/// (the S-xcr plan's XR8 (ii)).
/// </summary>
internal interface ITrancheLossDistribution
{
}
