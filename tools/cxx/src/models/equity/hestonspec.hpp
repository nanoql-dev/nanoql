// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The Heston and Bates process specs the equity harness units share (ql/processes/hestonprocess.hpp,
// batesprocess.hpp), with tests/NanoQL.Tests/Vectors/Cxx/Models/Equity/HestonCxxInputs.cs's names.
//
// A process is {"referenceDate", "dayCounter", "riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho"}:
// QuantLib's HestonProcess, under its default discretization, on flat forwards at the two rates (continuous, annual)
// from the reference date on the day counter. A Bates process adds "lambda", "nu" and "delta": QuantLib's BatesProcess
// on the same curves, under its default discretization. A quanto helper is {"foreign", "fxVolatility", "correlation",
// "atmLevel"}: QuantLib's FdmQuantoHelper on the process's risk-free curve, a flat foreign curve and a constant FX
// volatility on the process's reference date and day counter.

#ifndef NANOQL_CXX_MODELS_EQUITY_HESTONSPEC_HPP
#define NANOQL_CXX_MODELS_EQUITY_HESTONSPEC_HPP

#include "convert.hpp"

#include <ql/methods/finitedifferences/utilities/fdmquantohelper.hpp>
#include <ql/processes/batesprocess.hpp>
#include <ql/processes/hestonprocess.hpp>
#include <ql/shared_ptr.hpp>

#include <optional>

namespace harness::equity {

    struct ProcessSpec {
        DateSpec referenceDate{true, 0};
        DayCounterSpec dayCounter;
        QuantLib::Real riskFree = 0.0, dividend = 0.0, s0 = 0.0, v0 = 0.0, kappa = 0.0, theta = 0.0, sigma = 0.0,
                       rho = 0.0;
        bool jumps = false;
        QuantLib::Real lambda = 0.0, nu = 0.0, delta = 0.0;
    };
    ProcessSpec process(const json::value& v);
    // A BatesProcess for a spec with jumps, a HestonProcess otherwise.
    QuantLib::ext::shared_ptr<QuantLib::HestonProcess> makeProcess(const ProcessSpec& s);
    // Refuses a spec without jumps.
    QuantLib::ext::shared_ptr<QuantLib::BatesProcess> makeBatesProcess(const ProcessSpec& s);

    struct QuantoSpec {
        QuantLib::Real foreign = 0.0, fxVolatility = 0.0, correlation = 0.0, atmLevel = 0.0;
    };
    // null for none
    std::optional<QuantoSpec> quanto(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::FdmQuantoHelper> makeQuanto(const std::optional<QuantoSpec>& q,
                                                                   const ProcessSpec& s);

    // An option's results as the vectors write them: {"NPV", "delta", "gamma", "theta"}.
    json::value vanillaResults(QuantLib::Real npv, QuantLib::Real delta, QuantLib::Real gamma, QuantLib::Real theta);

}

#endif
