// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/integral.hpp, .cpp: the integrators' settings, absoluteAccuracy and maxEvaluations, as constructed
// and after setAbsoluteAccuracy and setMaxEvaluations, which the wheel doesn't expose, for each integrator nanoql
// ports; the Gaussian integrators pass Null<Real>() as their accuracy and their order as their maximum.

#include "convert.hpp"

#include <ql/math/integrals/gausslobattointegral.hpp>
#include <ql/math/integrals/gaussianquadratures.hpp>
#include <ql/math/integrals/simpsonintegral.hpp>
#include <ql/math/integrals/trapezoidintegral.hpp>

#include <memory>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        std::unique_ptr<Integrator> integrator(const std::string& kind, Real accuracy, Size maxEvaluations) {
            if (kind == "Simpson")
                return std::make_unique<SimpsonIntegral>(accuracy, maxEvaluations);
            if (kind == "TrapezoidDefault")
                return std::make_unique<TrapezoidIntegral<Default>>(accuracy, maxEvaluations);
            if (kind == "TrapezoidMidPoint")
                return std::make_unique<TrapezoidIntegral<MidPoint>>(accuracy, maxEvaluations);
            if (kind == "GaussLobatto")
                return std::make_unique<GaussLobattoIntegral>(maxEvaluations, accuracy);
            if (kind == "GaussLegendre")
                return std::make_unique<GaussLegendreIntegrator>(maxEvaluations);
            if (kind == "GaussChebyshev")
                return std::make_unique<GaussChebyshevIntegrator>(maxEvaluations);
            if (kind == "GaussChebyshev2nd")
                return std::make_unique<GaussChebyshev2ndIntegrator>(maxEvaluations);
            throw InvalidParams("unknown integrator " + kind);
        }

        const Register unit({
            {"Integrator::settings",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::string kind = text(a[0]);
                 Real accuracy = real(a[1]), newAccuracy = real(a[3]);
                 Integer maxEvaluations = integer(a[2]), newMaxEvaluations = integer(a[4]);
                 if (maxEvaluations < 0 || newMaxEvaluations < 0)
                     throw InvalidParams("a maximum must be non-negative");
                 return [=] {
                     std::unique_ptr<Integrator> I =
                         integrator(kind, accuracy, static_cast<Size>(maxEvaluations));
                     json::array out{I->absoluteAccuracy(), static_cast<std::int64_t>(I->maxEvaluations())};
                     I->setAbsoluteAccuracy(newAccuracy);
                     I->setMaxEvaluations(static_cast<Size>(newMaxEvaluations));
                     out.push_back(json::value(I->absoluteAccuracy()));
                     out.push_back(json::value(static_cast<std::int64_t>(I->maxEvaluations())));
                     return json::value(out);
                 };
             }},
        });

    }

}
