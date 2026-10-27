// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/models/fwdtocotswapadapter.hpp, .cpp, cotswaptofwdadapter.hpp, .cpp and fwdperiodadapter.hpp,
// .cpp, which the wheel doesn't expose.
//
// "FwdToCotSwapAdapter" and "CotSwapToFwdAdapter" take (model): the adapter on a model spec (marketmodelspec.hpp), its
// reads (modelResults). "FwdToCotSwapAdapterFactory.create" and "CotSwapToFwdAdapterFactory.create" take
// "FlatVolFactory.create"'s arguments (flatvol.cpp): the adapter factory on that FlatVolFactory, the model it creates
// and its reads. "FwdPeriodAdapter" takes (model, period, offset, newDisplacements): the adapter's "initialRates",
// "displacements", "rateTimes", "evolutionTimes", "firstAliveRate", "numberOfRates", "numberOfFactors" and
// "numberOfSteps"; its pseudo-roots, on QuantLib's Y matrix, are left out.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/handle.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/models/cotswaptofwdadapter.hpp>
#include <ql/models/marketmodels/models/flatvol.hpp>
#include <ql/models/marketmodels/models/fwdperiodadapter.hpp>
#include <ql/models/marketmodels/models/fwdtocotswapadapter.hpp>
#include <ql/termstructures/yield/flatforward.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        struct FactorySpec {
            Real longTermCorrelation = 0.0, beta = 0.0;
            Vec times, vols;
            DateSpec referenceDate{};
            Real rate = 0.0;
            DayCounterSpec dc{};
            Real displacement = 0.0;
            Vec rateTimes, evolutionTimes;
            Size factors = 0;
        };

        FactorySpec factory(const Args& a) {
            arity(a, 11);
            FactorySpec f;
            f.longTermCorrelation = real(a[0]);
            f.beta = real(a[1]);
            f.times = reals(a[2]);
            f.vols = reals(a[3]);
            f.referenceDate = date(a[4]);
            f.rate = real(a[5]);
            f.dc = dayCounter(a[6]);
            f.displacement = real(a[7]);
            f.rateTimes = reals(a[8]);
            f.evolutionTimes = reals(a[9]);
            f.factors = size(a[10]);
            return f;
        }

        ext::shared_ptr<MarketModelFactory> makeFlatVolFactory(const FactorySpec& f) {
            Handle<YieldTermStructure> curve(
                ext::make_shared<FlatForward>(makeDate(f.referenceDate), f.rate, makeDayCounter(f.dc)));
            return ext::make_shared<FlatVolFactory>(f.longTermCorrelation, f.beta, f.times, f.vols, curve,
                                                    f.displacement);
        }

        const Register unit({
            {"FwdToCotSwapAdapter",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ModelSpec m = model(a[0]);
                 return [=] { return modelResults(FwdToCotSwapAdapter(makeModel(m))); };
             }},
            {"CotSwapToFwdAdapter",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ModelSpec m = model(a[0]);
                 return [=] { return modelResults(CotSwapToFwdAdapter(makeModel(m))); };
             }},
            {"FwdToCotSwapAdapterFactory.create",
             [](const Args& a) -> Call {
                 FactorySpec f = factory(a);
                 return [=] {
                     FwdToCotSwapAdapterFactory adapter(makeFlatVolFactory(f));
                     return modelResults(*adapter.create(EvolutionDescription(f.rateTimes, f.evolutionTimes), f.factors));
                 };
             }},
            {"CotSwapToFwdAdapterFactory.create",
             [](const Args& a) -> Call {
                 FactorySpec f = factory(a);
                 return [=] {
                     CotSwapToFwdAdapterFactory adapter(makeFlatVolFactory(f));
                     return modelResults(*adapter.create(EvolutionDescription(f.rateTimes, f.evolutionTimes), f.factors));
                 };
             }},
            {"FwdPeriodAdapter",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ModelSpec m = model(a[0]);
                 Size period = size(a[1]), offset = size(a[2]);
                 Vec displacements = reals(a[3]);
                 return [=] {
                     FwdPeriodAdapter adapter(makeModel(m), period, offset, displacements);
                     json::object out;
                     out["initialRates"] = result(adapter.initialRates());
                     out["displacements"] = result(adapter.displacements());
                     out["rateTimes"] = result(adapter.evolution().rateTimes());
                     out["evolutionTimes"] = result(adapter.evolution().evolutionTimes());
                     out["firstAliveRate"] = result(adapter.evolution().firstAliveRate());
                     out["numberOfRates"] = static_cast<std::int64_t>(adapter.numberOfRates());
                     out["numberOfFactors"] = static_cast<std::int64_t>(adapter.numberOfFactors());
                     out["numberOfSteps"] = static_cast<std::int64_t>(adapter.numberOfSteps());
                     return json::value(out);
                 };
             }},
        });

    }

}
