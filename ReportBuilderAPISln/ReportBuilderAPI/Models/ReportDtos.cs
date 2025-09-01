namespace ReportBuilderAPI.Models
{
    public class ReportDtos
    {
    }
    public class ReportRequestDto
    {
        public int TemplateId { get; set; }
        public Dictionary<string, string> Filters { get; set; }
        public string? GroupByField { get; set; }
        public List<string> AggregateFields { get; set; } = new List<string>();
        public string AggregateFunction { get; set; } = "SUM"; // SUM, COUNT, AVG, MIN, MAX

        public List<string> VisibleColumns { get; set; }
    }

    public class DropdownOptionRequestDto
    {
        public int TemplateId { get; set; }
        public string FilterKey { get; set; }
    }


    public class ReportTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ReportMetadataDto
    {
        public List<string> Fields { get; set; }
        public List<FilterDefinition> Filters { get; set; }
    }

    public class FilterDefinition
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public List<string>? Options { get; set; }
    }

    public class SaveTemplateDto
    {
        public string Name { get; set; }
        public string SqlQuery { get; set; }
        public List<string> Fields { get; set; }
        public List<FilterDefinition> Filters { get; set; }
    }

    public class ReportTemplateFullDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SqlQuery { get; set; } = string.Empty;
        public List<string> Fields { get; set; } = new List<string>();
        public List<FilterDefinition> Filters { get; set; } = new List<FilterDefinition>();
    }

    public class SqlQueryAnalysisRequestDto
    {
        public string SqlQuery { get; set; }
    }

    public class SqlQueryAnalysisResponseDto
    {
        public List<string> Fields { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> FieldTypes { get; set; }
    }




}
