export interface FilterDefinition {
  key: string;
  label: string;
  type: 'text' | 'date' | 'dropdown'| 'daterange';
  options?: { label: string; value: string }[];
  value?: any;
  
}

export interface ReportTemplateDto {
  id: number;
  name: string;
}

export interface ReportMetadataDto {
  fields: string[];
  filters: FilterDefinition[];
}