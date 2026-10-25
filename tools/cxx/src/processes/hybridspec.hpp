// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The Heston-Hull-White hybrid process spec the harness units share (ql/processes/hybridhestonhullwhiteprocess.hpp),
// with tests/NanoQL.Tests/Vectors/Cxx/Processes/HybridHestonHullWhiteCxxInputs.cs's names.
//
// A hybrid process is {"heston", "a", "sigma", "forwardMeasureTime", "correlation", "discretization"}: QuantLib's
// HybridHestonHullWhiteProcess over models/equity/hestonspec.hpp's Heston process and a HullWhiteForwardProcess on that
// process's risk-free curve with the reversion a, the volatility sigma and the forward-measure time, at the
// equity-rate correlation, under the discretization (QuantLib's integer: Euler 0, BSMHullWhite 1).

#ifndef NANOQL_CXX_PROCESSES_HYBRIDSPEC_HPP
#define NANOQL_CXX_PROCESSES_HYBRIDSPEC_HPP

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/processes/hybridhestonhullwhiteprocess.hpp>
#include <ql/shared_ptr.hpp>

namespace harness::hybrid {

    struct ProcessSpec {
        equity::ProcessSpec heston;
        QuantLib::Real a = 0.0, sigma = 0.0, forwardMeasureTime = 0.0, correlation = 0.0;
        QuantLib::HybridHestonHullWhiteProcess::Discretization discretization =
            QuantLib::HybridHestonHullWhiteProcess::BSMHullWhite;
    };
    ProcessSpec process(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::HybridHestonHullWhiteProcess> makeProcess(const ProcessSpec& s);

}

#endif
