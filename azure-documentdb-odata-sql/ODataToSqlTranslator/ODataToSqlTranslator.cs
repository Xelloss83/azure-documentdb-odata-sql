﻿namespace Microsoft.Azure.Documents.OData.Sql
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Web.OData.Query;

    using Microsoft.OData.Core.UriParser;
    using Microsoft.OData.Core.UriParser.Semantic;

    /// <summary>
    /// TranslateOptions
    /// </summary>
    [Flags]
    public enum TranslateOptions
    {
        /// <summary>
        /// translate option for Sql SELECT clause
        /// </summary>
        SELECT_CLAUSE = 1,

        /// <summary>
        /// translate option for Sql WHERE clause
        /// </summary>
        WHERE_CLAUSE = 1 << 1,

        /// <summary>
        /// translate option for Sql ORDER BY clause
        /// </summary>
        ORDERBY_CLAUSE = 1 << 2,

        /// <summary>
        /// translate option for sql TOP clause
        /// </summary>
        TOP_CLAUSE = 1 << 3,

        /// <summary>
        /// translate option for all Sql clauses: SELECT, WHERE, ORDER BY, and TOP
        /// </summary>
        ALL = SELECT_CLAUSE | WHERE_CLAUSE | ORDERBY_CLAUSE | TOP_CLAUSE
    }

    /// <summary>
    /// ODataToSqlTranslator
    /// </summary>
    public class ODataToSqlTranslator
    {
        /// <summary>
        /// The visitor patterned ODataNodeToStringBuilder
        /// </summary>
        private readonly ODataNodeToStringBuilder oDataNodeToStringBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataToSqlTranslator"/> class.
        /// </summary>
        /// <param name="queryFormatter">Optional QueryFormatter, if no formatter provided, a SQLQueryFormatter is used by default</param>
        public ODataToSqlTranslator(IQueryFormatter queryFormatter = null)
        {
            queryFormatter = queryFormatter ?? new SQLQueryFormatter();
            this.oDataNodeToStringBuilder = new ODataNodeToStringBuilder(queryFormatter);
        }

        /// <summary>
        /// Function that takes in an <see cref="ODataQueryOptions"/>, a string representing the type to filter
        /// </summary>
        /// <param name="odataQueryOptions">The query.</param>
        /// <param name="translateOptions">Translation options.</param>
        /// <param name="additionalWhereClause">Additional clause to apply.</param>
        /// <returns>returns an SQL expression if successfully translated, otherwise a null string</returns>
        public string Translate(ODataQueryOptions odataQueryOptions, TranslateOptions translateOptions, string additionalWhereClause = null)
        {
            // TODO: refactor to use a StringBuilder
            string selectClause, whereClause, orderbyClause, topClause;
            selectClause = whereClause = orderbyClause = topClause = string.Empty;
            bool hasJoinClause = false;

            // WHERE CLAUSE
            if ((translateOptions & TranslateOptions.WHERE_CLAUSE) == TranslateOptions.WHERE_CLAUSE)
            {
                var customWhereClause = additionalWhereClause == null
                    ? string.Empty
                    : $"{additionalWhereClause}";
                var hasFilterClause = odataQueryOptions?.Filter?.FilterClause;
                Tuple<string, string> retVal = null;
                if (hasFilterClause != null)
                {
                    retVal = this.TranslateFilterClause(hasFilterClause);
                }

                whereClause = hasFilterClause == null
                    ? string.Empty
                    : retVal.Item2;

                whereClause = (!string.IsNullOrEmpty(customWhereClause) && !string.IsNullOrEmpty(whereClause))
                    ? $"{customWhereClause} AND {whereClause}"
                    : $"{customWhereClause}{whereClause}";

                whereClause = string.IsNullOrEmpty(whereClause)
                    ? string.Empty
                    : $"{Constants.SQLWhereSymbol} {whereClause}";

                if (!string.IsNullOrEmpty(retVal?.Item1))
                {
                    hasJoinClause = true;
                    whereClause = string.Concat(retVal.Item1, " ", whereClause);
                }
            }

            // SELECT CLAUSE
            if ((translateOptions & TranslateOptions.SELECT_CLAUSE) == TranslateOptions.SELECT_CLAUSE)
            {
                // TOP CLAUSE
                if ((translateOptions & TranslateOptions.TOP_CLAUSE) == TranslateOptions.TOP_CLAUSE)
                {
                    topClause = odataQueryOptions?.Top?.Value > 0
                        ? $"{Constants.SQLTopSymbol} {odataQueryOptions.Top.Value} "
                        : string.Empty;
                }

                selectClause = odataQueryOptions?.SelectExpand?.RawSelect == null
                    ? hasJoinClause ? string.Concat(Constants.SqlValueSymbol, Constants.SymbolSpace, Constants.SQLFieldNameSymbol) : Constants.SQLAsteriskSymbol
                    : string.Join(", ", odataQueryOptions.SelectExpand.RawSelect.Split(',').Select(c => string.Concat(Constants.SQLFieldNameSymbol, Constants.SymbolDot, c.Trim())));

                selectClause = $"{Constants.SQLSelectSymbol} {topClause}{selectClause} {Constants.SQLFromSymbol} {Constants.SQLFieldNameSymbol} ";
            }

            // ORDER BY CLAUSE
            if ((translateOptions & TranslateOptions.ORDERBY_CLAUSE) == TranslateOptions.ORDERBY_CLAUSE)
            {
                orderbyClause = odataQueryOptions?.OrderBy?.OrderByClause == null
                    ? string.Empty
                    : $"{Constants.SQLOrderBySymbol} {this.TranslateOrderByClause(odataQueryOptions.OrderBy.OrderByClause)} ";
            }

            var sb = new StringBuilder();
            sb.Append(selectClause);
            sb.Append(whereClause);
            var sp = default(string);
            if (!string.IsNullOrEmpty(whereClause))
            {
                sp = " ";
            }

            if (!string.IsNullOrEmpty(orderbyClause))
            {
                sb.Append(sp);
                sb.Append(orderbyClause);
            }

            return sb.ToString();
        }

        /// <summary>Translates a <see cref="FilterClause"/> into a <see cref="FilterClause"/>.</summary>
        /// <param name="filterClause">The filter clause to translate.</param>
        /// <returns>The translated string.</returns>
        private Tuple<string, string> TranslateFilterClause(FilterClause filterClause)
        {
            var tmp = this.oDataNodeToStringBuilder.TranslateNode(filterClause.Expression);
            if (string.IsNullOrEmpty(tmp) || tmp.IndexOf(Constants.Delimiter, StringComparison.Ordinal) < 0)
            {
                return new Tuple<string, string>(null, tmp);
            }

            var splited = tmp.Split(new[] { Constants.Delimiter[0] }, options: StringSplitOptions.RemoveEmptyEntries);
            var sbJoin = new StringBuilder();
            var sbWhere = new StringBuilder();
            var sp = string.Empty;

            foreach (var a in splited)
            {
                if (a.StartsWith(Constants.SQLJoinSymbol))
                {
                    sbJoin.Append(sp);
                    sbJoin.Append(a);
                    if (sp.Length == 0)
                    {
                        sp = " ";
                    }
                }
                else
                {
                    sbWhere.Append(a);
                }
            }

            return new Tuple<string, string>(sbJoin.ToString(), sbWhere.ToString());
        }

        /// <summary>Translates a <see cref="OrderByClause"/> into a <see cref="OrderByClause"/>.</summary>
        /// <param name="orderByClause">The orderBy clause to translate.</param>
        /// <param name="preExpr">expression built so far.</param>
        /// <returns>The translated string.</returns>
        private string TranslateOrderByClause(OrderByClause orderByClause, string preExpr = null)
        {
            string expr = string.Concat(this.oDataNodeToStringBuilder.TranslateNode(orderByClause.Expression), Constants.SymbolSpace, orderByClause.Direction == OrderByDirection.Ascending ? Constants.KeywordAscending.ToUpper() : Constants.KeywordDescending.ToUpper());

            expr = string.IsNullOrWhiteSpace(preExpr) ? expr : string.Concat(preExpr, Constants.SymbolComma, Constants.SymbolSpace, expr);

            if (orderByClause.ThenBy != null)
            {
                expr = this.TranslateOrderByClause(orderByClause.ThenBy, expr);
            }

            return expr;
        }
    }
}
