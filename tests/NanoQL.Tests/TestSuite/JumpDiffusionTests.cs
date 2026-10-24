// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/jumpdiffusion.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class JumpDiffusionTests
{
    /* The data below are from
       "Option pricing formulas", E.G. Haug, McGraw-Hill 1998, pag 9

       Haug use the arbitrary truncation criterium of 11 terms in the sum,
       which doesn't guarantee convergence up to 1e-2.
       Using Haug's criterium Haug's values have been correctly reproduced.
       the following values have the right 1e-2 accuracy: any value different
       from Haug has been noted.
    */
    private static readonly HaugMertonData[] Values =
    [
        //        type, strike,   spot,    q,    r,    t,  vol, int, gamma, value, tol
        // gamma = 0.25, strike = 80
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.25, 20.67, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.25, 21.74, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.25, 23.63, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.25, 20.65, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.25, 21.70, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.25, 23.61, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.25, 20.64, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.25, 21.70, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.25, 23.61, 1e-2), // Haug 23.28
        // gamma = 0.25, strike = 90
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.25, 11.00, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.25, 12.74, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.25, 15.40, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.25, 10.98, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.25, 12.75, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.25, 15.42, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.25, 10.98, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.25, 12.75, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.25, 15.42, 1e-2), // Haug 15.20
        // gamma = 0.25, strike = 100
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.25, 3.42, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.25, 5.88, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.25, 8.95, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.25, 3.51, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.25, 5.96, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.25, 9.02, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.25, 3.53, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.25, 5.97, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.25, 9.03, 1e-2), // Haug 8.89
        // gamma = 0.25, strike = 110
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.25, 0.55, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.25, 2.11, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.25, 4.67, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.25, 0.56, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.25, 2.16, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.25, 4.73, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.25, 0.56, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.25, 2.17, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.25, 4.74, 1e-2), // Haug 4.66
        // gamma = 0.25, strike = 120
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.25, 0.10, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.25, 0.64, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.25, 2.23, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.25, 0.06, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.25, 0.63, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.25, 2.25, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.25, 0.05, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.25, 0.62, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.25, 2.25, 1e-2), // Haug 2.21

        // gamma = 0.50, strike = 80
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.50, 20.72, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.50, 21.83, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.50, 23.71, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.50, 20.66, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.50, 21.73, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.50, 23.63, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.50, 20.65, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.50, 21.71, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.50, 23.61, 1e-2), // Haug 23.28
        // gamma = 0.50, strike = 90
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.50, 11.04, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.50, 12.72, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.50, 15.34, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.50, 11.02, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.50, 12.76, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.50, 15.41, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.50, 11.00, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.50, 12.75, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.50, 15.41, 1e-2), // Haug 15.18
        // gamma = 0.50, strike = 100
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.50, 3.14, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.50, 5.58, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.50, 8.71, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.50, 3.39, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.50, 5.87, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.50, 8.96, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.50, 3.46, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.50, 5.93, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.50, 9.00, 1e-2), // Haug 8.85
        // gamma = 0.50, strike = 110
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.50, 0.53, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.50, 1.93, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.50, 4.42, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.50, 0.58, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.50, 2.11, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.50, 4.67, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.50, 0.57, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.50, 2.14, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.50, 4.71, 1e-2), // Haug 4.62
        // gamma = 0.50, strike = 120
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.50, 0.19, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.50, 0.71, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.50, 2.15, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.50, 0.10, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.50, 0.66, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.50, 2.23, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.50, 0.07, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.50, 0.64, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.50, 2.24, 1e-2), // Haug 2.19

        // gamma = 0.75, strike = 80
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.75, 20.79, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.75, 21.96, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.75, 23.86, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.75, 20.68, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.75, 21.78, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.75, 23.67, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.75, 20.66, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.75, 21.74, 1e-2),
        new(OptionType.Call, 80.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.75, 23.64, 1e-2), // Haug 23.30
        // gamma = 0.75, strike = 90
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.75, 11.11, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.75, 12.75, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.75, 15.30, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.75, 11.09, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.75, 12.78, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.75, 15.39, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.75, 11.04, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.75, 12.76, 1e-2),
        new(OptionType.Call, 90.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.75, 15.40, 1e-2), // Haug 15.17
        // gamma = 0.75, strike = 100
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.75, 2.70, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.75, 5.08, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.75, 8.24, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.75, 3.16, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.75, 5.71, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.75, 8.85, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.75, 3.33, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.75, 5.85, 1e-2),
        new(OptionType.Call, 100.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.75, 8.95, 1e-2), // Haug 8.79
        // gamma = 0.75, strike = 110
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.75, 0.54, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.75, 1.69, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.75, 3.99, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.75, 0.62, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.75, 2.05, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.75, 4.57, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.75, 0.60, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.75, 2.11, 1e-2),
        new(OptionType.Call, 110.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.75, 4.66, 1e-2), // Haug 4.56
        // gamma = 0.75, strike = 120
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 1.0, 0.75, 0.29, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 1.0, 0.75, 0.84, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 1.0, 0.75, 2.09, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 5.0, 0.75, 0.15, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 5.0, 0.75, 0.71, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 5.0, 0.75, 2.21, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.10, 0.25, 10.0, 0.75, 0.11, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.25, 0.25, 10.0, 0.75, 0.67, 1e-2),
        new(OptionType.Call, 120.00, 100.00, 0.00, 0.08, 0.50, 0.25, 10.0, 0.75, 2.23, 1e-2), // Haug 2.17
    ];

    // Adapted: the quotes changed in place become a process built for each row.
    [Fact]
    public void TestMerton76()
    {
        // Testing Merton 76 jump-diffusion model for European options...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (HaugMertonData value in Values)
        {
            StrikedTypePayoff payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            Date exDate = today + TimeToDays(value.T);
            Exercise exercise = new EuropeanExercise(exDate);

            // delta in Haug's notation
            Real jVol = value.V * Math.Sqrt(value.Gamma / value.JumpIntensity);

            // z in Haug's notation
            Real diffusionVol = value.V * Math.Sqrt(1.0 - value.Gamma);

            // Haug is assuming zero meanJump
            Real meanJump = 0.0;
            Real meanLogJump = Math.Log(1.0 + meanJump) - (0.5 * jVol * jVol);

            Volatility totalVol = Math.Sqrt((value.JumpIntensity * jVol * jVol) + (diffusionVol * diffusionVol));
            Volatility volError = Math.Abs(totalVol - value.V);
            Assert.True(volError < 1e-13, $"{volError} mismatch");

            var stochProcess = new Merton76Process(
                value.S,
                new FlatForward(today, value.Q, dc),
                new FlatForward(today, value.R, dc),
                new BlackConstantVol(today, new NullCalendar(), diffusionVol, dc),
                value.JumpIntensity,
                meanLogJump,
                jVol);

            var option = new EuropeanOption(payoff, exercise);
            Real calculated = JumpDiffusionEngine.Calculate(option, context, stochProcess).NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            if (error > value.Tol)
            {
                Assert.Fail(
                    $"European {payoff.OptionType} option: spot {value.S}, strike {payoff.Strike}, q {value.Q}, "
                    + $"r {value.R}, vol {value.V}, intensity {value.JumpIntensity}, gamma {value.Gamma}: expected "
                    + $"value {value.Result}, calculated {calculated}, error {error}, tolerance {value.Tol}");
            }
        }
    }

    // Adapted: the quotes changed in place, and the evaluation date moved, become a process built for each bump.
    [Fact]
    public void TestGreeks()
    {
        // Testing jump-diffusion option greeks...
        var tolerance = new Dictionary<string, Real>
        {
            ["delta"] = 1.0e-4,
            ["gamma"] = 1.0e-4,
            ["theta"] = 1.1e-4,
            ["rho"] = 1.0e-4,
            ["divRho"] = 1.0e-4,
            ["vega"] = 1.0e-4,
        };

        OptionType[] types = [OptionType.Put, OptionType.Call];
        Real[] strikes = [50.0, 100.0, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [-0.05, 0.0, 0.05];
        Rate[] rRates = [0.0, 0.01, 0.2];

        // The testsuite check fails if a too short maturity is chosen(i.e. 1 year).
        // The problem is in the theta calculation. With the finite difference(fd) method
        // we might get values too close to the jump steps, invalidating the fd methodology
        // for calculating greeks.
        double[] residualTimes = [5.0];
        Volatility[] vols = [0.11];
        Real[] jInt = [1.0, 5.0];
        Real[] mLJ = [-0.20, 0.0, 0.20];
        Volatility[] jV = [0.01, 0.25];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        // the process on curves and a volatility from an evaluation date, QuantLib's flatRate(quote, dc)
        Merton76Process Process(EvaluationContext context, Real u, Rate q, Rate r, Volatility v, Real jj1, Real jj2, Real jj3) =>
            new(
                u,
                FlatForward.Create(0, new NullCalendar(), q, dc, context),
                FlatForward.Create(0, new NullCalendar(), r, dc, context),
                BlackConstantVol.Create(0, new NullCalendar(), v, dc, context),
                jj1,
                jj2,
                jj3);

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (Real jj1 in jInt)
                {
                    foreach (Real jj2 in mLJ)
                    {
                        foreach (Real jj3 in jV)
                        {
                            foreach (double residualTime in residualTimes)
                            {
                                Date exDate = today + TimeToDays(residualTime);
                                Exercise exercise = new EuropeanExercise(exDate);
                                StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);
                                var option = new EuropeanOption(payoff, exercise);

                                // The jumpdiffusionengine greeks are very sensitive to the
                                // convergence level.  A tolerance of 1.0e-08 is usually
                                // sufficient to get reasonable results
                                OneAssetOptionResults Price(EvaluationContext context, Real u, Rate q, Rate r, Volatility v) =>
                                    JumpDiffusionEngine.Calculate(option, context, Process(context, u, q, r, v, jj1, jj2, jj3), 1e-08);

                                var context = new EvaluationContext { EvaluationDate = today };
                                foreach (Real u in underlyings)
                                {
                                    foreach (Real q in qRates)
                                    {
                                        foreach (Real r in rRates)
                                        {
                                            foreach (Volatility v in vols)
                                            {
                                                OneAssetOptionResults results = Price(context, u, q, r, v);
                                                Real value = results.NPV!.Value;
                                                var calculated = new Dictionary<string, Real>
                                                {
                                                    ["delta"] = results.Delta!.Value,
                                                    ["gamma"] = results.Gamma!.Value,
                                                    ["theta"] = results.Theta!.Value,
                                                    ["rho"] = results.Rho!.Value,
                                                    ["divRho"] = results.DividendRho!.Value,
                                                    ["vega"] = results.Vega!.Value,
                                                };

                                                if (value > u * 1.0e-5)
                                                {
                                                    var expected = new Dictionary<string, Real>();

                                                    // perturb spot and get delta and gamma
                                                    Real du = u * 1.0e-5;
                                                    OneAssetOptionResults up = Price(context, u + du, q, r, v);
                                                    OneAssetOptionResults down = Price(context, u - du, q, r, v);
                                                    expected["delta"] = (up.NPV!.Value - down.NPV!.Value) / (2 * du);
                                                    expected["gamma"] = (up.Delta!.Value - down.Delta!.Value) / (2 * du);

                                                    // perturb rates and get rho and dividend rho
                                                    Spread dr = 1.0e-5;
                                                    Real valueP = Price(context, u, q, r + dr, v).NPV!.Value;
                                                    Real valueM = Price(context, u, q, r - dr, v).NPV!.Value;
                                                    expected["rho"] = (valueP - valueM) / (2 * dr);

                                                    Spread dq = 1.0e-5;
                                                    valueP = Price(context, u, q + dq, r, v).NPV!.Value;
                                                    valueM = Price(context, u, q - dq, r, v).NPV!.Value;
                                                    expected["divRho"] = (valueP - valueM) / (2 * dq);

                                                    // perturb volatility and get vega
                                                    Volatility dv = v * 1.0e-4;
                                                    valueP = Price(context, u, q, r, v + dv).NPV!.Value;
                                                    valueM = Price(context, u, q, r, v - dv).NPV!.Value;
                                                    expected["vega"] = (valueP - valueM) / (2 * dv);

                                                    // get theta from time-shifted options
                                                    double dT = dc.YearFraction(today - 1, today + 1);
                                                    valueM = Price(new EvaluationContext { EvaluationDate = today - 1 }, u, q, r, v).NPV!.Value;
                                                    valueP = Price(new EvaluationContext { EvaluationDate = today + 1 }, u, q, r, v).NPV!.Value;
                                                    expected["theta"] = (valueP - valueM) / dT;

                                                    // compare
                                                    foreach (string greek in expected.Keys)
                                                    {
                                                        Real expct = expected[greek], calcl = calculated[greek], tol = tolerance[greek];
                                                        Real error = Math.Abs(expct - calcl);
                                                        Assert.False(
                                                            error > tol,
                                                            $"{greek} of the {type} at strike {strike}, intensity {jj1}, mean log-jump {jj2}, "
                                                            + $"jump vol {jj3}, q {q}, r {r}: expected {expct}, calculated {calcl}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private readonly record struct HaugMertonData(
        OptionType Type, Real Strike, Real S, Rate Q, Rate R, double T, Volatility V, Real JumpIntensity, Real Gamma,
        Real Result, Real Tol);
}
