using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CampusHub.UnitTests;

internal static class SqliteDecimalSupport
{
    public static void UseInvariantDecimalComparison(this SqliteConnection connection) =>
        connection.CreateCollation("EF_DECIMAL", (left, right) =>
            decimal.Parse(left, CultureInfo.InvariantCulture).CompareTo(decimal.Parse(right, CultureInfo.InvariantCulture)));
}
