using System;
using System.IO;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace LawProject.Tools
{
    class Program
    {
        static void Main(string[] args)
        {
            string connStr = "Data Source=198.38.89.31;Initial Catalog=Admin_Law;User ID=admin_Law;Password=4c4H_0l8q;Connection Timeout=60;Encrypt=True;TrustServerCertificate=True;";
            string outputPath = args.Length > 0 ? args[0] : @"c:\Users\adts-\Desktop\Law Project\Law Project\MVCCaseManagement\SQL\00_Master_Full_Database_Schema_And_Seed.sql";

            Console.WriteLine("Starting full database export from Admin_Law...");
            var sb = new StringBuilder();

            sb.AppendLine("-- =============================================================================");
            sb.AppendLine("-- 🏛️ Law Project — Master Database Schema & Seed Data Script");
            sb.AppendLine("-- Target Engine: Microsoft SQL Server (2019+ / Azure SQL / LocalDB / Express)");
            sb.AppendLine(string.Format("-- Generated On: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
            sb.AppendLine("-- Description: Complete Self-Contained Schema, Tables, Constraints, Stored Procedures & Seed Data");
            sb.AppendLine("-- =============================================================================");
            sb.AppendLine();
            sb.AppendLine("SET ANSI_NULLS ON;");
            sb.AppendLine("SET QUOTED_IDENTIFIER ON;");
            sb.AppendLine("SET NOCOUNT ON;");
            sb.AppendLine("GO");
            sb.AppendLine();

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // 1. Get Tables
                var tables = new List<string>();
                using (var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }

                Console.WriteLine("Found {0} tables. Generating schema...", tables.Count);

                // 2. Generate CREATE TABLE statements
                foreach (var tbl in tables)
                {
                    sb.AppendLine("-- -----------------------------------------------------------------------------");
                    sb.AppendLine(string.Format("-- Table: [{0}]", tbl));
                    sb.AppendLine("-- -----------------------------------------------------------------------------");
                    sb.AppendLine(string.Format("IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = '{0}' AND s.name = 'dbo')", tbl));
                    sb.AppendLine("BEGIN");
                    sb.AppendLine(string.Format("    CREATE TABLE [dbo].[{0}] (", tbl));

                    var colList = new List<string>();
                    string colSql = @"
                        SELECT 
                            c.COLUMN_NAME, 
                            c.DATA_TYPE, 
                            c.CHARACTER_MAXIMUM_LENGTH, 
                            c.NUMERIC_PRECISION, 
                            c.NUMERIC_SCALE, 
                            c.IS_NULLABLE, 
                            c.COLUMN_DEFAULT,
                            COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity
                        FROM INFORMATION_SCHEMA.COLUMNS c
                        WHERE c.TABLE_NAME = @Tbl
                        ORDER BY c.ORDINAL_POSITION";

                    using (var cmd = new SqlCommand(colSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Tbl", tbl);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string colName = reader.GetString(0);
                                string dataType = reader.GetString(1);
                                int maxLen = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                byte precision = reader.IsDBNull(3) ? (byte)0 : reader.GetByte(3);
                                int scale = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                                string isNullable = reader.GetString(5);
                                string defaultVal = reader.IsDBNull(6) ? null : reader.GetString(6);
                                int isIdentity = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);

                                string typeDef = dataType.ToUpper();
                                if (dataType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) || 
                                    dataType.Equals("varchar", StringComparison.OrdinalIgnoreCase) ||
                                    dataType.Equals("char", StringComparison.OrdinalIgnoreCase) ||
                                    dataType.Equals("nchar", StringComparison.OrdinalIgnoreCase))
                                {
                                    typeDef += maxLen == -1 ? "(MAX)" : string.Format("({0})", maxLen);
                                }
                                else if (dataType.Equals("decimal", StringComparison.OrdinalIgnoreCase) || 
                                         dataType.Equals("numeric", StringComparison.OrdinalIgnoreCase))
                                {
                                    typeDef += string.Format("({0}, {1})", precision, scale);
                                }
                                else if (dataType.Equals("varbinary", StringComparison.OrdinalIgnoreCase))
                                {
                                    typeDef += maxLen == -1 ? "(MAX)" : string.Format("({0})", maxLen);
                                }

                                string identityDef = isIdentity == 1 ? " IDENTITY(1,1)" : "";
                                string nullDef = isNullable.Equals("YES", StringComparison.OrdinalIgnoreCase) ? "NULL" : "NOT NULL";
                                string defClause = string.IsNullOrWhiteSpace(defaultVal) ? "" : string.Format(" DEFAULT {0}", defaultVal);

                                colList.Add(string.Format("        [{0}] {1}{2} {3}{4}", colName, typeDef, identityDef, nullDef, defClause));
                            }
                        }
                    }

                    sb.AppendLine(string.Join(",\n", colList.ToArray()));
                    sb.AppendLine("    );");
                    sb.AppendLine("END;");
                    sb.AppendLine("GO");
                    sb.AppendLine();
                }

                // 3. Generate Primary Keys
                sb.AppendLine("-- =============================================================================");
                sb.AppendLine("-- PRIMARY KEY CONSTRAINTS");
                sb.AppendLine("-- =============================================================================");
                foreach (var tbl in tables)
                {
                    string pkSql = @"
                        SELECT kc.name AS PK_Name, c.name AS Column_Name
                        FROM sys.key_constraints kc
                        JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
                        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                        WHERE kc.type = 'PK' AND kc.parent_object_id = OBJECT_ID(@Tbl)
                        ORDER BY ic.key_ordinal";

                    var pkCols = new List<string>();
                    string pkName = "";
                    using (var cmd = new SqlCommand(pkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Tbl", "dbo." + tbl);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                pkName = reader.GetString(0);
                                pkCols.Add(string.Format("[{0}]", reader.GetString(1)));
                            }
                        }
                    }

                    if (pkCols.Count > 0)
                    {
                        sb.AppendLine(string.Format("IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = '{0}')", pkName));
                        sb.AppendLine("BEGIN");
                        sb.AppendLine(string.Format("    ALTER TABLE [dbo].[{0}] ADD CONSTRAINT [{1}] PRIMARY KEY CLUSTERED ({2});", tbl, pkName, string.Join(", ", pkCols.ToArray())));
                        sb.AppendLine("END;");
                        sb.AppendLine("GO");
                    }
                }
                sb.AppendLine();

                // 4. Generate Foreign Keys
                sb.AppendLine("-- =============================================================================");
                sb.AppendLine("-- FOREIGN KEY CONSTRAINTS");
                sb.AppendLine("-- =============================================================================");
                string fkSql = @"
                    SELECT 
                        fk.name AS FK_Name,
                        tp.name AS ParentTable,
                        cp.name AS ParentColumn,
                        tr.name AS ReferencedTable,
                        cr.name AS ReferencedColumn
                    FROM sys.foreign_keys fk
                    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
                    JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
                    JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
                    JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id";

                using (var cmd = new SqlCommand(fkSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string fkName = reader.GetString(0);
                        string parentTable = reader.GetString(1);
                        string parentCol = reader.GetString(2);
                        string refTable = reader.GetString(3);
                        string refCol = reader.GetString(4);

                        sb.AppendLine(string.Format("IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{0}')", fkName));
                        sb.AppendLine("BEGIN");
                        sb.AppendLine(string.Format("    ALTER TABLE [dbo].[{0}] WITH CHECK ADD CONSTRAINT [{1}] FOREIGN KEY([{2}]) REFERENCES [dbo].[{3}] ([{4}]);", parentTable, fkName, parentCol, refTable, refCol));
                        sb.AppendLine("END;");
                        sb.AppendLine("GO");
                    }
                }
                sb.AppendLine();

                // 5. Stored Procedures
                sb.AppendLine("-- =============================================================================");
                sb.AppendLine("-- STORED PROCEDURES & ROUTINES");
                sb.AppendLine("-- =============================================================================");
                string spSql = @"
                    SELECT m.definition
                    FROM sys.sql_modules m
                    JOIN sys.objects o ON m.object_id = o.object_id
                    WHERE o.type = 'P' AND o.is_ms_shipped = 0";

                using (var cmd = new SqlCommand(spSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string def = reader.GetString(0).Trim();
                        sb.AppendLine(def);
                        sb.AppendLine("GO");
                        sb.AppendLine();
                    }
                }

                // 6. Seed Data for Critical Masters
                sb.AppendLine("-- =============================================================================");
                sb.AppendLine("-- 🌿 SEED DATA: CORE SYSTEM & DIVISION MASTERS");
                sb.AppendLine("-- =============================================================================");
                var seedTables = new[] { "ROLE_MASTER", "DIVISION_MASTER", "USERS", "CASE_STATUS_MASTER", "MACT_MASTER" };
                foreach (var st in seedTables)
                {
                    Console.WriteLine("Extracting seed data for {0}...", st);
                    sb.AppendLine(string.Format("-- Seed Data: [{0}]", st));
                    string selectSql = string.Format("SELECT * FROM [dbo].[{0}]", st);
                    using (var cmd = new SqlCommand(selectSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var dt = new DataTable();
                        dt.Load(reader);

                        if (dt.Rows.Count > 0)
                        {
                            bool hasIdentity = false;
                            string idCheck = string.Format("SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.{0}') AND is_identity = 1", st);
                            using (var idCmd = new SqlCommand(idCheck, conn))
                            {
                                hasIdentity = idCmd.ExecuteScalar() != null;
                            }

                            if (hasIdentity)
                                sb.AppendLine(string.Format("SET IDENTITY_INSERT [dbo].[{0}] ON;", st));

                            var colNames = new List<string>();
                            foreach (DataColumn col in dt.Columns)
                            {
                                colNames.Add(string.Format("[{0}]", col.ColumnName));
                            }
                            string colsStr = string.Join(", ", colNames.ToArray());

                            foreach (DataRow row in dt.Rows)
                            {
                                var vals = new List<string>();
                                foreach (DataColumn col in dt.Columns)
                                {
                                    object v = row[col];
                                    if (v == DBNull.Value || v == null)
                                    {
                                        vals.Add("NULL");
                                    }
                                    else if (col.DataType == typeof(bool))
                                    {
                                        vals.Add((bool)v ? "1" : "0");
                                    }
                                    else if (col.DataType == typeof(DateTime))
                                    {
                                        vals.Add(string.Format("'{0:yyyy-MM-dd HH:mm:ss.fff}'", (DateTime)v));
                                    }
                                    else if (col.DataType == typeof(int) || col.DataType == typeof(long) || col.DataType == typeof(short) || col.DataType == typeof(byte) || col.DataType == typeof(decimal) || col.DataType == typeof(double))
                                    {
                                        vals.Add(v.ToString());
                                    }
                                    else
                                    {
                                        vals.Add("N'" + v.ToString().Replace("'", "''") + "'");
                                    }
                                }

                                string pkCol = dt.Columns[0].ColumnName;
                                object pkVal = row[0];
                                string pkCheck = (pkVal is string || pkVal is DateTime) ? string.Format("N'{0}'", pkVal.ToString().Replace("'", "''")) : pkVal.ToString();

                                sb.AppendLine(string.Format("IF NOT EXISTS (SELECT 1 FROM [dbo].[{0}] WHERE [{1}] = {2})", st, pkCol, pkCheck));
                                sb.AppendLine(string.Format("    INSERT INTO [dbo].[{0}] ({1}) VALUES ({2});", st, colsStr, string.Join(", ", vals.ToArray())));
                            }

                            if (hasIdentity)
                                sb.AppendLine(string.Format("SET IDENTITY_INSERT [dbo].[{0}] OFF;", st));

                            sb.AppendLine("GO");
                            sb.AppendLine();
                        }
                    }
                }

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
                Console.WriteLine("SUCCESS: Master script written to {0} (Size: {1:N0} bytes)", outputPath, new FileInfo(outputPath).Length);
            }
        }
    }
}
