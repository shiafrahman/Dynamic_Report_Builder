import { Component, ElementRef, OnInit } from '@angular/core';
import { ReportMetadataDto, ReportTemplateDto } from '../../models/report-template.model';
import { ReportService } from '../../services/report.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DynamicFiltersComponent } from '../dynamic-filters/dynamic-filters.component';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';


@Component({
  selector: 'app-report-viewer',
  standalone: true,
  imports: [CommonModule,FormsModule ,DynamicFiltersComponent ,HttpClientModule,NgChartsModule ],
  templateUrl: './report-viewer.component.html',
  styleUrl: './report-viewer.component.css'
})
export class ReportViewerComponent implements OnInit {
  templates: ReportTemplateDto[] = [];
  selectedTemplateId: number = 0;
  metadata: ReportMetadataDto = { fields: [], filters: [] };
  reportData: any[] = [];
  groupedReportData: any[] = [];

  groupableFields: string[] = [];
  numericFields: string[] = [];
  selectedGroupByField: string = '';
  selectedAggregateFields: string[] = [];
  selectedAggregateFunction: string = 'SUM';
  showGroupOptions: boolean = false;
  isGroupedReport: boolean = false;
  showSubtotals: boolean = false;

   // Column visibility properties
   showColumnOptions: boolean = false;
   visibleColumns: { [key: string]: boolean } = {};
   allColumnsSelected: boolean = true;

   // Chart properties
   showChartOptions: boolean = false;
   showChart: boolean = false;
   chartType: ChartType = 'bar';
   chartLabels: string[] = [];
   chartDatasets: ChartData<'bar'>['datasets'] = [];
   chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top',
      },
      title: {
        display: true,
        text: 'Report Chart'
      }
    },
    scales: {
      y: {
        beginAtZero: true
      }
    }
  };

  // Chart configuration
  chartLabelField: string = '';
  chartDataFields: string[] = [];
  chartTitle: string = 'Report Chart';




  constructor(private reportService: ReportService,private http: HttpClient,private elementRef: ElementRef) {}

  ngOnInit() {
    this.reportService.getTemplates().subscribe(res => this.templates = res);
  }


  // onTemplateChange() {
  //   this.reportService.getMetadata(this.selectedTemplateId).subscribe(res => this.metadata = res);
  // }

  onTemplateChange() {
    debugger;
  this.reportService.getMetadata(this.selectedTemplateId).subscribe(res => {
    this.metadata = res;
    this.metadata.filters.forEach(filter => {
      if (filter.type === 'dropdown') {
        this.reportService.getFilterOptions(this.selectedTemplateId, filter.key).subscribe(opts => {
          filter.options = opts.map(o => ({ label: o, value: o }));
        });
      }
    });

    // Load group metadata
    this.reportService.getGroupMetadata(this.selectedTemplateId).subscribe(groupMeta => {
      this.groupableFields = groupMeta.groupableFields;
      this.numericFields = groupMeta.numericFields;
    });

  });
}

toggleGroupOptions() {
  this.showGroupOptions = !this.showGroupOptions;
}

// generateGroupedReport() {
//   this.isGroupedReport = true; // Set to grouped report mode
//   const filters: { [key: string]: any } = {};
//   this.metadata.filters.forEach(f => filters[f.key] = f.value);
  
//   this.reportService.generateGroupedReport(
//     this.selectedTemplateId,
//     filters,
//     this.selectedGroupByField,
//     this.selectedAggregateFields,
//     this.selectedAggregateFunction
//   ).subscribe(res => {
//     this.groupedReportData = res;
//     this.reportData = res; // Keep for backward compatibility
    
//     // Update metadata fields to include aggregated fields
//     if (res.length > 0) {
//       const fields = Object.keys(res[0]);
//       //this.metadata.fields = Object.keys(res[0]);
//       this.metadata.fields = fields;
//       this.initializeColumnVisibility(fields);
//     } else {
//       this.metadata.fields = [];
//     }
//   });
// }

getGroupedData(): any[] {
  const groups = new Map<string, any[]>();
  
  this.groupedReportData.forEach(row => {
    const groupKey = row[this.selectedGroupByField];
    let groupArray = groups.get(groupKey);
    
    if (!groupArray) {
      groupArray = [];
      groups.set(groupKey, groupArray);
    }
    
    groupArray.push(row);
  });
  
  return Array.from(groups.entries()).map(([key, rows]) => ({
    key,
    rows
  }));
}

getSubtotal(group: any, field: string): number {
  if (field === this.selectedGroupByField) return 0;
  
  if (this.selectedAggregateFields.length > 0) {
    const aggregateField = this.selectedAggregateFields.find(f => 
      field === `${f}_${this.selectedAggregateFunction.toLowerCase()}`
    );
    
    if (aggregateField) {
      return group.rows.reduce((sum: number, row: any) => sum + (row[field] || 0), 0);
    }
  } else if (field === 'RecordCount') {
    return group.rows.length;
  }
  
  return 0;
}


  // generateReport() {
  //   debugger;
  //   this.isGroupedReport = false; // Reset to regular report mode
  //   const filters: { [key: string]: any } = {};
  //   this.metadata.filters.forEach(f => filters[f.key] = f.value);
  //   //this.reportService.generateReport(this.selectedTemplateId, filters).subscribe(res => this.reportData = res);
  //   this.reportService.generateReport(this.selectedTemplateId, filters).subscribe(res => {
  //     if (res.length > 0) {
  //       this.metadata.fields = Object.keys(res[0]); // Ensure exact key match
  //       this.initializeColumnVisibility(this.metadata.fields);
  //     } else {
  //       this.metadata.fields = [];
  //     }
  //     this.reportData = res;
  //     this.groupedReportData = []; // Clear grouped data
  //   });
  // }

  // exportExcel() {
  //   const filters: { [key: string]: any } = {};
  //   this.metadata.filters.forEach(f => filters[f.key] = f.value);
  //   this.reportService.exportToExcel(this.selectedTemplateId, filters).subscribe(blob => {
  //     const url = window.URL.createObjectURL(blob);
  //     const a = document.createElement('a');
  //     a.href = url;
  //     a.download = 'report.xlsx';
  //     a.click();
  //   });
  // }


  generateReport() {
    this.isGroupedReport = false; // Reset to regular report mode
    const filters: { [key: string]: any } = {};
    this.metadata.filters.forEach(f => filters[f.key] = f.value);
    
    // Get visible columns
    const visibleColumns = this.getVisibleColumns();
    
    this.reportService.generateReport(this.selectedTemplateId, filters, visibleColumns).subscribe(res => {
      if (res.length > 0) {
        this.metadata.fields = Object.keys(res[0]); // Ensure exact key match
        this.initializeColumnVisibility(this.metadata.fields);
      } else {
        this.metadata.fields = [];
      }
      this.reportData = res;
      this.groupedReportData = []; // Clear grouped data
    });
  }

  generateGroupedReport() {
    this.isGroupedReport = true; // Set to grouped report mode
    const filters: { [key: string]: any } = {};
    this.metadata.filters.forEach(f => filters[f.key] = f.value);
    
    // Get visible columns
    const visibleColumns = this.getVisibleColumns();
    
    this.reportService.generateGroupedReport(
      this.selectedTemplateId,
      filters,
      this.selectedGroupByField,
      this.selectedAggregateFields,
      this.selectedAggregateFunction,
      visibleColumns
    ).subscribe(res => {
      this.groupedReportData = res;
      this.reportData = res; // Keep for backward compatibility
      
      // Update metadata fields to include aggregated fields
      if (res.length > 0) {
        const fields = Object.keys(res[0]);
        this.metadata.fields = fields;
        this.initializeColumnVisibility(fields);
      } else {
        this.metadata.fields = [];
      }
    });
  }

  exportExcel() {
    const filters: { [key: string]: any } = {};
    this.metadata.filters.forEach(f => filters[f.key] = f.value);
    
    // Get visible columns
    const visibleColumns = this.getVisibleColumns();
    
    this.reportService.exportToExcel(this.selectedTemplateId, filters, visibleColumns).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'report.xlsx';
      a.click();
    });
  }

//   exportPdf() {
//   const filters: { [key: string]: any } = {};
//   this.metadata.filters.forEach(f => filters[f.key] = f.value);
//   this.reportService.exportToPdf(this.selectedTemplateId, filters).subscribe(blob => {
//     const url = window.URL.createObjectURL(blob);
//     const a = document.createElement('a');
//     a.href = url;
//     a.download = 'report.pdf';
//     a.click();
//   });
// }

exportPdf() {
  const filters: { [key: string]: any } = {};
  this.metadata.filters.forEach(f => filters[f.key] = f.value);
  
  // Get visible columns
  const visibleColumns = this.getVisibleColumns();
  
  this.reportService.exportToPdf(this.selectedTemplateId, filters, visibleColumns).subscribe(blob => {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'report.pdf';
    a.click();
  });
}

// Helper method to get the correct headers based on report type
getReportHeaders(): string[] {
  if (this.isGroupedReport) {
    return this.getGroupedReportHeaders();
  } else {
    return this.metadata.fields;
  }
}

getGroupedReportHeaders(): string[] {
  if (this.groupedReportData.length === 0) return [];
  
  const headers = new Set<string>();
  
  // Add group by field
  headers.add(this.selectedGroupByField);
  
  // Add aggregate fields
  if (this.selectedAggregateFields.length > 0) {
    this.selectedAggregateFields.forEach(field => {
      headers.add(`${field}_${this.selectedAggregateFunction.toLowerCase()}`);
    });
  } else {
    headers.add('RecordCount');
  }
  
  return Array.from(headers);
}

// Initialize column visibility
initializeColumnVisibility(fields: string[]) {
  // If we already have visibility settings, preserve them for fields that still exist
  const existingVisibility = { ...this.visibleColumns };
  this.visibleColumns = {};
  
  fields.forEach(field => {
    // If we had a setting for this field before, keep it; otherwise, default to visible
    this.visibleColumns[field] = existingVisibility.hasOwnProperty(field) ? existingVisibility[field] : true;
  });
  
  this.updateAllColumnsSelected();
}

 // Toggle column options dropdown
 toggleColumnOptions() {
  this.showColumnOptions = !this.showColumnOptions;
}

// Toggle visibility of a specific column
toggleColumnVisibility(field: string) {
  this.visibleColumns[field] = !this.visibleColumns[field];
  this.updateAllColumnsSelected();
}

// Toggle all columns visibility
toggleAllColumns() {
  const newState = !this.allColumnsSelected;
  this.allColumnsSelected = newState;
  
  Object.keys(this.visibleColumns).forEach(field => {
    this.visibleColumns[field] = newState;
  });
}

// Update the "all columns selected" state
updateAllColumnsSelected() {
  const visibleFields = Object.values(this.visibleColumns);
  this.allColumnsSelected = visibleFields.length > 0 && visibleFields.every(v => v);
}

// Check if a column should be visible
isColumnVisible(field: string): boolean {
  return this.visibleColumns[field] !== false;
}

// Get visible columns
getVisibleColumns(): string[] {
  return this.getReportHeaders().filter(field => this.isColumnVisible(field));
}

// Toggle chart options panel
toggleChartOptions() {
  this.showChartOptions = !this.showChartOptions;
  if (this.showChartOptions && this.reportData.length > 0) {
    // Initialize chart fields if not already set
    if (!this.chartLabelField && this.getReportHeaders().length > 0) {
      this.chartLabelField = this.getReportHeaders()[0];
      this.chartDataFields = this.getReportHeaders().slice(1, 3); // Select first 2 data fields by default
    }
  }
}

 // Generate chart based on current data and settings
 generateChart() {
  if (!this.chartLabelField || this.chartDataFields.length === 0) {
    alert('Please select both a label field and at least one data field.');
    return;
  }

  this.showChart = true;
  
  // Extract labels and data from report data
  const labels = new Set<string>();
  const datasets: any[] = [];
  
  // Prepare datasets for each selected data field
  this.chartDataFields.forEach((field, index) => {
    const data: number[] = [];
    const backgroundColors = [
      'rgba(54, 162, 235, 0.5)',  // Blue
      'rgba(255, 99, 132, 0.5)',  // Red
      'rgba(75, 192, 192, 0.5)',  // Teal
      'rgba(255, 206, 86, 0.5)',  // Yellow
      'rgba(153, 102, 255, 0.5)', // Purple
    ];
    
    const borderColors = [
      'rgba(54, 162, 235, 1)',
      'rgba(255, 99, 132, 1)',
      'rgba(75, 192, 192, 1)',
      'rgba(255, 206, 86, 1)',
      'rgba(153, 102, 255, 1)',
    ];
    
    // Collect data for this field
    this.reportData.forEach(row => {
      const label = row[this.chartLabelField];
      labels.add(label);
      
      // Convert to number if possible, otherwise use 0
      const value = parseFloat(row[field]) || 0;
      data.push(value);
    });
    
    datasets.push({
      label: field,
      data: data,
      backgroundColor: backgroundColors[index % backgroundColors.length],
      borderColor: borderColors[index % borderColors.length],
      borderWidth: 1
    });
  });
  
  // Set chart labels
  this.chartLabels = Array.from(labels);
  
  // Set chart datasets
  this.chartDatasets = datasets;
  
  // Safely update chart title
  // if (this.chartOptions.plugins && this.chartOptions.plugins.title) {
  //   this.chartOptions.plugins.title.text = this.chartTitle;
  // }
}

// Change chart type
changeChartType(type: ChartType) {
  this.chartType = type;
  if (this.showChart) {
    this.generateChart();
  }
}



}
