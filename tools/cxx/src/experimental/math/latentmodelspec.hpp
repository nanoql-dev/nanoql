// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The latent model units' shared specs, as tools/vectors/gen/cxx/experimental/math/latentmodel.py writes them: a model
// {"copula": "Gaussian" or "T", "tOrders" (T), and one of "factorWeights" (the matrix constructor), "weights" (the
// one-factor vector constructor), "correlSqr" with "nVariables", or "correlation" with "nVariables" (the quote
// constructor)}, built as the harness's own subclass of LatentModel<CP>, which adds an integration of a given type
// (QuantLib's protected IntegrationFactory) so that the base's integratedExpectedValue answers; and the integrands by
// name ("one", "m0", "m0sq", "m0m1", "exp", "indicator", "cos"), each a function of the systemic factors.
#ifndef NANOQL_CXX_EXPERIMENTAL_MATH_LATENTMODELSPEC_HPP
#define NANOQL_CXX_EXPERIMENTAL_MATH_LATENTMODELSPEC_HPP

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "convert.hpp"

#include <ql/experimental/math/gaussiancopulapolicy.hpp>
#include <ql/experimental/math/latentmodel.hpp>
#include <ql/experimental/math/tcopulapolicy.hpp>
#include <ql/quotes/simplequote.hpp>

#include <cmath>
#include <functional>
#include <string>
#include <vector>

namespace harness::latent {

    template <class CP>
    class HarnessLatentModel : public QuantLib::LatentModel<CP> {
      public:
        template <class... A>
        HarnessLatentModel(QuantLib::LatentModelIntegrationType::LatentModelIntegrationType type, A&&... a)
        : QuantLib::LatentModel<CP>(std::forward<A>(a)...),
          integration_(QuantLib::LatentModel<CP>::IntegrationFactory::createLMIntegration(this->numFactors(), type)) {}
        const QuantLib::ext::shared_ptr<QuantLib::LMIntegration>& integration() const override { return integration_; }

      private:
        QuantLib::ext::shared_ptr<QuantLib::LMIntegration> integration_;
    };

    template <class T>
    struct Tag {
        using type = T;
    };

    std::vector<std::vector<QuantLib::Real>> matrix(const json::value& v);
    std::function<QuantLib::Real(const std::vector<QuantLib::Real>&)> integrand(const std::string& name);
    QuantLib::TCopulaPolicy::initTraits tTraits(const json::value& orders);

    // The model of a spec, f called with it: f(const HarnessLatentModel<CP>&).
    template <class F>
    json::value onModel(const json::value& spec,
                        QuantLib::LatentModelIntegrationType::LatentModelIntegrationType type,
                        F f) {
        using namespace QuantLib;
        if (!spec.is_object())
            throw InvalidParams("expected a latent model spec");
        const json::object& o = spec.get_object();
        auto build = [&](auto ini, auto tag) -> json::value {
            using CP = typename decltype(tag)::type;
            if (const json::value* w = o.if_contains("factorWeights"); w != nullptr)
                return f(HarnessLatentModel<CP>(type, matrix(*w), ini));
            if (const json::value* w = o.if_contains("weights"); w != nullptr)
                return f(HarnessLatentModel<CP>(type, reals(*w), ini));
            if (const json::value* c = o.if_contains("correlSqr"); c != nullptr)
                return f(HarnessLatentModel<CP>(type, real(*c), static_cast<Size>(integer(o.at("nVariables"))), ini));
            if (const json::value* c = o.if_contains("correlation"); c != nullptr)
                return f(HarnessLatentModel<CP>(type, Handle<Quote>(ext::make_shared<SimpleQuote>(real(*c))),
                                                static_cast<Size>(integer(o.at("nVariables"))), ini));
            throw InvalidParams("a latent model spec needs factorWeights, weights, correlSqr or correlation");
        };
        std::string copula = text(o.at("copula"));
        if (copula == "Gaussian")
            return build(GaussianCopulaPolicy::initTraits(), Tag<GaussianCopulaPolicy>());
        if (copula == "T")
            return build(tTraits(o.at("tOrders")), Tag<TCopulaPolicy>());
        throw InvalidParams("unknown copula " + copula);
    }

}

#endif
