// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The credit portfolio units' shared specs, as tools/vectors/gen/cxx/experimental/credit/portfolio.py writes them:
//
//     currency  a code ("EUR", "USD", "GBP"), or null for QuantLib's empty Currency()
//     type      {"atomic": AtomicDefault::Type, "restructuring": Restructuring::Type}, or
//               {"failureToPay": [length, units], "amount": x}
//     key       {} for DefaultProbKey(); {"northAmericaCorp": true, "currency", "seniority", and optionally "grace"
//               ([length, units]), "amount", "restructuring"}; or {"eventTypes": [type...], "currency", "seniority"}
//     event     {"kind": "DefaultEvent" (with "type"), "FailureToPayEvent" (with "amount") or "BankruptcyEvent",
//               "date", "currency", "seniority", "settlementDate" (a serial or null), and "recovery" (the single-rate
//               constructor) or "recoveries" ([[seniority, rate]...], the map constructor; none for an empty map)}
//     issuer    {"curves": [{"key": key, "curve": credit curve (termstructures/credit/creditspec.hpp)}...],
//               "events": [event...]}
//     pool      [{"name", "issuer", "key" (null for Pool::add's default)}...], added in order
//     basket    {"refDate", "notionals", "pool", "attachment", "detachment"}, the pool's names its names
//     model     {"kind": "ConstantLossModel", "copula": "Gaussian" or "T", "tOrders" (T), "integration", the
//               "recoveries", and either "correlation" with "nVariables" (QuantLib's quote constructor) or
//               "factorWeights"}; or {"kind": "DefaultLossModel"}, QuantLib's base with every default kept; the C2
//               models: "HomogeneousPool" or "InhomogeneousPool" (the constant-loss keys and "nBuckets", "max", "min",
//               "nSteps"), "RandomDefault" (the constant-loss keys and "nSims", "accuracy", "seed"), "RandomLoss"
//               (the copula keys, "factorWeights" of the defaults then the recoveries, "recoveries", "modelA",
//               "nSims", "accuracy", "seed") and "GaussianLHP" ("correlation", "recoveries")
//
// Everything is built inside the call, so that QuantLib's refusals are throws and a malformed spec InvalidParams.
#ifndef NANOQL_CXX_EXPERIMENTAL_CREDIT_PORTFOLIOSPEC_HPP
#define NANOQL_CXX_EXPERIMENTAL_CREDIT_PORTFOLIOSPEC_HPP

#include "convert.hpp"

#include <ql/currency.hpp>
#include <ql/experimental/credit/basket.hpp>
#include <ql/experimental/credit/defaultevent.hpp>
#include <ql/experimental/credit/defaultlossmodel.hpp>
#include <ql/experimental/credit/defaultprobabilitykey.hpp>
#include <ql/experimental/credit/defaulttype.hpp>
#include <ql/experimental/credit/issuer.hpp>
#include <ql/experimental/credit/pool.hpp>

#include <vector>

namespace harness::credit::portfolio {

    const json::object& object(const json::value& v, const std::string& what);
    const json::value& key(const json::object& o, const char* name, const std::string& what);
    QuantLib::Period period(const json::value& v);

    QuantLib::Currency makeCurrency(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::DefaultType> makeType(const json::value& v);
    QuantLib::DefaultProbKey makeKey(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::DefaultEvent> makeEvent(const json::value& v);
    QuantLib::Issuer makeIssuer(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::Pool> makePool(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::Basket> makeBasket(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::DefaultLossModel> makeLossModel(const json::value& v);

    // A basket with its model set: QuantLib's flow, the model linked at the first statistic.
    QuantLib::ext::shared_ptr<QuantLib::Basket> makeBoundBasket(const json::value& basket, const json::value& model);

    json::value currencyCode(const QuantLib::Currency& c);  // the code, or null for the empty currency
    json::value typeSummary(const QuantLib::DefaultType& t);  // [atomic, restructuring]
    json::value keySummary(const QuantLib::DefaultProbKey& k);  // [seniority, currency, [type summary...]]
    json::value array(const std::vector<QuantLib::Real>& x);

}

#endif
