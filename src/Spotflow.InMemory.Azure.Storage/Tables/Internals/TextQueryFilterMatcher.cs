using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.UriParser;

namespace Spotflow.InMemory.Azure.Storage.Tables.Internals;

internal class TextQueryFilterMatcher
{
    private static readonly Uri _baseUri;
    private static readonly IEdmModel _edmModel;
    private static readonly string _defaultEntitySetName;

    private readonly SingleValueNode? _filterExpression;
    private readonly ILoggerFactory _loggerFactory;


    static TextQueryFilterMatcher()
    {
        _baseUri = new("https://example.com");
        _defaultEntitySetName = "Entities";

        var builder = new ODataConventionModelBuilder();

        builder.EntitySet<InMemoryTableEntity.EdmType>(_defaultEntitySetName);

        _edmModel = builder.GetEdmModel();
    }

    public TextQueryFilterMatcher(string? filter, ILoggerFactory loggerFactory)
    {
        if (filter is null)
        {
            _filterExpression = null;
        }
        else
        {
            var path = $"{_defaultEntitySetName}?{Uri.EscapeDataString("$filter")}={Uri.EscapeDataString(filter)}";

            var uri = new Uri(_baseUri, path);

            var parser = new ODataUriParser(_edmModel, _baseUri, uri);

            _filterExpression = parser.ParseFilter().Expression;
        }

        _loggerFactory = loggerFactory;

    }


    public bool IsMatch(InMemoryTableEntity entity)
    {
        if (_filterExpression is null)
        {
            return true;
        }

        var visitor = new ConditionVisitor(entity, _loggerFactory);

        return _filterExpression.Accept(visitor);

    }

    private static readonly BinaryOperatorKind[] _logicalOperators = [BinaryOperatorKind.Or, BinaryOperatorKind.And];
    private static readonly BinaryOperatorKind[] _comparisonOperators = [
        BinaryOperatorKind.Equal,
        BinaryOperatorKind.LessThan,
        BinaryOperatorKind.LessThanOrEqual,
        BinaryOperatorKind.GreaterThan,
        BinaryOperatorKind.GreaterThanOrEqual
        ];

    private class ConditionVisitor(InMemoryTableEntity entity, ILoggerFactory loggerFactory) : QueryNodeVisitor<bool>
    {
        private readonly ILogger<ConditionVisitor> _logger = loggerFactory.CreateLogger<ConditionVisitor>();

        public override bool Visit(BinaryOperatorNode nodeIn)
        {
            if (Array.IndexOf(_logicalOperators, nodeIn.OperatorKind) >= 0)
            {
                using var conditionLogScope = _logger.BeginScope("Condition: {left} {Condition} {right}", nodeIn.Left.Kind, nodeIn.OperatorKind, nodeIn.Right.Kind);

                _logger.LogInformation("Visiting left side.");

                var left = nodeIn.Left.Accept(this);

                _logger.LogInformation("Visiting right side.");

                var right = nodeIn.Right.Accept(this);

                return nodeIn.OperatorKind switch
                {
                    BinaryOperatorKind.Or => left || right,
                    BinaryOperatorKind.And => left && right,
                    _ => throw new InvalidOperationException($"Unexpected condition operator: {nodeIn.OperatorKind}.")
                };
            }

            if (Array.IndexOf(_comparisonOperators, nodeIn.OperatorKind) >= 0)
            {
                using var valueLogScope = _logger.BeginScope("Value: {left} {Operator} {right}", nodeIn.Left.Kind, nodeIn.OperatorKind, nodeIn.Right.Kind);

                var valueVisitor = new ValueVisitor(entity);

                _logger.LogInformation("Visiting left side.");

                var left = nodeIn.Left.Accept(valueVisitor);

                _logger.LogInformation("Visiting right side.");

                var right = nodeIn.Right.Accept(valueVisitor);

                return nodeIn.OperatorKind switch
                {
                    BinaryOperatorKind.Equal => left.IsEqual(right),
                    BinaryOperatorKind.LessThan => left.IsLessThan(right),
                    BinaryOperatorKind.LessThanOrEqual => left.IsLessThanOrEqual(right),
                    BinaryOperatorKind.GreaterThan => left.IsGreaterThan(right),
                    BinaryOperatorKind.GreaterThanOrEqual => left.IsGreaterThanOrEqual(right),
                    _ => throw new InvalidOperationException($"Unexpected comparison operator: {nodeIn.OperatorKind}")
                };
            }

            throw new InvalidOperationException($"Unexpected operator: {nodeIn.OperatorKind}.");

        }
    }

    private class ValueVisitor(InMemoryTableEntity entity) : QueryNodeVisitor<InMemoryTableEntityPropertyValue>
    {
        public override InMemoryTableEntityPropertyValue Visit(ConstantNode nodeIn)
        {
            return FromObject(nodeIn.Value);
        }

        public override InMemoryTableEntityPropertyValue Visit(ConvertNode nodeIn)
        {
            return nodeIn.Source.Accept(this);
        }

        public override InMemoryTableEntityPropertyValue Visit(SingleValuePropertyAccessNode nodeIn)
        {
            var propertyName = nodeIn.Property.Name;

            var value = entity.GetPropertyValueOrNull(propertyName);
            return FromObject(propertyName, value);
        }

        public override InMemoryTableEntityPropertyValue Visit(SingleValueOpenPropertyAccessNode nodeIn)
        {
            var propertyName = nodeIn.Name;

            var value = entity.GetPropertyValueOrNull(propertyName);
            return FromObject(propertyName, value);
        }

        private static InMemoryTableEntityPropertyValue FromObject(string propertyName, object? value)
        {
            if (InMemoryTableEntityPropertyValue.TryFromObject(value, out var result))
            {
                return result;
            }

            throw new NotSupportedException($"Property '{propertyName}' has unsupported type: {value} ({value?.GetType().ToString() ?? "<null>"})");
        }

        private static InMemoryTableEntityPropertyValue FromObject(object? value)
        {
            if (InMemoryTableEntityPropertyValue.TryFromObject(value, out var result))
            {
                return result;
            }

            throw new NotSupportedException($"Value '{value}' has unsupported type: ({value?.GetType().ToString() ?? "<null>"})");
        }

    }

    private abstract record InMemoryTableEntityPropertyValue
    {


        public virtual bool IsEqual(InMemoryTableEntityPropertyValue other) => this == other;
        public abstract bool IsLessThan(InMemoryTableEntityPropertyValue other);
        public abstract bool IsLessThanOrEqual(InMemoryTableEntityPropertyValue other);
        public abstract bool IsGreaterThan(InMemoryTableEntityPropertyValue other);
        public abstract bool IsGreaterThanOrEqual(InMemoryTableEntityPropertyValue other);


        public record None : InMemoryTableEntityPropertyValue
        {
            public static None Instance { get; } = new();

            public override bool IsGreaterThan(InMemoryTableEntityPropertyValue other) => false;
            public override bool IsGreaterThanOrEqual(InMemoryTableEntityPropertyValue other) => false;
            public override bool IsLessThan(InMemoryTableEntityPropertyValue other) => false;
            public override bool IsLessThanOrEqual(InMemoryTableEntityPropertyValue other) => false;
        }

        public record String(string Value) : InMemoryTableEntityPropertyValue
        {
            public override bool IsGreaterThan(InMemoryTableEntityPropertyValue other) => Compare(other) > 0;
            public override bool IsGreaterThanOrEqual(InMemoryTableEntityPropertyValue other) => Compare(other) >= 0;
            public override bool IsLessThan(InMemoryTableEntityPropertyValue other) => Compare(other) < 0;
            public override bool IsLessThanOrEqual(InMemoryTableEntityPropertyValue other) => Compare(other) <= 0;

            private int? Compare(InMemoryTableEntityPropertyValue other)
            {
                if (other is not String otherString)
                {
                    return null;
                }

                return string.Compare(Value, otherString.Value, StringComparison.Ordinal);
            }
        }

        public record Integer(long Value) : InMemoryTableEntityPropertyValue
        {
            public override bool IsGreaterThan(InMemoryTableEntityPropertyValue other) => Compare(other) > 0;
            public override bool IsGreaterThanOrEqual(InMemoryTableEntityPropertyValue other) => Compare(other) >= 0;
            public override bool IsLessThan(InMemoryTableEntityPropertyValue other) => Compare(other) < 0;
            public override bool IsLessThanOrEqual(InMemoryTableEntityPropertyValue other) => Compare(other) <= 0;

            private int? Compare(InMemoryTableEntityPropertyValue other)
            {
                if (other is not Integer otherInteger)
                {
                    return null;
                }

                return Value.CompareTo(otherInteger.Value);
            }
        }

        public record Double(double Value) : InMemoryTableEntityPropertyValue
        {

            public override bool IsEqual(InMemoryTableEntityPropertyValue other) => Compare(other) == 0;
            public override bool IsGreaterThan(InMemoryTableEntityPropertyValue other) => Compare(other) > 0;
            public override bool IsGreaterThanOrEqual(InMemoryTableEntityPropertyValue other) => Compare(other) >= 0;
            public override bool IsLessThan(InMemoryTableEntityPropertyValue other) => Compare(other) < 0;
            public override bool IsLessThanOrEqual(InMemoryTableEntityPropertyValue other) => Compare(other) <= 0;

            private int? Compare(InMemoryTableEntityPropertyValue other)
            {
                if (other is not Double otherDouble)
                {
                    return null;
                }

                var diff = Math.Abs(Value - otherDouble.Value);

                const double epsilon = 1.192092896e-07F;

                if (diff < epsilon)
                {
                    return 0;
                }

                return Value.CompareTo(otherDouble.Value);
            }
        }

        public static bool TryFromObject(object? value, [NotNullWhen(true)] out InMemoryTableEntityPropertyValue? result)
        {
            result = value switch
            {
                null => None.Instance,
                string v => new String(v),
                int v => new Integer(v),
                long v => new Integer(v),
                double v => new Double(v),
                float v => new Double(v),
                _ => null
            };

            return result is not null;
        }
    }

}

