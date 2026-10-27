// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/multiproduct.hpp, pathwisemultiproduct.hpp and ql/models/marketmodels/products/: the
// market-model products the wheel doesn't expose, stepped on curve states the harness sets.
//
// "MarketModelMultiProduct.steps" takes (product, stepForwards): the product (productspec.hpp), reset and stepped on an
// LMMCurveState of its rate times set on each step's forwards in turn, until it reports its path finished or the
// forwards run out; the result {"numberOfProducts", "maxNumberOfCashFlowsPerProductPerStep", "possibleCashFlowTimes",
// "evolutionTimes", "suggestedNumeraires" ({"throws": true} where QuantLib refuses it), "steps"}, each step {"done",
// "numberCashFlows", "cashFlows"}, each product's cash flows [[timeIndex, amount], ...], its count's first.
//
// "MarketModelPathwiseMultiProduct.steps" takes (product, stepForwards): a pathwise product (productspec.hpp's
// pathwiseProduct) stepped the same way, its cash flows' amounts sized 1 + the number of rates as QuantLib's engines
// size them; the result as above with "alreadyDeflated", each cash flow [timeIndex, amount, its derivatives...].

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"
#include "models/marketmodels/products/productspec.hpp"

#include <ql/errors.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>

#include <cstdint>
#include <memory>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        const Register unit({
            {"MarketModelMultiProduct.steps",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ProductSpec spec = product(a[0]);
                 Mat forwards = matrix(a[1]);
                 return [=] {
                     std::unique_ptr<MarketModelMultiProduct> p = makeProduct(spec);
                     const EvolutionDescription& evolution = p->evolution();
                     const std::vector<Time>& rateTimes = evolution.rateTimes();
                     for (const Vec& f : forwards)
                         if (f.size() + 1 != rateTimes.size())
                             throw InvalidParams("each step's forwards must be one a rate");
                     Size n = p->numberOfProducts();
                     std::vector<Size> counts(n);
                     std::vector<std::vector<MarketModelMultiProduct::CashFlow>> flows(
                         n, std::vector<MarketModelMultiProduct::CashFlow>(p->maxNumberOfCashFlowsPerProductPerStep()));
                     json::object out;
                     out["numberOfProducts"] = static_cast<std::int64_t>(n);
                     out["maxNumberOfCashFlowsPerProductPerStep"] =
                         static_cast<std::int64_t>(p->maxNumberOfCashFlowsPerProductPerStep());
                     out["possibleCashFlowTimes"] = result(p->possibleCashFlowTimes());
                     out["evolutionTimes"] = result(evolution.evolutionTimes());
                     try {
                         out["suggestedNumeraires"] = result(p->suggestedNumeraires());
                     } catch (Error&) {
                         json::object t;
                         t["throws"] = true;
                         out["suggestedNumeraires"] = t;
                     }
                     json::array steps;
                     p->reset();
                     for (const Vec& f : forwards) {
                         LMMCurveState cs(rateTimes);
                         cs.setOnForwardRates(f);
                         bool done = p->nextTimeStep(cs, counts, flows);
                         json::object step;
                         step["done"] = done;
                         step["numberCashFlows"] = result(counts);
                         json::array all;
                         for (Size i = 0; i < n; ++i) {
                             json::array product;
                             for (Size j = 0; j < counts[i]; ++j) {
                                 json::array flow;
                                 flow.emplace_back(static_cast<std::int64_t>(flows[i][j].timeIndex));
                                 flow.emplace_back(flows[i][j].amount);
                                 product.emplace_back(flow);
                             }
                             all.emplace_back(product);
                         }
                         step["cashFlows"] = all;
                         steps.emplace_back(step);
                         if (done)
                             break;
                     }
                     out["steps"] = steps;
                     return json::value(out);
                 };
             }},
            {"MarketModelPathwiseMultiProduct.steps",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ProductSpec spec = pathwiseProduct(a[0]);
                 Mat forwards = matrix(a[1]);
                 return [=] {
                     std::unique_ptr<MarketModelPathwiseMultiProduct> p = makePathwiseProduct(spec);
                     const EvolutionDescription& evolution = p->evolution();
                     const std::vector<Time>& rateTimes = evolution.rateTimes();
                     for (const Vec& f : forwards)
                         if (f.size() + 1 != rateTimes.size())
                             throw InvalidParams("each step's forwards must be one a rate");
                     Size n = p->numberOfProducts();
                     std::vector<Size> counts(n);
                     MarketModelPathwiseMultiProduct::CashFlow blank;
                     blank.amount.resize(rateTimes.size());
                     std::vector<std::vector<MarketModelPathwiseMultiProduct::CashFlow>> flows(
                         n, std::vector<MarketModelPathwiseMultiProduct::CashFlow>(
                                p->maxNumberOfCashFlowsPerProductPerStep(), blank));
                     json::object out;
                     out["numberOfProducts"] = static_cast<std::int64_t>(n);
                     out["maxNumberOfCashFlowsPerProductPerStep"] =
                         static_cast<std::int64_t>(p->maxNumberOfCashFlowsPerProductPerStep());
                     out["alreadyDeflated"] = p->alreadyDeflated();
                     out["possibleCashFlowTimes"] = result(p->possibleCashFlowTimes());
                     out["evolutionTimes"] = result(evolution.evolutionTimes());
                     try {
                         out["suggestedNumeraires"] = result(p->suggestedNumeraires());
                     } catch (Error&) {
                         json::object t;
                         t["throws"] = true;
                         out["suggestedNumeraires"] = t;
                     }
                     json::array steps;
                     p->reset();
                     for (const Vec& f : forwards) {
                         LMMCurveState cs(rateTimes);
                         cs.setOnForwardRates(f);
                         bool done = p->nextTimeStep(cs, counts, flows);
                         json::object step;
                         step["done"] = done;
                         step["numberCashFlows"] = result(counts);
                         json::array all;
                         for (Size i = 0; i < n; ++i) {
                             json::array product;
                             for (Size j = 0; j < counts[i]; ++j) {
                                 json::array flow;
                                 flow.emplace_back(static_cast<std::int64_t>(flows[i][j].timeIndex));
                                 for (Real x : flows[i][j].amount)
                                     flow.emplace_back(x);
                                 product.emplace_back(flow);
                             }
                             all.emplace_back(product);
                         }
                         step["cashFlows"] = all;
                         steps.emplace_back(step);
                         if (done)
                             break;
                     }
                     out["steps"] = steps;
                     return json::value(out);
                 };
             }},
        });

    }

}
