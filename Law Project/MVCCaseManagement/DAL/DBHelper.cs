using System.Data;
using Microsoft.Data.SqlClient;

namespace MVCCaseManagement.DAL
{
    public class DBHelper
    {
        private readonly string _connectionString;

        public DBHelper(IConfiguration configuration)
        {
            // In production: set env var CONNECTIONSTRINGS__MVCCASEDB=<connection string>
            // ASP.NET Core's config system reads env vars automatically (double underscore = section separator)
            // This means the env var overrides appsettings.json without any code changes.
            _connectionString = configuration.GetConnectionString("MVCCaseDB");

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string 'MVCCaseDB' is not configured. " +
                    "Set the CONNECTIONSTRINGS__MVCCASEDB environment variable on the server, " +
                    "or configure it in appsettings.json for development.");
            }
        }

        public string GetConnectionString() => _connectionString;

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public DataTable ExecuteQuery(string query, SqlParameter[]? parameters = null)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(query, connection) { CommandTimeout = 120 };
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            using var adapter = new SqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            return dataTable;
        }

        public int ExecuteNonQuery(string query, SqlParameter[]? parameters = null)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(query, connection) { CommandTimeout = 120 };
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            connection.Open();
            return command.ExecuteNonQuery();
        }

        public object? ExecuteScalar(string query, SqlParameter[]? parameters = null)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(query, connection) { CommandTimeout = 120 };
            
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            connection.Open();
            return command.ExecuteScalar();
        }

        /// <summary>
        /// Execute a non-query command using an existing connection and transaction (ACID support).
        /// </summary>
        public int ExecuteNonQuery(string query, SqlParameter[]? parameters, SqlConnection connection, SqlTransaction transaction)
        {
            using var command = new SqlCommand(query, connection, transaction) { CommandTimeout = 120 };
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute a scalar command using an existing connection and transaction (ACID support).
        /// Returns the first column of the first row (e.g. SCOPE_IDENTITY()).
        /// </summary>
        public object? ExecuteScalar(string query, SqlParameter[]? parameters, SqlConnection connection, SqlTransaction transaction)
        {
            using var command = new SqlCommand(query, connection, transaction) { CommandTimeout = 120 };
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            return command.ExecuteScalar();
        }

        public void EnsureColumn(string tableName, string columnName, string columnDefinition)
        {
            string query = $@"
                IF NOT EXISTS (
                    SELECT 1 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = '{tableName}' 
                    AND COLUMN_NAME = '{columnName}'
                )
                BEGIN
                    ALTER TABLE {tableName} ADD {columnName} {columnDefinition};
                END";
            
            ExecuteNonQuery(query);
        }
    }
}
