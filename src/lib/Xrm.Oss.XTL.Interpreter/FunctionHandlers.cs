﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using static Xrm.Oss.XTL.Interpreter.XTLInterpreter;

namespace Xrm.Oss.XTL.Interpreter
{
    public static class FunctionHandlers
    {
        public static FunctionHandler Not = (primary, service, tracing, organizationConfig, parameters) =>
        {
            var target = parameters.FirstOrDefault();

            if (!(target.Value is bool))
            {
                throw new InvalidPluginExecutionException("Not expects a boolean input, consider using one of the Is methods");
            }

            var result = !((bool)target.Value);

            return new ValueExpression(result.ToString(), result);
        };

        public static FunctionHandler First = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (parameters.Count != 1)
            {
                throw new InvalidPluginExecutionException("First expects a list as only parameter!");
            }

            var firstParam = parameters.FirstOrDefault().Value;

            if (!(firstParam is List<object>))
            {
                throw new InvalidPluginExecutionException("First expects a list as input");
            }

            return new ValueExpression(string.Empty, ((List<object>)firstParam).FirstOrDefault());
        };

        public static FunctionHandler Last = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (parameters.Count != 1)
            {
                throw new InvalidPluginExecutionException("Last expects a list as only parameter!");
            }

            var firstParam = parameters.FirstOrDefault().Value;

            if (!(firstParam is List<object>))
            {
                throw new InvalidPluginExecutionException("Last expects a list as input");
            }

            return new ValueExpression(string.Empty, ((List<object>)firstParam).LastOrDefault());
        };

        public static FunctionHandler IsEqual = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (parameters.Count != 2)
            {
                throw new InvalidPluginExecutionException("IsEqual expects exactly 2 parameters!");
            }

            var expected = parameters[0];
            var actual = parameters[1];

            var falseReturn = new ValueExpression (bool.FalseString, false);
            var trueReturn = new ValueExpression (bool.TrueString, true);

            if (expected.Value == null && actual.Value == null)
            {
                return trueReturn;
            }

            if (expected.Value == null && actual.Value != null)
            {
                return falseReturn;
            }

            if (expected.Value != null && actual.Value == null)
            {
                return falseReturn;
            }

            var result = expected.Value.Equals(actual.Value);

            if (expected.Value is string && actual.Value is string)
            {
                return new ValueExpression(result.ToString(), result);
            }

            if (expected.Value is bool && actual.Value is bool)
            {
                return new ValueExpression(result.ToString(), result);
            }

            if (expected.Value is int && actual.Value is int)
            {
                return new ValueExpression(result.ToString(), result);
            }

            if (expected.Value is EntityReference && actual.Value is EntityReference)
            {
                return new ValueExpression(result.ToString(), result);
            }

            if (new[] { expected.Value, actual.Value }.All(v => v is int || v is OptionSetValue))
            {
                var values = new[] { expected.Value, actual.Value }
                    .Select(v => v is OptionSetValue ? ((OptionSetValue)v).Value : (int)v)
                    .ToList();

                var optionSetResult = values[0].Equals(values[1]);
                return new ValueExpression(optionSetResult.ToString(), optionSetResult);
            }

            throw new InvalidPluginExecutionException($"Incompatible comparison types: {expected.Value.GetType().Name} and {actual.Value.GetType().Name}");
        };

        public static FunctionHandler And = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (parameters.Count != 2)
            {
                throw new InvalidPluginExecutionException("And expects at least 2 conditions!");
            }

            if (parameters.Any(p => !(p.Value is bool)))
            {
                throw new InvalidPluginExecutionException("And: All conditions must be booleans!");
            }

            if (parameters.All(p => (bool)p.Value))
            {
                return new ValueExpression(bool.TrueString, true);
            }

            return new ValueExpression(bool.FalseString, false);
        };

        public static FunctionHandler Or = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (parameters.Count != 2)
            {
                throw new InvalidPluginExecutionException("Or expects at least 2 conditions!");
            }

            if (parameters.Any(p => !(p.Value is bool)))
            {
                throw new InvalidPluginExecutionException("Or: All conditions must be booleans!");
            }

            if (parameters.Any(p => (bool)p.Value))
            {
                return new ValueExpression(bool.TrueString, true);
            }

            return new ValueExpression(bool.FalseString, false);
        };

        public static FunctionHandler IsNull = (primary, service, tracing, organizationConfig, parameters) =>
        {
            var target = parameters.FirstOrDefault();

            if (target.Value == null)
            {
                return new ValueExpression(bool.TrueString, true);
            }

            return new ValueExpression(bool.FalseString, false);
        };

        public static FunctionHandler If = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (parameters.Count != 3)
            {
                throw new InvalidPluginExecutionException("If-Then-Else expects exactly three parameters: Condition, True-Action, False-Action");
            }

            var condition = parameters[0];
            var trueAction = parameters[1];
            var falseAction = parameters[2];

            if (!(condition.Value is bool))
            {
                throw new InvalidPluginExecutionException("If condition must be a boolean!");
            }

            if ((bool)condition.Value)
            {
                tracing.Trace("Executing true condition");
                return new ValueExpression(new Lazy<ValueExpression>(() => trueAction));
            }

            tracing.Trace("Executing false condition");
            return new ValueExpression(new Lazy<ValueExpression>(() => falseAction));
        };

        public static FunctionHandler GetPrimaryRecord = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (primary == null)
            {
                return new ValueExpression(null);
            }

            return new ValueExpression(string.Empty, primary);
        };

        public static FunctionHandler GetRecordUrl = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (organizationConfig == null || string.IsNullOrEmpty(organizationConfig.OrganizationUrl))
            {
                throw new InvalidPluginExecutionException("GetRecordUrl can't find the Organization Url inside the plugin step secure configuration. Please add it.");
            }

            if (!parameters.All(p => p .Value is EntityReference || p.Value is Entity || p.Value == null))
            {
                throw new InvalidPluginExecutionException("Only Entity Reference ValueExpressions are supported in GetRecordUrl");
            }

            var refs = parameters.Where(p => p != null).Select(e =>
            {
                var entityReference = e.Value as EntityReference;

                if (entityReference != null) {
                    return new
                    {
                        Id = entityReference.Id,
                        LogicalName = entityReference.LogicalName
                    };
                }

                var entity = e.Value as Entity;
                
                return new
                {
                    Id = entity.Id,
                    LogicalName = entity.LogicalName
                };
            });
            var organizationUrl = organizationConfig.OrganizationUrl.EndsWith("/") ? organizationConfig.OrganizationUrl : organizationConfig.OrganizationUrl + "/";
            var urls = string.Join(Environment.NewLine, refs.Select(e =>
            {
                var url = $"{organizationUrl}main.aspx?etn={e.LogicalName}&id={e.Id}&newWindow=true&pagetype=entityrecord";
                return $"<a href=\"{url}\">{url}</a>";
            }));

            return new ValueExpression(urls, urls);
        };

        private static Func<string, IOrganizationService, Dictionary<string, string>> RetrieveColumnNames = (entityName, service) =>
        {
            return ((RetrieveEntityResponse) service.Execute(new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = entityName,
                RetrieveAsIfPublished = false
            }))
            .EntityMetadata
            .Attributes
            .ToDictionary(a => a.LogicalName, a => a?.DisplayName?.UserLocalizedLabel?.Label ?? a.LogicalName);
        };

        public static FunctionHandler RenderRecordTable = (primary, service, tracing, organizationConfig, parameters) =>
        {
            tracing.Trace("Parsing parameters");

            if (parameters.Count < 3)
            {
                throw new InvalidPluginExecutionException("RecordTable needs at least 3 parameters: Entities, entity name, add url boolean, display columns as separate string constants");
            }

            if (!(parameters[0].Value is List<object>))
            {
                throw new InvalidPluginExecutionException("RecordTable requires the first parameter to be a list of entities");
            }

            var records = ((List<object>)parameters[0].Value).Cast<Entity>().ToList();
            tracing.Trace($"Records: {records.Count}");

            // We need the entity name although it should be set in the record. If no records are passed, we would fail to display the grid with proper columns otherwise
            var entityName = parameters[1].Value as string;

            if (string.IsNullOrEmpty(entityName))
            {
                throw new InvalidPluginExecutionException("Second parameter of the RecordTable function needs to be the entity name as string");
            }

            var addRecordUrl = parameters[2].Value as bool?;

            // We need the column names explicitly, since CRM does not return new ValueExpression(null)-valued columns, so that we can't rely on the column union of all records. In addition to that, the order can be set this way
            var displayColumns = parameters.Skip(3)?.Select(p => p.Value).Cast<string>() ?? new List<string>();

            tracing.Trace("Retrieving column names");
            var columnNames = RetrieveColumnNames(entityName, service);
            tracing.Trace($"Column names done");

            var tableHeadStyle = @"style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px""";
            var tableDataStyle = @"style=""border:1px solid black;padding:1px 15px 1px 5px""";

            tracing.Trace("Parsed parameters");

            // Create table header
            var stringBuilder = new StringBuilder("<table>\n<tr>");
            foreach (var column in displayColumns)
            {
                var name = columnNames.ContainsKey(column) ? columnNames[column] : column;
                stringBuilder.AppendLine($"<th {tableHeadStyle}>{name}</th>");
            }

            // Add column for url if wanted
            if (addRecordUrl.HasValue && addRecordUrl.Value)
            {
                stringBuilder.AppendLine($"<th {tableHeadStyle}>URL</th>");
            }
            stringBuilder.AppendLine("<tr />");

            if (records != null)
            {
                foreach (var record in records)
                {
                    stringBuilder.AppendLine("<tr>");

                    foreach (var column in displayColumns)
                    {
                        stringBuilder.AppendLine($"<td {tableDataStyle}>{PropertyStringifier.Stringify(record, column)}</td>");
                    }

                    if (addRecordUrl.HasValue && addRecordUrl.Value)
                    {
                        stringBuilder.AppendLine($"<td {tableDataStyle}>{GetRecordUrl(primary, service, tracing, organizationConfig, new List<ValueExpression> { new ValueExpression(string.Empty, record)}).Value}</td>");
                    }

                    stringBuilder.AppendLine("<tr />");
                }
            }

            stringBuilder.AppendLine("</table>");
            var table = stringBuilder.ToString();

            return new ValueExpression(table, table);
        };

        public static FunctionHandler Fetch = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (parameters.Count < 1)
            {
                throw new InvalidPluginExecutionException("Fetch needs at least one parameter: Fetch XML.");
            }

            var fetch = parameters[0].Value as string;

            if (string.IsNullOrEmpty(fetch))
            {
                throw new InvalidPluginExecutionException("First parameter of Fetch function needs to be a fetchXml string");
            }

            var @params = parameters.Skip(1).ToList();
            
            List<object> references = new List<object> { primary.Id };

            if (@params is IEnumerable)
            {
                foreach (var item in @params)
                {
                    var reference = item.Value as EntityReference;
                    if (reference != null)
                    {
                        references.Add(reference.Id);
                        continue;
                    }

                    var entity = item.Value as Entity;
                    if (entity != null)
                    {
                        references.Add(entity.Id);
                        continue;
                    }

                    var optionSet = item.Value as OptionSetValue;
                    if (optionSet != null)
                    {
                        references.Add(optionSet.Value);
                        continue;
                    }

                    references.Add(item.Value);
                }
            }

            var records = new List<object>();

            var query = fetch;

            if (primary != null)
            {
                query = query.Replace("{0}", references[0].ToString());
            }

            tracing.Trace("Replacing references");

            var referenceRegex = new Regex("{([0-9]+)}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            query = referenceRegex.Replace(query, match =>
            {
                var capture = match.Groups[1].Value;
                var referenceNumber = int.Parse(capture);

                if (referenceNumber >= references.Count)
                {
                    throw new InvalidPluginExecutionException($"You tried using reference {referenceNumber} in fetch, but there are less reference inputs than that. You should probably wrap this fetch inside an if condition and only execute it, if your reference is non-null.");
                }

                return references[referenceNumber].ToString();
            });

            tracing.Trace("References replaced");
            tracing.Trace($"Executing fetch: {query}");
            records.AddRange(service.RetrieveMultiple(new FetchExpression(query)).Entities);

            return new ValueExpression(string.Empty, records);
        };

        public static FunctionHandler GetValue = (primary, service, tracing, organizationConfig, parameters) =>
        {
            if (primary == null)
            {
                return new ValueExpression(null);
            }

            var field = parameters.FirstOrDefault()?.Value as string;

            if (string.IsNullOrEmpty(field))
            {
                throw new InvalidPluginExecutionException("First parameter of Value function needs to be the field name as string");
            }

            var target = primary;

            if (parameters.Count > 1)
            {
                var explicitTarget = parameters[1].Value;

                if (explicitTarget != null)
                {
                    if (!(explicitTarget is Entity))
                    {
                        throw new InvalidPluginExecutionException("When passing a second parameter as primary entity to Value function, it has to be of type entity.");
                    }

                    target = parameters[1].Value as Entity; 
                }
                else
                {
                    return new ValueExpression(string.Empty, string.Empty);
                }
            }

            if (field == null)
            {
                throw new InvalidPluginExecutionException("Value requires a field target string as input");
            }

            return DataRetriever.ResolveTokenValue(field, target, service);
        };
    }
}
