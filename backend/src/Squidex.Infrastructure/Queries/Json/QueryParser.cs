﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Infrastructure.Json;
using Squidex.Infrastructure.Json.Objects;
using Squidex.Infrastructure.Reflection;
using Squidex.Infrastructure.Validation;

namespace Squidex.Infrastructure.Queries.Json
{
    public static class QueryParser
    {
        public static ClrQuery Parse(this QueryModel model, string json, IJsonSerializer jsonSerializer)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ClrQuery();
            }

            var query = ParseFromJson(json, jsonSerializer);

            return Convert(model, query);
        }

        public static ClrQuery Convert(this QueryModel model, Query<IJsonValue> query)
        {
            if (query == null)
            {
                return new ClrQuery();
            }

            var result = SimpleMapper.Map(query, new ClrQuery());

            var errors = new List<string>();

            model.ConvertSorting(result, errors);
            model.ConvertFilters(result, errors, query);

            if (errors.Count > 0)
            {
                throw new ValidationException(errors.Select(BuildError).ToList());
            }

            return result;
        }

        private static void ConvertFilters(this QueryModel model, ClrQuery result, List<string> errors, Query<IJsonValue> query)
        {
            if (query.Filter == null)
            {
                return;
            }

            var filter = JsonFilterVisitor.Parse(query.Filter, model, errors);

            if (filter != null)
            {
                result.Filter = Optimizer<ClrValue>.Optimize(filter);
            }
        }

        private static void ConvertSorting(this QueryModel model, ClrQuery result, List<string> errors)
        {
            if (result.Sort == null)
            {
                return;
            }

            foreach (var sorting in result.Sort)
            {
                sorting.Path.GetMatchingFields(model.Schema, errors);
            }
        }

        public static Query<IJsonValue> ParseFromJson(string json, IJsonSerializer jsonSerializer)
        {
            try
            {
                return jsonSerializer.Deserialize<Query<IJsonValue>>(json);
            }
            catch (JsonException ex)
            {
                var error = Errors.InvalidQueryJson(ex.Message);

                throw new ValidationException(error);
            }
        }

        private static ValidationError BuildError(string message)
        {
            return new ValidationError(Errors.InvalidQuery(message));
        }
    }
}
