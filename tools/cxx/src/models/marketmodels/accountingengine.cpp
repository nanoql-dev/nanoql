// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/discounter.hpp and accountingengine.hpp: the discounter and the accounting engine, which the
// wheel doesn't expose.
//
// "MarketModelDiscounter.numeraireBonds" takes (paymentTime, rateTimes, forwards, numeraire): the discounter's
// numeraire bonds on an LMMCurveState of the rate times set on the forwards.
//
// "AccountingEngine.values" takes (evolverType, model, generator, numeraires, product, initialNumeraireValue, paths):
// the evolver of the type ("LogNormalFwdRateEuler", "LogNormalFwdRatePc" or "NormalFwdRatePc") on the model and a
// generator factory (marketmodelspec.hpp) from step 0, the product (productspec.hpp), and the engine's values over the
// paths in a SequenceStatisticsInc of the products' dimension; the result {"samples", "weightSum", "mean", "variance",
// "errorEstimate"}, the last two {"throws": true} on one path.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"
#include "models/marketmodels/products/productspec.hpp"

#include <ql/errors.hpp>
#include <ql/math/statistics/sequencestatistics.hpp>
#include <ql/models/marketmodels/accountingengine.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/discounter.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateeuler.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdratepc.hpp>
#include <ql/models/marketmodels/evolvers/normalfwdratepc.hpp>

#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value attempt(const std::function<json::value()>& f) {
            try {
                return f();
            } catch (Error&) {
                json::object t;
                t["throws"] = true;
                return t;
            }
        }

        const Register unit({
            {"MarketModelDiscounter.numeraireBonds",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real paymentTime = real(a[0]);
                 Vec rateTimes = reals(a[1]), forwards = reals(a[2]);
                 Size numeraire = size(a[3]);
                 if (rateTimes.size() < 2 || forwards.size() + 1 != rateTimes.size() || numeraire >= rateTimes.size())
                     throw InvalidParams("two rate times at least, a forward a rate and a numeraire among the bonds");
                 return [=] {
                     LMMCurveState cs(rateTimes);
                     cs.setOnForwardRates(forwards);
                     return json::value(MarketModelDiscounter(paymentTime, rateTimes).numeraireBonds(cs, numeraire));
                 };
             }},
            {"AccountingEngine.values",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::string type = text(a[0]);
                 if (type != "LogNormalFwdRateEuler" && type != "LogNormalFwdRatePc" && type != "NormalFwdRatePc")
                     throw InvalidParams("unknown evolver type " + type);
                 ModelSpec m = model(a[1]);
                 GeneratorSpec g = generator(a[2]);
                 std::vector<Size> numeraires = sizes(a[3]);
                 ProductSpec p = product(a[4]);
                 Real initialNumeraireValue = real(a[5]);
                 Size paths = size(a[6]);
                 return [=] {
                     std::unique_ptr<MarketModelMultiProduct> prod = makeProduct(p);
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     std::unique_ptr<BrownianGeneratorFactory> f = makeFactory(g);
                     ext::shared_ptr<MarketModelEvolver> e;
                     if (type == "LogNormalFwdRateEuler")
                         e = ext::make_shared<LogNormalFwdRateEuler>(mm, *f, numeraires);
                     else if (type == "LogNormalFwdRatePc")
                         e = ext::make_shared<LogNormalFwdRatePc>(mm, *f, numeraires);
                     else
                         e = ext::make_shared<NormalFwdRatePc>(mm, *f, numeraires);
                     AccountingEngine engine(e, *prod, initialNumeraireValue);
                     SequenceStatisticsInc stats(prod->numberOfProducts());
                     engine.multiplePathValues(stats, paths);
                     json::object out;
                     out["samples"] = static_cast<std::int64_t>(stats.samples());
                     out["weightSum"] = stats.weightSum();
                     out["mean"] = result(stats.mean());
                     out["variance"] = attempt([&] { return result(stats.variance()); });
                     out["errorEstimate"] = attempt([&] { return result(stats.errorEstimate()); });
                     return json::value(out);
                 };
             }},
        });

    }

}
