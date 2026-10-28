// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/latentmodel.hpp, which the wheel doesn't expose, on experimental/math/latentmodelspec.hpp's
// model specs and integrands:
//
//     LatentModel::inspect          (model): [size, numFactors, numTotalFactors, factorWeights, idiosyncFctrs]
//     LatentModel::members          (model, values, probabilities, samples, uniform samples): [cumulativeY(value, i)
//                                    for each value and variable, cumulativeZ(value), inverseCumulativeY(p, i),
//                                    inverseCumulativeZ(p), inverseCumulativeDensity(p, k), density(systemic part of
//                                    each sample), allFactorCumulInverter(each uniform sample), latentVarValue(each
//                                    sample, i), latentVariableCorrel(i, j)]
//     LatentModel::integratedExpectedValue (model, integration type, integrand)
//     FactorSampler::draws          (model, generator, seed, count): the default sampler's draws on
//                                    RandomSequenceGenerator<MersenneTwisterUniformRng> ("MT") or SobolRsg ("Sobol"),
//                                    and the Box-Muller specialization ("BoxMuller", Gaussian only)
//     FactorSampler::polarFirst     (model, seeds): the polar Student t specialization's first draw on a fresh sampler
//                                    at each seed (Student t only)
//
// Each draw is [values..., weight].

#include "experimental/math/latentmodelspec.hpp"

#include <ql/math/randomnumbers/boxmullergaussianrng.hpp>
#include <ql/math/randomnumbers/mt19937uniformrng.hpp>
#include <ql/math/randomnumbers/randomsequencegenerator.hpp>
#include <ql/math/randomnumbers/sobolrsg.hpp>

using namespace QuantLib;

namespace harness {

    namespace latent {

        std::vector<std::vector<Real>> matrix(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a matrix as an array of rows");
            std::vector<std::vector<Real>> out;
            for (const json::value& r : v.get_array())
                out.push_back(reals(r));
            return out;
        }

        std::function<Real(const std::vector<Real>&)> integrand(const std::string& name) {
            if (name == "one")
                return [](const std::vector<Real>&) { return 1.0; };
            if (name == "m0")
                return [](const std::vector<Real>& m) { return m[0]; };
            if (name == "m0sq")
                return [](const std::vector<Real>& m) { return m[0] * m[0]; };
            if (name == "m0m1")
                return [](const std::vector<Real>& m) { return m[0] * m[1]; };
            if (name == "exp")
                return [](const std::vector<Real>& m) { return std::exp(0.1 * m[0]); };
            if (name == "indicator")
                return [](const std::vector<Real>& m) { return m[0] < 0.3 ? 1.0 : 0.0; };
            if (name == "cos")
                return [](const std::vector<Real>& m) {
                    Real s = 0.0;
                    for (Real x : m)
                        s += x;
                    return std::cos(s);
                };
            throw InvalidParams("unknown integrand " + name);
        }

        TCopulaPolicy::initTraits tTraits(const json::value& orders) {
            TCopulaPolicy::initTraits ini;
            ini.tOrders = integers(orders);
            return ini;
        }

    }

    namespace {

        using latent::onModel;

        json::value arr(const std::vector<Real>& x) {
            json::array out;
            for (Real r : x)
                out.emplace_back(r);
            return json::value(std::move(out));
        }

        template <class S>
        json::value draws(S& sampler, Size count) {
            json::array out;
            for (Size i = 0; i < count; i++) {
                const auto& s = sampler.nextSequence();
                json::array d;
                for (Real x : s.value)
                    d.emplace_back(x);
                d.emplace_back(s.weight);
                out.emplace_back(std::move(d));
            }
            return json::value(std::move(out));
        }

        const auto quadrature = LatentModelIntegrationType::GaussianQuadrature;

        const Register unit({
            {"LatentModel::inspect",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 json::value m = a[0];
                 return [=] {
                     return onModel(m, quadrature, [](const auto& lm) {
                         json::array w;
                         for (const auto& row : lm.factorWeights())
                             w.push_back(arr(row));
                         json::array out;
                         out.emplace_back(static_cast<std::int64_t>(lm.size()));
                         out.emplace_back(static_cast<std::int64_t>(lm.numFactors()));
                         out.emplace_back(static_cast<std::int64_t>(lm.numTotalFactors()));
                         out.emplace_back(std::move(w));
                         out.push_back(arr(lm.idiosyncFctrs()));
                         return json::value(std::move(out));
                     });
                 };
             }},
            {"LatentModel::members",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 json::value m = a[0];
                 std::vector<Real> values = reals(a[1]), probs = reals(a[2]);
                 std::vector<std::vector<Real>> samples = latent::matrix(a[3]), uniforms = latent::matrix(a[4]);
                 return [=] {
                     return onModel(m, quadrature, [&](const auto& lm) {
                         Size n = lm.size(), k = lm.numFactors();
                         json::array cy, cz, icy, icz, icd, dens, inv, lv, corr;
                         for (Real v : values) {
                             for (Size i = 0; i < n; i++)
                                 cy.emplace_back(lm.cumulativeY(v, i));
                             cz.emplace_back(lm.cumulativeZ(v));
                         }
                         for (Real p : probs) {
                             for (Size i = 0; i < n; i++)
                                 icy.emplace_back(lm.inverseCumulativeY(p, i));
                             icz.emplace_back(lm.inverseCumulativeZ(p));
                             for (Size j = 0; j < k; j++)
                                 icd.emplace_back(lm.inverseCumulativeDensity(p, j));
                         }
                         for (const std::vector<Real>& s : samples) {
                             dens.emplace_back(lm.density(std::vector<Real>(s.begin(), s.begin() + k)));
                             for (Size i = 0; i < n; i++)
                                 lv.emplace_back(lm.latentVarValue(s, i));
                         }
                         for (const std::vector<Real>& u : uniforms)
                             inv.push_back(arr(lm.allFactorCumulInverter(u)));
                         for (Size i = 0; i < n; i++)
                             for (Size j = 0; j < n; j++)
                                 corr.emplace_back(lm.latentVariableCorrel(i, j));
                         json::array out;
                         for (auto* x : {&cy, &cz, &icy, &icz, &icd, &dens, &inv, &lv, &corr})
                             out.emplace_back(std::move(*x));
                         return json::value(std::move(out));
                     });
                 };
             }},
            {"LatentModel::integratedExpectedValue",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value m = a[0];
                 auto type = enumeration<LatentModelIntegrationType::LatentModelIntegrationType>(a[1]);
                 std::string f = text(a[2]);
                 return [=] {
                     return onModel(m, type, [&](const auto& lm) {
                         return json::value(lm.integratedExpectedValue(latent::integrand(f)));
                     });
                 };
             }},
            {"FactorSampler::polarFirst",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value m = a[0];
                 if (!a[1].is_array())
                     throw InvalidParams("expected an array of seeds");
                 std::vector<BigNatural> seeds;
                 for (const json::value& x : a[1].get_array())
                     seeds.push_back(static_cast<BigNatural>(int64(x)));
                 return [=] {
                     return onModel(m, quadrature, [&](const auto& lm) -> json::value {
                         using LM = std::decay_t<decltype(lm)>;
                         using CP = typename LM::copulaType;
                         if constexpr (std::is_same_v<CP, TCopulaPolicy>) {
                             json::array out;
                             for (BigNatural seed : seeds) {
                                 typename LM::template FactorSampler<
                                     RandomSequenceGenerator<PolarStudentTRng<MersenneTwisterUniformRng>>>
                                     s(lm.copula(), seed);
                                 out.push_back(draws(s, 1).as_array().front());
                             }
                             return json::value(std::move(out));
                         } else {
                             throw InvalidParams("the polar sampler takes a Student t model");
                         }
                     });
                 };
             }},
            {"FactorSampler::draws",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 json::value m = a[0];
                 std::string g = text(a[1]);
                 auto seed = static_cast<BigNatural>(int64(a[2]));
                 auto count = static_cast<Size>(integer(a[3]));
                 return [=] {
                     return onModel(m, quadrature, [&](const auto& lm) -> json::value {
                         using LM = std::decay_t<decltype(lm)>;
                         using CP = typename LM::copulaType;
                         if (g == "MT") {
                             typename LM::template FactorSampler<RandomSequenceGenerator<MersenneTwisterUniformRng>> s(
                                 lm.copula(), seed);
                             return draws(s, count);
                         }
                         if (g == "Sobol") {
                             typename LM::template FactorSampler<SobolRsg> s(lm.copula(), seed);
                             return draws(s, count);
                         }
                         if constexpr (std::is_same_v<CP, GaussianCopulaPolicy>) {
                             if (g == "BoxMuller") {
                                 typename LM::template FactorSampler<
                                     RandomSequenceGenerator<BoxMullerGaussianRng<MersenneTwisterUniformRng>>>
                                     s(lm.copula(), seed);
                                 return draws(s, count);
                             }
                         }
                         throw InvalidParams("unknown factor sampler " + g);
                     });
                 };
             }},
        });

    }

}
