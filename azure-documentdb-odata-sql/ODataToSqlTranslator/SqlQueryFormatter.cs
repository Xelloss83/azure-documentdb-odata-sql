namespace Microsoft.Azure.Documents.OData.Sql
{
    using Microsoft.OData.Edm;

    /// <summary>
    /// string formmater for OData to Sql converter
    /// </summary>
    public class SQLQueryFormatter : IQueryFormatter
    {
        private char startLetter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLQueryFormatter"/> class.
        /// </summary>
        public SQLQueryFormatter()
        {
            this.startLetter = 'a';
            this.startLetter--;
        }

        /// <summary>
        /// fieldName => c.fieldName
        /// </summary>
        /// <param name="fieldName">The field name</param>
        /// <returns>Translated field</returns>
        public string TranslateFieldName(string fieldName)
        {
            return string.Concat(Constants.SQLFieldNameSymbol, Constants.SymbolDot, fieldName.Trim());
        }

        /// <summary>
        /// Convert value to SQL format: Namespace'enumVal' => c.enumVal
        /// </summary>
        /// <param name="typeName">the Odata enum type</param>
        /// <param name="value">string value of the type a number or literal</param>
        /// <returns>enumValue without the namespace</returns>
        public string TranslateEnumValue(IEdmTypeReference typeName, string value)
        {
            long result;
            return long.TryParse(value, out result)
                ? string.Concat(Constants.SymbolSingleQuote, typeName.AsEnum().ToStringLiteral(result), Constants.SymbolSingleQuote)
                : value;
        }

        /// <summary>
        /// Convert fieldname (parent and child) to SQL format: "class/field" => "c.class.field'"
        /// </summary>
        /// <param name="source">the parent field</param>
        /// <param name="edmProperty">the child field</param>
        /// <returns>The translated source</returns>
        public string TranslateSource(string source, string edmProperty)
        {
            var str = string.Concat(source.Trim(), Constants.SymbolDot, edmProperty.Trim());
            return str.StartsWith(Constants.SQLFieldNameSymbol + Constants.SymbolDot) ? str : string.Concat(Constants.SQLFieldNameSymbol, Constants.SymbolDot, str);
        }

        /// <summary>
        /// Convert functionName to SQL format: funtionName => FUNCTIONNAME
        /// </summary>
        /// <param name="functionName">The name of the funtion.</param>
        /// <returns>Translated funtion</returns>
        public string TranslateFunctionName(string functionName)
        {
            switch (functionName)
            {
                case Constants.KeywordToUpper:
                    return Constants.SQLUpperSymbol;

                case Constants.KeywordToLower:
                    return Constants.SQLLowerSymbol;

                case Constants.KeywordIndexOf:
                    return Constants.SQLIndexOfSymbol;

                case Constants.KeywordTrim:
                    return $"{Constants.SQLLtrimSymbol}{Constants.SymbolOpenParen}{Constants.SQLRtrimSymbol}";

                default:
                    return functionName.ToUpper();
            }
        }

        /// <summary>
        /// returns e.g. JOIN a IN c.companies.
        /// </summary>
        /// <param name="joinCollection">Collection to join.</param>
        /// <returns>Translated value.</returns>
        public string TranslateJoinClause(string joinCollection)
        {
            this.startLetter++;

            // startLetter becomes 'b', 'c' etc
            return string.Concat(
                Constants.SQLJoinSymbol,
                Constants.SymbolSpace,
                this.startLetter,
                Constants.SymbolSpace,
                Constants.SQLInSymbol,
                Constants.SymbolSpace,
                Constants.SQLFieldNameSymbol,
                Constants.SymbolDot,
                joinCollection);
        }

        /// <summary>
        /// translate any expression to a where clause.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="edmProperty">The property.</param>
        /// <returns>Translated value.</returns>
        public string TranslateJoinClause(string source, string edmProperty)
        {
            return string.Concat(this.startLetter, Constants.SymbolDot, edmProperty);
        }
    }
}
