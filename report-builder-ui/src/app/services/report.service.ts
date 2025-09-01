import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { ReportMetadataDto, ReportTemplateDto } from "../models/report-template.model";
import { Observable } from "rxjs";

@Injectable({ providedIn: 'root' })
export class ReportService {

  private baseUrl = 'http://localhost:5260/api/report';
  constructor(private http: HttpClient) {}

  getTemplates(): Observable<ReportTemplateDto[]> {
    return this.http.get<ReportTemplateDto[]>(`${this.baseUrl}/templates`);
  }

  getMetadata(templateId: number): Observable<ReportMetadataDto> {
    return this.http.get<ReportMetadataDto>(`${this.baseUrl}/${templateId}/metadata`);
  }

  // generateReport(templateId: number, filters: any): Observable<any[]> {
  //   return this.http.post<any[]>(`${this.baseUrl}/generate`, {
  //     templateId,
  //     filters
  //   });
  // }

  // exportToExcel(templateId: number, filters: any,visibleColumns?: string[]): Observable<Blob> {
  //   return this.http.post(`${this.baseUrl}/export/excel`, {
  //     templateId,
  //     filters
  //   }, { responseType: 'blob' });
  // }

  generateReport(templateId: number, filters: any, visibleColumns?: string[]): Observable<any[]> {
    return this.http.post<any[]>(`${this.baseUrl}/generate`, {
      templateId,
      filters,
      visibleColumns
    });
  }

  exportToExcel(templateId: number, filters: any, visibleColumns?: string[]): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/export/excel`, {
      templateId,
      filters,
      visibleColumns
    }, { responseType: 'blob' });
  }

//   exportToPdf(templateId: number, filters: any): Observable<Blob> {
//   return this.http.post(`${this.baseUrl}/export/pdf`, {
//     templateId,
//     filters
//   }, { responseType: 'blob' });
// }

exportToPdf(templateId: number, filters: any, visibleColumns?: string[]): Observable<Blob> {
  return this.http.post(`${this.baseUrl}/export/pdf`, {
    templateId,
    filters,
    visibleColumns
  }, { responseType: 'blob' });
}

getFilterOptions(templateId: number, filterKey: string): Observable<string[]> {
  return this.http.post<string[]>(`${this.baseUrl}/filters/options`, {
    templateId,
    filterKey
  });
}

analyzeSqlQuery(sqlQuery: string): Observable<string[]> {
  return this.http.post<string[]>(`${this.baseUrl}/analyze-query`, {
    sqlQuery: sqlQuery
  });
}

getGroupMetadata(templateId: number): Observable<any> {
  return this.http.get<any>(`${this.baseUrl}/${templateId}/group-metadata`);
}

// generateGroupedReport(templateId: number, filters: any, groupBy: string, 
//                      aggregateFields: string[], aggregateFunction: string): Observable<any[]> {
//   return this.http.post<any[]>(`${this.baseUrl}/generate`, {
//     templateId,
//     filters,
//     groupByField: groupBy,
//     aggregateFields,
//     aggregateFunction
//   });
// }

generateGroupedReport(templateId: number, filters: any, groupBy: string, 
  aggregateFields: string[], aggregateFunction: string, visibleColumns?: string[]): Observable<any[]> {
return this.http.post<any[]>(`${this.baseUrl}/generate`, {
templateId,
filters,
groupByField: groupBy,
aggregateFields,
aggregateFunction,
visibleColumns
});
}

}
