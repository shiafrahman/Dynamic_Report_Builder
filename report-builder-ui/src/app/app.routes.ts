import { Routes } from '@angular/router';
import { ReportViewerComponent } from './components/report-viewer/report-viewer.component';
import { TemplateBuilderComponent } from './components/template-builder/template-builder.component';
import { LoginComponent } from './components/login/login.component';
import { TemplateListComponent } from './components/template-list/template-list.component';

export const routes: Routes = [
  { path: '', redirectTo: 'reports', pathMatch: 'full' },
  { path: 'reports', component: ReportViewerComponent },
   { path: 'templates', component: TemplateListComponent },
  { path: 'templates/new', component: TemplateBuilderComponent },
  { path: 'templates/edit/:id', component: TemplateBuilderComponent },
  { path: 'login', component: LoginComponent }
];
