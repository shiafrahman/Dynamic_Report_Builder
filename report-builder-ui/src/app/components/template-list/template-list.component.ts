import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { ReportTemplateDto } from '../../models/report-template.model';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';


@Component({
  selector: 'app-template-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './template-list.component.html',
  styleUrl: './template-list.component.css'
})
export class TemplateListComponent {
  templates: ReportTemplateDto[] = [];
  constructor(private http: HttpClient, private router: Router) {}

  ngOnInit() {
    this.loadTemplates();
  }

  loadTemplates() {
    this.http.get<ReportTemplateDto[]>('http://localhost:5260/api/report/templates')
      .subscribe(templates => this.templates = templates);
  }

  editTemplate(id: number) {
    this.router.navigate(['/templates/edit', id]);
  }

  createNewTemplate() {
    this.router.navigate(['/templates/new']);
  }

}
