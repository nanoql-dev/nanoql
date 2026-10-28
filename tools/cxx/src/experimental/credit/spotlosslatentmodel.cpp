// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/spotlosslatentmodel.hpp, which the wheel doesn't expose, on experimental/credit/portfoliospec's
// basket spec; the model is {"copula": "Gaussian" or "T", "tOrders" (T), "integration", "factorWeights" (the defaults',
// then the recoveries'), "recoveries", "modelA"}, reset on the basket:
//
//     SpotRecoveryLatentModel::members (model, basket, date, factors, probabilities, latents, samples): for each name,
//         [conditionalDefaultProbability(date, i, m) for each factor sample m,
//          conditionalDefaultProbability(p, i, m) for each probability p and factor sample m,
//          expCondRecovery(date, i, m) for each factor sample m,
//          conditionalRecovery(x, i, date) for each latent value x,
//          latentRRVarValue(s, i) for each sample s of every factor]
//     SpotRecoveryLatentModel::refused (model, basket): resetBasket's refusal on a basket of another size

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/portfoliospec.hpp"

#include <ql/experimental/credit/spotlosslatentmodel.hpp>
#include <ql/experimental/math/gaussiancopulapolicy.hpp>
#include <ql/experimental/math/tcopulapolicy.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::key;
        using credit::portfolio::makeBasket;
        using credit::portfolio::object;

        std::vector<std::vector<Real>> rows(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of rows");
            std::vector<std::vector<Real>> out;
            for (const json::value& r : v.get_array())
                out.push_back(reals(r));
            return out;
        }

        template <class CP>
        json::value members(const json::object& o, const typename CP::initTraits& ini, const json::value& b,
                            const Date& d, const std::vector<std::vector<Real>>& factors, const std::vector<Real>& probs,
                            const std::vector<Real>& latents, const std::vector<std::vector<Real>>& samples) {
            const std::string what = "a spot-recovery model";
            auto type = enumeration<LatentModelIntegrationType::LatentModelIntegrationType>(key(o, "integration", what));
            SpotRecoveryLatentModel<CP> m(rows(key(o, "factorWeights", what)), reals(key(o, "recoveries", what)),
                                          real(key(o, "modelA", what)), type, ini);
            ext::shared_ptr<Basket> basket = makeBasket(b);
            m.resetBasket(basket);
            json::array out;
            for (Size i = 0; i < basket->size(); i++) {
                json::array byDate, byProb, recovery, conditional, rr;
                for (const std::vector<Real>& f : factors) {
                    byDate.emplace_back(m.conditionalDefaultProbability(d, i, f));
                    recovery.emplace_back(m.expCondRecovery(d, i, f));
                    for (Real p : probs)
                        byProb.emplace_back(m.conditionalDefaultProbability(p, i, f));
                }
                for (Real x : latents)
                    conditional.emplace_back(m.conditionalRecovery(x, i, d));
                for (const std::vector<Real>& s : samples)
                    rr.emplace_back(m.latentRRVarValue(s, i));
                json::array name;
                for (auto* a : {&byDate, &byProb, &recovery, &conditional, &rr})
                    name.emplace_back(std::move(*a));
                out.emplace_back(std::move(name));
            }
            return json::value(std::move(out));
        }

        template <class F>
        json::value onCopula(const json::value& model, F f) {
            const json::object& o = object(model, "a spot-recovery model");
            std::string copula = text(key(o, "copula", "a spot-recovery model"));
            if (copula == "Gaussian")
                return f(o, GaussianCopulaPolicy::initTraits(), static_cast<GaussianCopulaPolicy*>(nullptr));
            TCopulaPolicy::initTraits ini;
            ini.tOrders = integers(key(o, "tOrders", "a spot-recovery model"));
            return f(o, ini, static_cast<TCopulaPolicy*>(nullptr));
        }

        const Register unit({
            {"SpotRecoveryLatentModel::members",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 json::value model = a[0], b = a[1];
                 DateSpec ds = date(a[2]);
                 std::vector<std::vector<Real>> factors = rows(a[3]), samples = rows(a[6]);
                 std::vector<Real> probs = reals(a[4]), latents = reals(a[5]);
                 return [=] {
                     return onCopula(model, [&](const json::object& o, const auto& ini, auto* tag) {
                         using CP = std::remove_pointer_t<decltype(tag)>;
                         return members<CP>(o, ini, b, makeDate(ds), factors, probs, latents, samples);
                     });
                 };
             }},
            {"SpotRecoveryLatentModel::refused",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value model = a[0], b = a[1];
                 return [=] {
                     return onCopula(model, [&](const json::object& o, const auto& ini, auto* tag) {
                         using CP = std::remove_pointer_t<decltype(tag)>;
                         const std::string what = "a spot-recovery model";
                         auto type = enumeration<LatentModelIntegrationType::LatentModelIntegrationType>(
                             key(o, "integration", what));
                         SpotRecoveryLatentModel<CP> m(rows(key(o, "factorWeights", what)),
                                                       reals(key(o, "recoveries", what)), real(key(o, "modelA", what)),
                                                       type, ini);
                         m.resetBasket(makeBasket(b));
                         return json::value(true);
                     });
                 };
             }},
        });

    }

}
