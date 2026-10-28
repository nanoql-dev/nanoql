// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/gaussiancopulapolicy.hpp and tcopulapolicy.hpp, .cpp, which the wheel doesn't expose, built
// from a weight matrix and, for the T policy, its orders:
//
//     GaussianCopulaPolicy::members  (factorWeights, values, probabilities, samples, uniform samples)
//     TCopulaPolicy::members         (factorWeights, tOrders, values, probabilities, samples, uniform samples)
//
// each [numFactors, cumulativeY(value, i) for each value and variable, cumulativeZ(value), inverseCumulativeY(p, i),
// inverseCumulativeZ(p), inverseCumulativeDensity(p, k), density(each sample's systemic part),
// allFactorCumulInverter(each uniform sample), getInitTraits()'s orders (T: [], Gaussian), varianceFactors() (T: [],
// Gaussian)].

#include "experimental/math/latentmodelspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        json::value arr(const std::vector<Real>& x) {
            json::array out;
            for (Real r : x)
                out.emplace_back(r);
            return json::value(std::move(out));
        }

        template <class CP>
        json::value members(const CP& c, Size n, Size k, const std::vector<Real>& values, const std::vector<Real>& probs,
                            const std::vector<std::vector<Real>>& samples,
                            const std::vector<std::vector<Real>>& uniforms, json::value orders, json::value vf) {
            json::array cy, cz, icy, icz, icd, dens, inv;
            for (Real v : values) {
                for (Size i = 0; i < n; i++)
                    cy.emplace_back(c.cumulativeY(v, i));
                cz.emplace_back(c.cumulativeZ(v));
            }
            for (Real p : probs) {
                for (Size i = 0; i < n; i++)
                    icy.emplace_back(c.inverseCumulativeY(p, i));
                icz.emplace_back(c.inverseCumulativeZ(p));
                for (Size j = 0; j < k; j++)
                    icd.emplace_back(c.inverseCumulativeDensity(p, j));
            }
            for (const std::vector<Real>& s : samples)
                dens.emplace_back(c.density(std::vector<Real>(s.begin(), s.begin() + k)));
            for (const std::vector<Real>& u : uniforms)
                inv.push_back(arr(c.allFactorCumulInverter(u)));
            json::array out;
            out.emplace_back(static_cast<std::int64_t>(c.numFactors()));
            for (auto* x : {&cy, &cz, &icy, &icz, &icd, &dens, &inv})
                out.emplace_back(std::move(*x));
            out.push_back(orders);
            out.push_back(vf);
            return json::value(std::move(out));
        }

        const Register unit({
            {"GaussianCopulaPolicy::members",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::vector<std::vector<Real>> w = latent::matrix(a[0]), s = latent::matrix(a[3]),
                                                u = latent::matrix(a[4]);
                 std::vector<Real> values = reals(a[1]), probs = reals(a[2]);
                 return [=] {
                     GaussianCopulaPolicy c(w);
                     return members(c, w.size(), w.empty() ? 0 : w[0].size(), values, probs, s, u,
                                    json::value(json::array()), json::value(json::array()));
                 };
             }},
            {"TCopulaPolicy::members",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::vector<std::vector<Real>> w = latent::matrix(a[0]), s = latent::matrix(a[4]),
                                                u = latent::matrix(a[5]);
                 json::value orders = a[1];
                 std::vector<Real> values = reals(a[2]), probs = reals(a[3]);
                 return [=] {
                     TCopulaPolicy c(w, latent::tTraits(orders));
                     json::array o;
                     for (Integer x : c.getInitTraits().tOrders)
                         o.emplace_back(static_cast<std::int64_t>(x));
                     return members(c, w.size(), w.empty() ? 0 : w[0].size(), values, probs, s, u,
                                    json::value(std::move(o)), arr(c.varianceFactors()));
                 };
             }},
        });

    }

}
