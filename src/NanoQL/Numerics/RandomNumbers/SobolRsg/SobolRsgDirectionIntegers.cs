// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Richard Gould
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2013 Cheng Li
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND LicenseRef-JoeKuo
// Upstream: ql/math/randomnumbers/sobolrsg.hpp ql/math/randomnumbers/sobolrsg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// The Joe-Kuo numbers are available under a BSD-style license, at http://web.maths.unsw.edu.au/~fkuo/sobol/:
//
// Copyright (c) 2008, Frances Y. Kuo and Stephen Joe
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//
//     * Neither the names of the copyright holders nor the names of the
//       University of New South Wales and the University of Waikato
//       and its contributors may be used to endorse or promote products derived
//       from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ``AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

namespace NanoQL.Numerics;

/// <summary>
/// The initialization numbers (free direction integers) of a <see cref="SobolRsg"/> (QuantLib's
/// <c>SobolRsg::DirectionIntegers</c>). Their choice is crucial for the homogeneity properties of the sequence: Sobol
/// defines two, Property A and Property A'.
/// </summary>
public enum SobolRsgDirectionIntegers
{
    /// <summary>
    /// The unit initialization numbers suggested in "Numerical Recipes in C", 2nd edition, by Press, Teukolsky,
    /// Vetterling, and Flannery (section 7.7), which fail the test for Property A even for low dimensions.
    /// </summary>
    Unit,

    /// <summary>
    /// Jäckel's, in his book (section 8.3), up to dimension 32. Coefficients for d ≤ 8 are the same as in Bratley and
    /// Fox, so Property A' holds for d ≤ 6 but Property A holds for d ≤ 32.
    /// </summary>
    Jaeckel,

    /// <summary>
    /// Bratley and Fox's coefficients up to dimension 40, crediting unpublished work of Sobol' and Levitan (Bratley,
    /// P., Fox, B.L. (1988) "Algorithm 659: Implementing Sobol's quasirandom sequence generator," ACM Transactions on
    /// Mathematical Software 14:88-100). They satisfy Property A for d ≤ 20 and d = 23, 31, 33, 34, 37; Property A'
    /// holds for d ≤ 6.
    /// </summary>
    SobolLevitan,

    /// <summary>
    /// The coefficients of Lemieux, Cieslak, and Luttmer's implementation up to dimension 360: those for d ≤ 40 are
    /// the same as in Bratley and Fox, and for 40 &lt; d ≤ 360 they have been calculated as optimal values based on
    /// the "resolution" criterion ("RandQMC user's guide - A package for randomized quasi-Monte Carlo methods in C,"
    /// by C. Lemieux, M. Cieslak, and K. Luttmer, version January 13 2004).
    /// </summary>
    SobolLevitanLemieux,

    /// <summary>
    /// A set of the Joe–Kuo numbers, due to Stephen Joe and Frances Kuo (S. Joe and F. Y. Kuo, Constructing Sobol
    /// sequences with better two-dimensional projections, preprint Nov 22 2007; see
    /// http://web.maths.unsw.edu.au/~fkuo/sobol/), tabulated up to dimension 2,000.
    /// </summary>
    JoeKuoD5,

    /// <summary>A set of the Joe–Kuo numbers, tabulated up to dimension 21,200, every dimension the generator takes.</summary>
    JoeKuoD6,

    /// <summary>A set of the Joe–Kuo numbers, tabulated up to dimension 1,900.</summary>
    JoeKuoD7,

    /// <summary>
    /// The Kuo numbers, due to Stephen Joe and Frances Kuo, tabulated up to dimension 4,926. They were generated to work
    /// with a different ordering of primitive polynomials for the first 40 or so dimensions, which is why they use the
    /// alternative primitive polynomials.
    /// </summary>
    Kuo,

    /// <summary>A second set of the Kuo numbers, tabulated up to dimension 3,947, with the alternative primitive polynomials.</summary>
    Kuo2,

    /// <summary>A third set of the Kuo numbers, tabulated up to dimension 4,587, with the alternative primitive polynomials.</summary>
    Kuo3,
}
