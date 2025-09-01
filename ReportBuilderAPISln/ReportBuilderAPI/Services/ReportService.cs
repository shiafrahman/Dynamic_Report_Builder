using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using ReportBuilderAPI.Models;
using System.Text;

namespace ReportBuilderAPI.Services
{
    public class ReportService
    {
        private readonly IConfiguration _config;
        private readonly SqlConnection _conn;

        public ReportService(IConfiguration config)
        {
            _config = config;
            _conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        }

        public List<ReportTemplateDto> GetReportTemplates()
        {
            var list = new List<ReportTemplateDto>();
            var cmd = new SqlCommand("SELECT Id, Name FROM ReportTemplates", _conn);
            _conn.Open();
            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ReportTemplateDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
            _conn.Close();
            return list;
        }

        public ReportMetadataDto GetTemplateMetadata(int templateId)
        {
            var metadata = new ReportMetadataDto
            {
                Fields = new List<string>(),
                Filters = new List<FilterDefinition>()
            };

            var cmd = new SqlCommand("SELECT FieldName FROM ReportTemplateFields WHERE ReportTemplateId = @id", _conn);
            cmd.Parameters.AddWithValue("@id", templateId);
            _conn.Open();
            var reader = cmd.ExecuteReader();
            while (reader.Read())
                metadata.Fields.Add(reader.GetString(0));
            _conn.Close();

            var filterCmd = new SqlCommand("SELECT FilterKey, FilterType FROM ReportTemplateFilters WHERE ReportTemplateId = @id", _conn);
            filterCmd.Parameters.AddWithValue("@id", templateId);
            _conn.Open();
            var filterReader = filterCmd.ExecuteReader();
            while (filterReader.Read())
            {
                metadata.Filters.Add(new FilterDefinition
                {
                    Key = filterReader.GetString(0),
                    Label = filterReader.GetString(0),
                    Type = filterReader.GetString(1)
                });
            }
            _conn.Close();
            return metadata;
        }

        public List<Dictionary<string, object>> GenerateReport(ReportRequestDto request)
         {
            string sql = GetBaseQuery(request.TemplateId);
            if (!string.IsNullOrEmpty(request.GroupByField))
            {
                // Build the grouped query with proper structure
                sql = BuildGroupByQuery(sql, request);

                // Now handle the WHERE clause separately
                string whereClause = BuildWhereConditions(request.Filters);

                if (!string.IsNullOrEmpty(whereClause))
                {
                    // Check if the query already has a WHERE clause
                    if (sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // If WHERE exists, we need to add our conditions with AND
                        // Find the position after WHERE to insert our conditions
                        int whereIndex = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) + 5;
                        sql = sql.Insert(whereIndex, " " + whereClause + " AND ");
                    }
                    else
                    {
                        // If no WHERE exists, add it before GROUP BY
                        int groupByIndex = sql.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
                        if (groupByIndex >= 0)
                        {
                            sql = sql.Insert(groupByIndex, " WHERE " + whereClause + " ");
                        }
                    }
                }
            }
            else
            {
                // For non-grouped queries, just append the WHERE clause as before
                sql += BuildWhereClause(request.Filters);
            }
            //sql += BuildWhereClause(request.Filters);

            var cmd = new SqlCommand(sql, _conn);
            foreach (var f in request.Filters)
                cmd.Parameters.AddWithValue("@" + f.Key, f.Value);

            _conn.Open();
            var reader = cmd.ExecuteReader();
            var result = new List<Dictionary<string, object>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader[i];
                result.Add(row);
            }
             _conn.Close();
            return result;
        }

        private string BuildGroupByQuery(string baseSql, ReportRequestDto request)
        {
            if (string.IsNullOrEmpty(request.GroupByField))
                return baseSql;

            // Extract the main part of the query (before any WHERE clause)
            string baseSelectPart;
            string baseWherePart = "";

            var whereIndex = baseSql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
            if (whereIndex >= 0)
            {
                baseSelectPart = baseSql.Substring(0, whereIndex).Trim();
                baseWherePart = baseSql.Substring(whereIndex).Trim();
            }
            else
            {
                baseSelectPart = baseSql;
            }

            // Build the SELECT clause with grouping
            var selectFields = new List<string>();
            selectFields.Add(request.GroupByField);

            // Add aggregate fields
            if (request.AggregateFields != null && request.AggregateFields.Any())
            {
                foreach (var field in request.AggregateFields)
                {
                    selectFields.Add($"{request.AggregateFunction}({field}) AS {field}_{request.AggregateFunction.ToLower()}");
                }
            }
            else
            {
                // If no aggregate fields specified, count the records
                selectFields.Add($"COUNT(*) AS RecordCount");
            }

            //// Build the final query with proper structure
            //string finalSelect = $"SELECT {string.Join(", ", selectFields)}";
            //string fromClause = $"FROM ({baseSelectPart}) AS GroupedQuery";
            //string whereClause = !string.IsNullOrEmpty(baseWherePart) ? baseWherePart : "";
            //string groupByClause = $"GROUP BY {request.GroupByField}";

            //// Combine parts in correct order: SELECT -> FROM -> WHERE -> GROUP BY
            //string result = $"{finalSelect} {fromClause}";

            //if (!string.IsNullOrEmpty(whereClause))
            //{
            //    result += $" {whereClause}";
            //}

            //result += $" {groupByClause}";

            //return result;
            return $"SELECT {string.Join(", ", selectFields)} FROM ({baseSelectPart}) AS GroupedQuery GROUP BY {request.GroupByField}";
        }

        private string GetBaseQuery(int templateId)
        {
            var cmd = new SqlCommand("SELECT SqlQuery FROM ReportTemplateSources WHERE ReportTemplateId = @id", _conn);
            cmd.Parameters.AddWithValue("@id", templateId);
            _conn.Open();
            var sql = (string)cmd.ExecuteScalar();
            _conn.Close();
            return sql;
        }

        public async Task<List<string>> GetFilterOptions([FromBody] DropdownOptionRequestDto dto)
        {
            var options = new List<string>();
            var baseSql = GetBaseQuery(dto.TemplateId);

            
            var cleanSql = baseSql.Split("WHERE", StringSplitOptions.RemoveEmptyEntries)[0].Trim();

            var sql = $"SELECT DISTINCT {dto.FilterKey} FROM ({cleanSql}) AS base";

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    options.Add(reader.GetString(0));
                }
            }

             return options;
        }

        private string BuildWhereClause(Dictionary<string, string> filters)
        {
            var where = " WHERE 1=1";
            foreach (var kv in filters)
            {
                if (string.IsNullOrWhiteSpace(kv.Value))
                    continue;
                if (kv.Key == "DateFrom")
                {
                    where += " AND InvoiceDate >= @DateFrom";
                }
                else if (kv.Key == "DateTo")
                {
                    where += " AND InvoiceDate <= @DateTo";
                }
                else
                {
                    where += $" AND {kv.Key} = @{kv.Key}";
                }
                // where += $" AND {kv.Key} = @{kv.Key}";
            }
            return where;
        }

        private string BuildWhereConditions(Dictionary<string, string> filters)
        {
            var conditions = new List<string>();

            foreach (var kv in filters)
            {
                if (string.IsNullOrWhiteSpace(kv.Value))
                    continue;

                if (kv.Key == "DateFrom")
                {
                    conditions.Add("InvoiceDate >= @DateFrom");
                }
                else if (kv.Key == "DateTo")
                {
                    conditions.Add("InvoiceDate <= @DateTo");
                }
                else
                {
                    conditions.Add($"{kv.Key} = @{kv.Key}");
                }
            }

            return string.Join(" AND ", conditions);
        }

        //public MemoryStream ExportToExcel(ReportRequestDto request)
        //{
        //    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        //    var data = GenerateReport(request);
        //    var stream = new MemoryStream();
        //    using (var package = new ExcelPackage(stream))
        //    {
        //        var sheet = package.Workbook.Worksheets.Add("Report");
        //        if (data.Count > 0)
        //        {
        //            var headers = data[0].Keys.ToList();
        //            for (int i = 0; i < headers.Count; i++)
        //                sheet.Cells[1, i + 1].Value = headers[i];

        //            for (int r = 0; r < data.Count; r++)
        //            {
        //                for (int c = 0; c < headers.Count; c++)
        //                {
        //                    sheet.Cells[r + 2, c + 1].Value = data[r][headers[c]];
        //                }
        //            }
        //        }
        //        package.Save();
        //    }
        //    stream.Position = 0;
        //    return stream;
        //}

        //public MemoryStream ExportToExcel(ReportRequestDto request, List<string> visibleColumns = null)
        //{

        //    // Set the EPPlus license context
        //    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        //    var data = GenerateReport(request);
        //    var stream = new MemoryStream();

        //    using (var package = new ExcelPackage(stream))
        //    {
        //        var sheet = package.Workbook.Worksheets.Add("Report");

        //        if (data.Count > 0)
        //        {

        //            var headers = data[0].Keys.ToList();

        //            // Write headers
        //            for (int i = 0; i < headers.Count; i++)
        //            {
        //                sheet.Cells[1, i + 1].Value = headers[i];
        //                sheet.Cells[1, i + 1].Style.Font.Bold = true;
        //            }

        //            // Write data rows
        //            for (int r = 0; r < data.Count; r++)
        //            {
        //                for (int c = 0; c < headers.Count; c++)
        //                {
        //                    sheet.Cells[r + 2, c + 1].Value = data[r][headers[c]];
        //                }
        //            }

        //            // Auto-fit columns for better readability
        //            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        //        }

        //        package.Save();
        //    }

        //    stream.Position = 0;
        //    return stream;
        //}


        public MemoryStream ExportToExcel(ReportRequestDto request, List<string> visibleColumns = null)
        {
            // Set the EPPlus license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var data = GenerateReport(request);
            var stream = new MemoryStream();

            using (var package = new ExcelPackage(stream))
            {
                var sheet = package.Workbook.Worksheets.Add("Report");

                if (data.Count > 0)
                {
                    // If no visible columns specified, use all columns
                    if (visibleColumns == null || visibleColumns.Count == 0)
                    {
                        visibleColumns = data[0].Keys.ToList();
                    }

                    // Write headers for visible columns only
                    for (int i = 0; i < visibleColumns.Count; i++)
                    {
                        sheet.Cells[1, i + 1].Value = visibleColumns[i];
                        sheet.Cells[1, i + 1].Style.Font.Bold = true;
                    }

                    // Write data rows for visible columns only
                    for (int r = 0; r < data.Count; r++)
                    {
                        for (int c = 0; c < visibleColumns.Count; c++)
                        {
                            string columnName = visibleColumns[c];
                            if (data[r].ContainsKey(columnName))
                            {
                                sheet.Cells[r + 2, c + 1].Value = data[r][columnName];
                            }
                        }
                    }

                    // Auto-fit columns for better readability
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                }

                package.Save();
            }

            stream.Position = 0;
            return stream;
        }

        //public string GenerateHtmlReport(ReportRequestDto request)
        //{
        //    var data = GenerateReport(request);
        //    if (data == null || data.Count == 0)
        //        return "<h2>No data found for this report.</h2>";

        //    var html = new StringBuilder();
        //    html.Append("<html><head>");
        //    html.Append("<style>");
        //    html.Append("table { width: 100%; border-collapse: collapse; font-family: Arial; }");
        //    html.Append("th, td { border: 1px solid #ccc; padding: 8px; text-align: left; }");
        //    html.Append("th { background-color: #f2f2f2; }");
        //    html.Append("</style>");
        //    html.Append("</head><body>");
        //    html.Append("<h2>Dynamic Report</h2>");
        //    html.Append("<table>");

        //    // Headers
        //    html.Append("<thead><tr>");
        //    foreach (var key in data[0].Keys)
        //    {
        //        html.Append($"<th>{key}</th>");
        //    }
        //    html.Append("</tr></thead>");

        //    // Rows
        //    html.Append("<tbody>");
        //    foreach (var row in data)
        //    {
        //        html.Append("<tr>");
        //        foreach (var key in row.Keys)
        //        {
        //            html.Append($"<td>{row[key]}</td>");
        //        }
        //        html.Append("</tr>");
        //    }
        //    html.Append("</tbody>");

        //    html.Append("</table>");
        //    html.Append("</body></html>");

        //    return html.ToString();
        //}

        public string GenerateHtmlReport(ReportRequestDto request, List<string> visibleColumns = null)
        {
            var data = GenerateReport(request);
            if (data == null || data.Count == 0)
                return "<h2>No data found for this report.</h2>";

            // If no visible columns specified, use all columns
            if (visibleColumns == null || visibleColumns.Count == 0)
            {
                visibleColumns = data[0].Keys.ToList();
            }

            var html = new StringBuilder();
            html.Append("<html><head>");
            html.Append("<style>");
            html.Append("table { width: 100%; border-collapse: collapse; font-family: Arial; }");
            html.Append("th, td { border: 1px solid #ccc; padding: 8px; text-align: left; }");
            html.Append("th { background-color: #f2f2f2; }");
            html.Append("</style>");
            html.Append("</head><body>");
            html.Append("<h2>Dynamic Report</h2>");
            html.Append("<table>");

            // Headers
            html.Append("<thead><tr>");
            foreach (var key in visibleColumns)
            {
                html.Append($"<th>{key}</th>");
            }
            html.Append("</tr></thead>");

            // Rows
            html.Append("<tbody>");
            foreach (var row in data)
            {
                html.Append("<tr>");
                foreach (var key in visibleColumns)
                {
                    html.Append($"<td>{row[key]}</td>");
                }
                html.Append("</tr>");
            }
            html.Append("</tbody>");

            html.Append("</table>");
            html.Append("</body></html>");

            return html.ToString();
        }


        public async Task<bool> SaveTemplateAsync(SaveTemplateDto dto)
        {
            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                var transaction = conn.BeginTransaction();

                try
                {
                    // 1. Insert into ReportTemplates
                    string insertTemplateSql = @"
                INSERT INTO ReportTemplates (Name, Description, CreatedDate)
                OUTPUT INSERTED.Id
                VALUES (@Name, '', GETDATE())";

                    int templateId;
                    using (SqlCommand cmd = new SqlCommand(insertTemplateSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Name", dto.Name);
                        templateId = (int)await cmd.ExecuteScalarAsync();
                    }

                    // 2. Insert Fields
                    int sortOrder = 1;
                    foreach (var field in dto.Fields)
                    {
                        string insertFieldSql = @"
                    INSERT INTO ReportTemplateFields (ReportTemplateId, FieldName, DisplayName, SortOrder)
                    VALUES (@TemplateId, @FieldName, @DisplayName, @SortOrder)";
                        using (SqlCommand cmd = new SqlCommand(insertFieldSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@TemplateId", templateId);
                            cmd.Parameters.AddWithValue("@FieldName", field);
                            cmd.Parameters.AddWithValue("@DisplayName", field);
                            cmd.Parameters.AddWithValue("@SortOrder", sortOrder++);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 3. Insert Filters
                    foreach (var filter in dto.Filters)
                    {
                        string insertFilterSql = @"
                    INSERT INTO ReportTemplateFilters (ReportTemplateId, FilterKey, FilterType, FilterValue)
                    VALUES (@TemplateId, @FilterKey, @FilterType, '')";
                        using (SqlCommand cmd = new SqlCommand(insertFilterSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@TemplateId", templateId);
                            cmd.Parameters.AddWithValue("@FilterKey", filter.Key);
                            cmd.Parameters.AddWithValue("@FilterType", filter.Type);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 4. Insert SQL Query
                    string insertSourceSql = @"
                INSERT INTO ReportTemplateSources (ReportTemplateId, SqlQuery)
                VALUES (@TemplateId, @SqlQuery)";
                    using (SqlCommand cmd = new SqlCommand(insertSourceSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@TemplateId", templateId);
                        cmd.Parameters.AddWithValue("@SqlQuery", dto.SqlQuery);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }


        public ReportTemplateFullDto GetFullTemplate(int templateId)
        {
            var template = new ReportTemplateFullDto();

            // Get basic template info
            var cmd = new SqlCommand("SELECT Id, Name FROM ReportTemplates WHERE Id = @id", _conn);
            cmd.Parameters.AddWithValue("@id", templateId);
            _conn.Open();
            var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                template.Id = reader.GetInt32(0);
                template.Name = reader.GetString(1);
            }
            _conn.Close();

            // Get SQL query
            var sqlCmd = new SqlCommand("SELECT SqlQuery FROM ReportTemplateSources WHERE ReportTemplateId = @id", _conn);
            sqlCmd.Parameters.AddWithValue("@id", templateId);
            _conn.Open();
            template.SqlQuery = sqlCmd.ExecuteScalar()?.ToString() ?? string.Empty;
            _conn.Close();

            // Get fields
            var fieldsCmd = new SqlCommand("SELECT FieldName FROM ReportTemplateFields WHERE ReportTemplateId = @id ORDER BY SortOrder", _conn);
            fieldsCmd.Parameters.AddWithValue("@id", templateId);
            _conn.Open();
            var fieldsReader = fieldsCmd.ExecuteReader();
            template.Fields = new List<string>();
            while (fieldsReader.Read())
                template.Fields.Add(fieldsReader.GetString(0));
            _conn.Close();

            // Get filters
            var filtersCmd = new SqlCommand("SELECT FilterKey, FilterType FROM ReportTemplateFilters WHERE ReportTemplateId = @id", _conn);
            filtersCmd.Parameters.AddWithValue("@id", templateId);
            _conn.Open();
            var filtersReader = filtersCmd.ExecuteReader();
            template.Filters = new List<FilterDefinition>();
            while (filtersReader.Read())
            {
                template.Filters.Add(new FilterDefinition
                {
                    Key = filtersReader.GetString(0),
                    Type = filtersReader.GetString(1),
                    Label = filtersReader.GetString(0)
                });
            }
            _conn.Close();

            return template;
        }

        public async Task<bool> UpdateTemplateAsync(int templateId, SaveTemplateDto dto)
        {
            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                var transaction = conn.BeginTransaction();

                try
                {
                    // 1. Update ReportTemplates
                    string updateTemplateSql = @"
                UPDATE ReportTemplates 
                SET Name = @Name
                WHERE Id = @Id";

                    using (SqlCommand cmd = new SqlCommand(updateTemplateSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", templateId);
                        cmd.Parameters.AddWithValue("@Name", dto.Name);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Delete existing fields
                    string deleteFieldsSql = "DELETE FROM ReportTemplateFields WHERE ReportTemplateId = @TemplateId";
                    using (SqlCommand cmd = new SqlCommand(deleteFieldsSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@TemplateId", templateId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 3. Insert new fields
                    int sortOrder = 1;
                    foreach (var field in dto.Fields)
                    {
                        string insertFieldSql = @"
                    INSERT INTO ReportTemplateFields (ReportTemplateId, FieldName, DisplayName, SortOrder)
                    VALUES (@TemplateId, @FieldName, @DisplayName, @SortOrder)";
                        using (SqlCommand cmd = new SqlCommand(insertFieldSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@TemplateId", templateId);
                            cmd.Parameters.AddWithValue("@FieldName", field);
                            cmd.Parameters.AddWithValue("@DisplayName", field);
                            cmd.Parameters.AddWithValue("@SortOrder", sortOrder++);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 4. Delete existing filters
                    string deleteFiltersSql = "DELETE FROM ReportTemplateFilters WHERE ReportTemplateId = @TemplateId";
                    using (SqlCommand cmd = new SqlCommand(deleteFiltersSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@TemplateId", templateId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 5. Insert new filters
                    foreach (var filter in dto.Filters)
                    {
                        string insertFilterSql = @"
                    INSERT INTO ReportTemplateFilters (ReportTemplateId, FilterKey, FilterType, FilterValue)
                    VALUES (@TemplateId, @FilterKey, @FilterType, '')";
                        using (SqlCommand cmd = new SqlCommand(insertFilterSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@TemplateId", templateId);
                            cmd.Parameters.AddWithValue("@FilterKey", filter.Key);
                            cmd.Parameters.AddWithValue("@FilterType", filter.Type);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 6. Update SQL Query
                    string updateSourceSql = @"
                UPDATE ReportTemplateSources
                SET SqlQuery = @SqlQuery
                WHERE ReportTemplateId = @TemplateId";
                    using (SqlCommand cmd = new SqlCommand(updateSourceSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@TemplateId", templateId);
                        cmd.Parameters.AddWithValue("@SqlQuery", dto.SqlQuery);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }

        public SqlQueryAnalysisResponseDto AnalyzeSqlQuery(SqlQueryAnalysisRequestDto request)
        {
            var response = new SqlQueryAnalysisResponseDto();

            try
            {
                // Basic validation - ensure it's a SELECT query
                if (!request.SqlQuery.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    response.IsValid = false;
                    response.ErrorMessage = "Only SELECT queries are supported.";
                    return response;
                }

                // Modify the query to return only the structure (no data)
                var modifiedSql = $"SELECT TOP 1 * FROM ({request.SqlQuery}) AS temp";

                var cmd = new SqlCommand(modifiedSql, _conn);
                _conn.Open();
                var reader = cmd.ExecuteReader();

                var fields = new List<string>();
                var fieldTypes = new Dictionary<string, string>();
                var schemaTable = reader.GetSchemaTable();
                //for (int i = 0; i < reader.FieldCount; i++)
                //{
                //    fields.Add(reader.GetName(i));
                //}

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var fieldName = reader.GetName(i);
                    fields.Add(fieldName);

                    // Determine field type
                    var dataType = schemaTable.Rows[i]["DataType"].ToString();
                    if (dataType.Contains("Date") || dataType.Contains("Time"))
                        fieldTypes[fieldName] = "date";
                    else if (dataType.Contains("Int") || dataType.Contains("Decimal") || dataType.Contains("Float"))
                        fieldTypes[fieldName] = "number";
                    else
                        fieldTypes[fieldName] = "text";
                }

                _conn.Close();

                response.Fields = fields;
                response.FieldTypes = fieldTypes; // Add this to the DTO
                response.IsValid = true;
               
            }
            catch (Exception ex)
            {
                response.IsValid = false;
                response.ErrorMessage = $"Error analyzing query: {ex.Message}";
                if (_conn.State == System.Data.ConnectionState.Open)
                    _conn.Close();
            }

            return response;
        }


    }
}
