// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/markovfunctional.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>The factory of the custom smile sections the Markov-functional model calibrates to.</summary>
/// <remarks>An extension point, as in QuantLib.</remarks>
public abstract class MarkovFunctionalCustomSmileFactory
{
    /// <summary>The custom smile section for a calibration point.</summary>
    /// <param name="source">The point's market smile, at the model's at-the-money level.</param>
    /// <param name="atm">The point's at-the-money level, on the model's curve.</param>
    /// <returns>The section.</returns>
    public abstract MarkovFunctionalCustomSmileSection SmileSection(SmileSection source, double atm);
}
