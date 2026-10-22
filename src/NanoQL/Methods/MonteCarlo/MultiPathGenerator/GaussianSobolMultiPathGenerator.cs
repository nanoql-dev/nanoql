// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/multipathgenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// QuantLib-Python's <c>GaussianSobolMultiPathGenerator</c>: a <see cref="MultiPathGenerator{TGsg}"/> over Sobol Gaussian sequences, <c>MultiPathGenerator&lt;GaussianLowDiscrepancySequenceGenerator&gt;</c>.
/// </summary>
/// <remarks>A named class, a convenience; nanoql's own code uses the generic type.</remarks>
public sealed class GaussianSobolMultiPathGenerator : MultiPathGenerator<InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>>
{
    /// <summary>Initializes a new instance of the <see cref="GaussianSobolMultiPathGenerator"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="times">The time grid, of at least two times.</param>
    /// <param name="generator">The sequence generator, which the path generator copies.</param>
    /// <param name="brownianBridge">Whether a Brownian bridge orders the draws: not supported.</param>
    public GaussianSobolMultiPathGenerator(StochasticProcess process, TimeGrid times, InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal> generator, bool brownianBridge = false)
        : base(process, times, generator, brownianBridge)
    {
    }

    private GaussianSobolMultiPathGenerator(GaussianSobolMultiPathGenerator source)
        : base(source)
    {
    }

    /// <inheritdoc/>
    public override GaussianSobolMultiPathGenerator Clone() => new(this);
}
