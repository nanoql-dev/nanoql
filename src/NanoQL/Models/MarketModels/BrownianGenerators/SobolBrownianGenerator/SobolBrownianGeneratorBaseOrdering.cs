// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>How a <see cref="SobolBrownianGeneratorBase"/> assigns the Sobol sequence's variates to the factors and
/// steps.</summary>
public enum SobolBrownianGeneratorBaseOrdering
{
    /// <summary>The variates with the best quality are used for the evolution of the first factor.</summary>
    Factors,

    /// <summary>The variates with the best quality are used for the largest steps of all factors.</summary>
    Steps,

    /// <summary>A diagonal schema assigns the variates with the best quality to the most important factors and the
    /// largest steps.</summary>
    Diagonal,
}
