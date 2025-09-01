import { Component, OnInit } from '@angular/core';
import { FilterDefinition } from '../../models/report-template.model';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms'; // ✅ Add this
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ReportService } from '../../services/report.service';

@Component({
  selector: 'app-template-builder',
  standalone: true,
  imports: [HttpClientModule, FormsModule,CommonModule], // ✅ Include FormsModule here
  templateUrl: './template-builder.component.html',
  styleUrl: './template-builder.component.css'
})
export class TemplateBuilderComponent implements OnInit {
  templateId: number | null = null;
  templateName = '';
  selectedFields: string[] = [];
  filters: FilterDefinition[] = [];
  baseQuery = '';
  private queryChangeTimeout: any;
  isLoadingFields = false;
  
  constructor(private http: HttpClient,
              private route: ActivatedRoute,
              private reportService: ReportService) {}

  ngOnInit() {
    this.route.params.subscribe(params => {
      if (params['id']) {
        this.templateId = +params['id'];
        this.loadTemplate(this.templateId);
      }
    });
  }

  loadTemplate(id: number) {
    this.http.get<any>(`http://localhost:5260/api/report/templates/${id}/full`)
      .subscribe({
        next: (template) => {
          this.templateName = template.name;
          this.baseQuery = template.sqlQuery;
          this.selectedFields = template.fields;
          this.filters = template.filters;
        },
        error: (err) => console.error('Error loading template:', err)
      });
  }

  loadFieldsFromQuery() {
    if (!this.baseQuery.trim()) {
      alert('Please enter a SQL query first.');
      return;
    }

    this.isLoadingFields = true;
  
    this.reportService.analyzeSqlQuery(this.baseQuery).subscribe({
      next: (fields) => {
        this.selectedFields = fields;
        this.isLoadingFields = false;
        alert(`Successfully loaded ${fields.length} fields from the query.`);
      },
      error: (err) => {
        this.isLoadingFields = false;
        console.error('Error analyzing query:', err);
        alert(`Failed to analyze query: ${err.error || err.message}`);
      }
    });
  }

  onQueryChange() {
    clearTimeout(this.queryChangeTimeout);
    this.queryChangeTimeout = setTimeout(() => {
      if (this.baseQuery.trim()) {
        this.loadFieldsFromQuery();
      }
    }, 1000); // Wait 1 second after user stops typing
  }

  saveTemplate() {
    const body = {
      id: this.templateId,
      name: this.templateName,
      sqlQuery: this.baseQuery,
      fields: this.selectedFields,
      filters: this.filters
    };

    const request = this.templateId 
      ? this.http.put(`http://localhost:5260/api/report/templates/${this.templateId}`, body)
      : this.http.post('http://localhost:5260/api/report/templates/save', body);

      request.subscribe({
      next: () => alert(`Template ${this.templateId ? 'updated' : 'saved'} successfully!`),
      error: err => console.error('Save error:', err)
    });

    // this.http.post('http://localhost:5260/api/report/templates/save', body).subscribe({
    //   next: () => alert('Template saved successfully!'),
    //   error: err => console.error('Save error:', err)
    // });
  }
}
