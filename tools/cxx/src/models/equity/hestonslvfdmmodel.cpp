// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/equity/hestonslvfdmmodel.hpp, .cpp: the finite-difference SLV model's log entries, which the wheel
// doesn't expose.
//
// "HestonSLVFDMModel::logEntries" takes (process, localVolatility, endDate, params, scheme, mixingFactor, entries): the
// process models/equity/hestonspec.hpp's spec; a LocalConstantVol at that volatility on the process's reference date
// and day counter; the end date; QuantLib's HestonSLVFokkerPlanckFdmParams fields in order up to the transformation
// type (17 numbers, then the Green's function's Algorithm and the TransformationType); the scheme [type, theta, mu];
// the mixing factor; and the indexes of the log entries to return. The model is built with logging on. It returns
// {"size"}, the number of entries, and for each index asked {"t", "prob", "x", "v"}: the entry's time, its density
// and its mesher's locations in each direction.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/models/equity/hestonmodel.hpp>
#include <ql/models/equity/hestonslvfdmmodel.hpp>
#include <ql/termstructures/volatility/equityfx/localconstantvol.hpp>

#include <iterator>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::array toJson(const std::vector<Real>& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return out;
        }

        const Register unit({
            {"HestonSLVFDMModel::logEntries",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 equity::ProcessSpec p = equity::process(a[0]);
                 Real volatility = real(a[1]);
                 DateSpec endDate = date(a[2]);
                 if (!a[3].is_array() || a[3].get_array().size() != 19)
                     throw InvalidParams("expected the parameters' 19 fields, got " + json::serialize(a[3]));
                 const json::array& f = a[3].get_array();
                 std::vector<Real> numbers;
                 for (std::size_t i = 0; i < 17; ++i)
                     numbers.push_back(real(f[i]));
                 auto greens = enumeration<FdmHestonGreensFct::Algorithm>(f[17]);
                 auto trafo = enumeration<FdmSquareRootFwdOp::TransformationType>(f[18]);
                 if (!a[4].is_array() || a[4].get_array().size() != 3)
                     throw InvalidParams("expected a scheme [type, theta, mu], got " + json::serialize(a[4]));
                 auto schemeType = enumeration<FdmSchemeDesc::FdmSchemeType>(a[4].get_array()[0]);
                 Real theta = real(a[4].get_array()[1]), mu = real(a[4].get_array()[2]);
                 Real mixingFactor = real(a[5]);
                 std::vector<Integer> entries = integers(a[6]);
                 auto size = [](Real x) {
                     if (!(x >= 0.0))
                         throw InvalidParams("a size must be non-negative");
                     return static_cast<Size>(x);
                 };
                 HestonSLVFokkerPlanckFdmParams params = {
                     size(numbers[0]), size(numbers[1]), size(numbers[2]), size(numbers[3]), numbers[4],
                     size(numbers[5]), size(numbers[6]), numbers[7], numbers[8], size(numbers[9]), numbers[10],
                     numbers[11], numbers[12], numbers[13], numbers[14], numbers[15], numbers[16], greens, trafo,
                     FdmSchemeDesc(schemeType, theta, mu)};
                 return [=] {
                     auto process = equity::makeProcess(p);
                     Handle<LocalVolTermStructure> localVol(ext::make_shared<LocalConstantVol>(
                         makeDate(p.referenceDate), volatility, makeDayCounter(p.dayCounter)));
                     HestonSLVFDMModel model(localVol, Handle<HestonModel>(ext::make_shared<HestonModel>(process)),
                                             makeDate(endDate), params, true, std::vector<Date>(), mixingFactor);
                     const std::list<HestonSLVFDMModel::LogEntry>& log = model.logEntries();
                     json::array asked;
                     for (Integer i : entries) {
                         if (i < 0 || static_cast<Size>(i) >= log.size())
                             throw InvalidParams("an entry index past the log's end");
                         const auto& e = *std::next(log.begin(), i);
                         const auto& meshers = e.mesher->getFdm1dMeshers();
                         asked.emplace_back(json::object{
                             {"t", e.t},
                             {"prob", toJson(std::vector<Real>(e.prob->begin(), e.prob->end()))},
                             {"x", toJson(meshers.at(0)->locations())},
                             {"v", toJson(meshers.at(1)->locations())}});
                     }
                     return json::value(json::object{{"size", static_cast<double>(log.size())}, {"entries", asked}});
                 };
             }},
        });

    }

}
