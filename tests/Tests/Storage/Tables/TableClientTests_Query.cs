using Azure.Data.Tables;

using Microsoft.Extensions.Logging.Abstractions;

using Spotflow.InMemory.Azure.Storage.Tables.Internals;

using Tests.Utils;

namespace Tests.Storage.Tables;

[TestClass]
public class TableClientTests_Query
{

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Query_Entities_With_Linq_Should_Succeed()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKeyPrefix = Guid.NewGuid().ToString();

        tableClient.AddEntity(new TableEntity($"{partitionKeyPrefix}_A", "rk1"));
        tableClient.AddEntity(new TableEntity($"{partitionKeyPrefix}_A", "rk2"));
        tableClient.AddEntity(new TableEntity($"{partitionKeyPrefix}_B", "rk1"));

        var entities = tableClient.Query<TableEntity>(e => e.PartitionKey == $"{partitionKeyPrefix}_A");

        entities.Should().HaveCount(2).And.OnlyContain(e => e.PartitionKey == $"{partitionKeyPrefix}_A");
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow("PartitionKey eq '{prefix}_A'", 2, DisplayName = "Range query for single condition on standard property.")]
    [DataRow("PartitionKey eq '{prefix}_A' and RowKey eq 'rk1'", 1, DisplayName = "Simple point query with some results.")]
    [DataRow("PartitionKey eq '{prefix}_C' and RowKey eq 'rk1'", 0, DisplayName = "Simple point query without results.")]
    [DataRow("PartitionKey eq '{prefix}_A' and (RowKey eq 'rk1' or RowKey eq 'rk2')", 2, DisplayName = "OR point query with all results.")]
    [DataRow("PartitionKey eq '{prefix}_A' and (RowKey eq 'rk1' or RowKey eq 'rk4')", 1, DisplayName = "OR point query with some results.")]
    [DataRow("PartitionKey eq '{prefix}_A' and (RowKey eq 'rk3' or RowKey eq 'rk4')", 0, DisplayName = "OR point query without results.")]
    [DataRow("PartitionKey eq '{prefix}_A' and customProperty1 eq 'test'", 1, DisplayName = "Range query on custom string property.")]
    [DataRow("PartitionKey eq '{prefix}_A' and customProperty2 eq 42", 1, DisplayName = "Range query on custom integer property.")]
    [DataRow("PartitionKey eq '{prefix}_B' and customProperty3 eq 42.42d", 1, DisplayName = "Range query on custom double property.")]
    [DataRow("PartitionKey eq '{prefix}_A' and RowKey ge 'rk1' and RowKey lt 'rk3'", 2, DisplayName = "Range query with RowKey range condition.")]
    public void Query_Entities_With_String_Should_Succeed(string query, int numberOfResults)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKeyPrefix = Guid.NewGuid().ToString();

        tableClient.AddEntity(new TableEntity($"{partitionKeyPrefix}_A", "rk1") { ["customProperty1"] = "test" });
        tableClient.AddEntity(new TableEntity($"{partitionKeyPrefix}_A", "rk2") { ["customProperty2"] = 42 });
        tableClient.AddEntity(new TableEntity($"{partitionKeyPrefix}_B", "rk1") { ["customProperty3"] = 42.42d });

        var prefixedQuery = query.Replace("{prefix}", partitionKeyPrefix);

        var entities = tableClient.Query<TableEntity>(prefixedQuery);

        entities.Should().HaveCount(numberOfResults);
    }

    [TestMethod]
    public void Query_Matcher_Operators_For_Strings_Should_Be_Correct()
    {
        var entity = InMemoryTableEntity.CreateNew(new TableEntity("pk", "rk") { ["customProperty"] = "2024-04-12" }, TimeProvider.System);

        static TextQueryFilterMatcher matcher(string query) => new(query, NullLoggerFactory.Instance);

        var lessThan_success = matcher("customProperty lt '2024-04-13'");
        var lessThan_fail = matcher("customProperty lt '2024-04-12'");
        var lessThan_undefined = matcher("customProperty lt 1");
        var lessThanOrEqual_success = matcher("customProperty le '2024-04-12'");
        var lessThanOrEqual_fail = matcher("customProperty le '2024-04-11'");
        var lessThanOrEqual_undefined = matcher("customProperty le 1");
        var greaterThan_success = matcher("customProperty gt '2024-04-11'");
        var greaterThan_fail = matcher("customProperty gt '2024-04-12'");
        var greaterThan_undefined = matcher("customProperty gt 20250412");
        var greaterThanOrEqual_success = matcher("customProperty ge '2024-04-12'");
        var greaterThanOrEqual_fail = matcher("customProperty ge '2024-04-13'");
        var greaterThanOrEqual_undefined = matcher("customProperty ge 20250412");
        var equal_success = matcher("customProperty eq '2024-04-12'");
        var equal_fail = matcher("customProperty eq '2024-04-11'");
        var equal_undefined = matcher("customProperty eq 20240412");

        lessThan_success.IsMatch(entity).Should().BeTrue();
        lessThan_fail.IsMatch(entity).Should().BeFalse();
        lessThan_undefined.IsMatch(entity).Should().BeFalse();
        lessThanOrEqual_success.IsMatch(entity).Should().BeTrue();
        lessThanOrEqual_fail.IsMatch(entity).Should().BeFalse();
        lessThanOrEqual_undefined.IsMatch(entity).Should().BeFalse();
        greaterThan_success.IsMatch(entity).Should().BeTrue();
        greaterThan_fail.IsMatch(entity).Should().BeFalse();
        greaterThan_undefined.IsMatch(entity).Should().BeFalse();
        greaterThanOrEqual_success.IsMatch(entity).Should().BeTrue();
        greaterThanOrEqual_fail.IsMatch(entity).Should().BeFalse();
        greaterThanOrEqual_undefined.IsMatch(entity).Should().BeFalse();
        equal_success.IsMatch(entity).Should().BeTrue();
        equal_fail.IsMatch(entity).Should().BeFalse();
        equal_undefined.IsMatch(entity).Should().BeFalse();
    }


    [TestMethod]
    public void Query_Matcher_Operators_For_Integers_Should_Be_Correct()
    {
        var entity = InMemoryTableEntity.CreateNew(new TableEntity("pk", "rk") { ["customProperty"] = 42 }, TimeProvider.System);

        static TextQueryFilterMatcher matcher(string query) => new(query, NullLoggerFactory.Instance);

        var lessThan_success = matcher("customProperty lt 43");
        var lessThan_fail = matcher("customProperty lt 42");
        var lessThan_undefined = matcher("customProperty lt '43'");
        var lessThanOrEqual_success = matcher("customProperty le 42");
        var lessThanOrEqual_fail = matcher("customProperty le 41");
        var lessThanOrEqual_undefined = matcher("customProperty le '42'");
        var greaterThan_success = matcher("customProperty gt 41");
        var greaterThan_fail = matcher("customProperty gt 42");
        var greaterThan_undefined = matcher("customProperty gt '41'");
        var greaterThanOrEqual_success = matcher("customProperty ge 42");
        var greaterThanOrEqual_fail = matcher("customProperty ge 43");
        var greaterThanOrEqual_undefined = matcher("customProperty ge '42'");
        var equal_success = matcher("customProperty eq 42");
        var equal_fail = matcher("customProperty eq 43");
        var equal_undefined = matcher("customProperty eq '42'");

        lessThan_success.IsMatch(entity).Should().BeTrue();
        lessThan_fail.IsMatch(entity).Should().BeFalse();
        lessThan_undefined.IsMatch(entity).Should().BeFalse();
        lessThanOrEqual_success.IsMatch(entity).Should().BeTrue();
        lessThanOrEqual_fail.IsMatch(entity).Should().BeFalse();
        lessThanOrEqual_undefined.IsMatch(entity).Should().BeFalse();
        greaterThan_success.IsMatch(entity).Should().BeTrue();
        greaterThan_fail.IsMatch(entity).Should().BeFalse();
        greaterThan_undefined.IsMatch(entity).Should().BeFalse();
        greaterThanOrEqual_success.IsMatch(entity).Should().BeTrue();
        greaterThanOrEqual_fail.IsMatch(entity).Should().BeFalse();
        greaterThanOrEqual_undefined.IsMatch(entity).Should().BeFalse();
        equal_success.IsMatch(entity).Should().BeTrue();
        equal_fail.IsMatch(entity).Should().BeFalse();
        equal_undefined.IsMatch(entity).Should().BeFalse();
    }


    [TestMethod]
    public void Query_Matcher_Operators_For_Doubles_Should_Be_Correct()
    {
        var entity = InMemoryTableEntity.CreateNew(new TableEntity("pk", "rk") { ["customProperty"] = 3.141 }, TimeProvider.System);

        static TextQueryFilterMatcher matcher(string query) => new(query, NullLoggerFactory.Instance);

        var lessThan_success = matcher("customProperty lt 3.15");
        var lessThan_fail = matcher("customProperty lt 3.14");
        var lessThan_undefined = matcher("customProperty lt '3.13'");
        var lessThanOrEqual_success = matcher("customProperty le 3.141");
        var lessThanOrEqual_fail = matcher("customProperty le 3.13");
        var lessThanOrEqual_undefined = matcher("customProperty le '3.14'");
        var greaterThan_success = matcher("customProperty gt 3.13");
        var greaterThan_fail = matcher("customProperty gt 3.142");
        var greaterThan_undefined = matcher("customProperty gt '3.13'");
        var greaterThanOrEqual_success = matcher("customProperty ge 3.14");
        var greaterThanOrEqual_fail = matcher("customProperty ge 3.15");
        var greaterThanOrEqual_undefined = matcher("customProperty ge '3.14'");
        var equal_success = matcher("customProperty eq 3.141");
        var equal_fail = matcher("customProperty eq 3.15");
        var equal_undefined = matcher("customProperty eq '3.141'");

        lessThan_success.IsMatch(entity).Should().BeTrue();
        lessThan_fail.IsMatch(entity).Should().BeFalse();
        lessThan_undefined.IsMatch(entity).Should().BeFalse();
        lessThanOrEqual_success.IsMatch(entity).Should().BeTrue();
        lessThanOrEqual_fail.IsMatch(entity).Should().BeFalse();
        lessThanOrEqual_undefined.IsMatch(entity).Should().BeFalse();
        greaterThan_success.IsMatch(entity).Should().BeTrue();
        greaterThan_fail.IsMatch(entity).Should().BeFalse();
        greaterThan_undefined.IsMatch(entity).Should().BeFalse();
        greaterThanOrEqual_success.IsMatch(entity).Should().BeTrue();
        greaterThanOrEqual_fail.IsMatch(entity).Should().BeFalse();
        greaterThanOrEqual_undefined.IsMatch(entity).Should().BeFalse();
        equal_success.IsMatch(entity).Should().BeTrue();
        equal_fail.IsMatch(entity).Should().BeFalse();
        equal_undefined.IsMatch(entity).Should().BeFalse();
    }
}
