// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/defaultprobabilitylatentmodel.hpp and constantlosslatentmodel.hpp, which the wheel doesn't
// expose: the default latent model and the constant-loss latent model, each reset on a basket of
// experimental/credit/portfoliospec.hpp's spec (resetBasket), at the evaluation date the settings set. A model is
// {"kind": "DefaultLatentModel" or "ConstantLossLatentModel", "copula": "Gaussian" or "T", "tOrders" (T), "integration",
// "recoveries" (constant loss), and "correlation" with "nVariables" (QuantLib's quote constructor) or "factorWeights"}.
//
//     DefaultLatentModel::probOfDefault       (model, basket, i, dates)
//     DefaultLatentModel::defaultCorrelation  (model, basket, date, i, j)
//     DefaultLatentModel::probAtLeastNEvents  (model, basket, n, date)
//     DefaultLatentModel::conditional         (model, basket, probabilities, i, factor samples):
//                                              [conditionalDefaultProbability(p, i, m) for each p and m,
//                                               conditionalDefaultProbabilityInvP(inverseCumulativeY(p, i), i, m)]
//     ConstantLossLatentModel::recoveries     (model, basket, date): [recoveries, conditionalRecovery(date, i, {0})
//                                              per name, expectedRecovery(date, i, key) per name]

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/portfoliospec.hpp"

#include <ql/experimental/credit/constantlosslatentmodel.hpp>
#include <ql/experimental/credit/defaultprobabilitylatentmodel.hpp>
#include <ql/experimental/math/gaussiancopulapolicy.hpp>
#include <ql/experimental/math/tcopulapolicy.hpp>
#include <ql/quotes/simplequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::array;
        using credit::portfolio::key;
        using credit::portfolio::makeBasket;
        using credit::portfolio::object;

        template <class T>
        struct Tag {
            using type = T;
        };

        std::vector<std::vector<Real>> matrix(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a matrix as an array of rows");
            std::vector<std::vector<Real>> out;
            for (const json::value& r : v.get_array())
                out.push_back(reals(r));
            return out;
        }

        // f(model) on a spec's model reset on a basket's spec.
        template <class F>
        json::value onModel(const json::value& spec, const json::value& basket, F f) {
            const std::string what = "a latent model";
            const json::object& o = object(spec, what);
            std::string kind = text(key(o, "kind", what));
            auto type = enumeration<LatentModelIntegrationType::LatentModelIntegrationType>(key(o, "integration", what));
            ext::shared_ptr<Basket> b = makeBasket(basket);
            auto build = [&](auto ini, auto tag) -> json::value {
                using CP = typename decltype(tag)::type;
                const json::value* c = o.if_contains("correlation");
                Handle<Quote> q;
                Size n = 0;
                if (c != nullptr) {
                    q = Handle<Quote>(ext::make_shared<SimpleQuote>(real(*c)));
                    n = static_cast<Size>(integer(key(o, "nVariables", what)));
                }
                if (kind == "DefaultLatentModel") {
                    ext::shared_ptr<DefaultLatentModel<CP>> m =
                        c != nullptr ? ext::make_shared<DefaultLatentModel<CP>>(q, n, type, ini)
                                     : ext::make_shared<DefaultLatentModel<CP>>(matrix(key(o, "factorWeights", what)),
                                                                                type, ini);
                    m->resetBasket(b);
                    return f(*m, *b);
                }
                if (kind == "ConstantLossLatentModel") {
                    std::vector<Real> r = reals(key(o, "recoveries", what));
                    ext::shared_ptr<ConstantLossLatentmodel<CP>> m =
                        c != nullptr
                            ? ext::make_shared<ConstantLossLatentmodel<CP>>(q, r, type, n, ini)
                            : ext::make_shared<ConstantLossLatentmodel<CP>>(matrix(key(o, "factorWeights", what)), r,
                                                                            type, ini);
                    m->resetBasket(b);
                    return f(*m, *b);
                }
                throw InvalidParams("unknown latent model " + kind);
            };
            std::string copula = text(key(o, "copula", what));
            if (copula == "Gaussian")
                return build(GaussianCopulaPolicy::initTraits(), Tag<GaussianCopulaPolicy>());
            if (copula == "T") {
                TCopulaPolicy::initTraits ini;
                ini.tOrders = integers(key(o, "tOrders", what));
                return build(ini, Tag<TCopulaPolicy>());
            }
            throw InvalidParams("unknown copula " + copula);
        }

        template <class F>
        Method method(std::size_t n, F f) {
            return [=](const Args& a) -> Call {
                arity(a, n);
                json::value m = a[0], b = a[1];
                Args rest(a.begin() + 2, a.end());
                return [=] { return onModel(m, b, [&](const auto& model, const Basket& basket) {
                                 return f(model, basket, rest);
                             }); };
            };
        }

        const Register unit({
            {"DefaultLatentModel::probOfDefault",
             method(4, [](const auto& m, const Basket&, const Args& r) {
                 auto i = static_cast<Size>(integer(r[0]));
                 json::array out;
                 for (const json::value& d : r[1].as_array())
                     out.emplace_back(m.probOfDefault(i, makeDate(date(d))));
                 return json::value(std::move(out));
             })},
            {"DefaultLatentModel::defaultCorrelation",
             method(5, [](const auto& m, const Basket&, const Args& r) {
                 return json::value(m.defaultCorrelation(makeDate(date(r[0])), static_cast<Size>(integer(r[1])),
                                                         static_cast<Size>(integer(r[2]))));
             })},
            {"DefaultLatentModel::probAtLeastNEvents",
             method(4, [](const auto& m, const Basket&, const Args& r) {
                 return json::value(m.probAtLeastNEvents(static_cast<Size>(integer(r[0])), makeDate(date(r[1]))));
             })},
            {"DefaultLatentModel::conditional",
             method(5, [](const auto& m, const Basket&, const Args& r) {
                 std::vector<Real> ps = reals(r[0]);
                 auto i = static_cast<Size>(integer(r[1]));
                 std::vector<std::vector<Real>> ms = matrix(r[2]);
                 json::array plain, inv;
                 for (Real p : ps)
                     for (const std::vector<Real>& f : ms) {
                         plain.emplace_back(m.conditionalDefaultProbability(p, i, f));
                         inv.emplace_back(m.conditionalDefaultProbabilityInvP(m.inverseCumulativeY(p, i), i, f));
                     }
                 json::array out;
                 out.emplace_back(std::move(plain));
                 out.emplace_back(std::move(inv));
                 return json::value(std::move(out));
             })},
            {"ConstantLossLatentModel::recoveries",
             method(3, [](const auto& m, const Basket& b, const Args& r) -> json::value {
                 if constexpr (std::is_base_of_v<ConstantLossLatentmodel<typename std::decay_t<decltype(m)>::copulaType>,
                                                 std::decay_t<decltype(m)>>) {
                     Date d = makeDate(date(r[0]));
                     json::array cond, expected;
                     for (Size i = 0; i < b.size(); i++) {
                         cond.emplace_back(m.conditionalRecovery(d, i, std::vector<Real>(1, 0.0)));
                         expected.emplace_back(m.expectedRecovery(d, i, b.defaultKeys()[i]));
                     }
                     json::array out;
                     out.push_back(array(m.recoveries()));
                     out.emplace_back(std::move(cond));
                     out.emplace_back(std::move(expected));
                     return json::value(std::move(out));
                 } else {
                     throw InvalidParams("not a constant-loss latent model");
                 }
             })},
        });

    }

}
