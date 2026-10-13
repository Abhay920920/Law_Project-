using System;
using System.Data;

namespace MVCCaseManagement.Common
{
    /// <summary>
    /// High-performance, null-safe DataRow extension methods to simplify ADO.NET and Dapper data reading,
    /// eliminate repeated column existence checks, and prevent IndexOutOfRangeException runtime crashes.
    /// </summary>
    public static class DataRowExtensions
    {
        public static bool HasColumn(this DataRow row, string colName)
        {
            return row?.Table?.Columns != null && row.Table.Columns.Contains(colName);
        }

        public static string? GetString(this DataRow row, string colName)
        {
            if (row == null || !row.HasColumn(colName) || row[colName] == DBNull.Value)
                return null;

            return row[colName]?.ToString();
        }

        public static int? GetInt(this DataRow row, string colName)
        {
            if (row == null || !row.HasColumn(colName) || row[colName] == DBNull.Value)
                return null;

            var val = row[colName];
            if (val is int i) return i;
            if (val is short s) return (int)s;
            if (val is long l && l <= int.MaxValue && l >= int.MinValue) return (int)l;

            if (int.TryParse(val?.ToString(), out int parsed))
                return parsed;

            return null;
        }

        public static int GetInt(this DataRow row, string colName, int fallback)
        {
            return row.GetInt(colName) ?? fallback;
        }

        public static long? GetLong(this DataRow row, string colName)
        {
            if (row == null || !row.HasColumn(colName) || row[colName] == DBNull.Value)
                return null;

            var val = row[colName];
            if (val is long l) return l;
            if (val is int i) return (long)i;

            if (long.TryParse(val?.ToString(), out long parsed))
                return parsed;

            return null;
        }

        public static decimal? GetDecimal(this DataRow row, string colName)
        {
            if (row == null || !row.HasColumn(colName) || row[colName] == DBNull.Value)
                return null;

            var val = row[colName];
            if (val is decimal d) return d;
            if (val is double dbl) return (decimal)dbl;
            if (val is float f) return (decimal)f;
            if (val is int i) return (decimal)i;

            if (decimal.TryParse(val?.ToString(), out decimal parsed))
                return parsed;

            return null;
        }

        public static DateTime? GetDate(this DataRow row, string colName)
        {
            if (row == null || !row.HasColumn(colName) || row[colName] == DBNull.Value)
                return null;

            var val = row[colName];
            if (val is DateTime dt) return dt;

            if (DateTime.TryParse(val?.ToString(), out DateTime parsed))
                return parsed;

            return null;
        }

        public static bool GetBool(this DataRow row, string colName, bool fallback = false)
        {
            if (row == null || !row.HasColumn(colName) || row[colName] == DBNull.Value)
                return fallback;

            return ToBool(row[colName]) ?? fallback;
        }

        public static bool? GetBoolNullable(this DataRow row, string colName)
        {
            if (row == null || !row.HasColumn(colName) || row[colName] == DBNull.Value)
                return null;

            return ToBool(row[colName]);
        }

        public static bool? ToBool(object? value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (value is bool b) return b;

            string s = value.ToString()?.Trim() ?? "";
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase) || s.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;

            return null;
        }
    }
}
