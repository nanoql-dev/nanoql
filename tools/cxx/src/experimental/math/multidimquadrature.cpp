// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/multidimquadrature.hpp, .cpp and multidimintegrator.hpp, .cpp, which the wheel doesn't expose,
// on experimental/math/latentmodelspec.hpp's integrands:
//
//     GaussianQuadMultidimIntegrator::integrate (dimension, order, mu, integrand): the scalar integral; and its order
//     MultidimIntegral::integrate               (dimension, a, b, accuracy, maxIterations, integrand): the scalar
//                                                integral on TrapezoidIntegral<Default>(accuracy, maxIterations) per
//                                                dimension over [a, b]^dimension
//
// The vector integration isn't vectored: its first node's weight is lost.

#include "experimental/math/latentmodelspec.hpp"

#include <ql/experimental/math/multidimintegrator.hpp>
#include <ql/experimental/math/multidimquadrature.hpp>
#include <ql/math/integrals/trapezoidintegral.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"GaussianQuadMultidimIntegrator::integrate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 auto dim = static_cast<Size>(integer(a[0])), order = static_cast<Size>(integer(a[1]));
                 Real mu = real(a[2]);
                 std::string f = text(a[3]);
                 return [=] {
                     GaussianQuadMultidimIntegrator g(dim, order, mu);
                     json::array out;
                     out.emplace_back(g.integrate<Real>(latent::integrand(f)));
                     out.emplace_back(static_cast<std::int64_t>(g.order()));
                     return json::value(std::move(out));
                 };
             }},
            {"MultidimIntegral::integrate",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 auto dim = static_cast<Size>(integer(a[0]));
                 Real lo = real(a[1]), hi = real(a[2]), accuracy = real(a[3]);
                 auto maxIterations = static_cast<Size>(integer(a[4]));
                 std::string f = text(a[5]);
                 return [=] {
                     std::vector<ext::shared_ptr<Integrator>> is;
                     for (Size i = 0; i < dim; i++)
                         is.push_back(ext::make_shared<TrapezoidIntegral<Default>>(accuracy, maxIterations));
                     MultidimIntegral m(is);
                     return json::value(m(latent::integrand(f), std::vector<Real>(dim, lo), std::vector<Real>(dim, hi)));
                 };
             }},
        });

    }

}
