import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FilterDefinition } from '../../models/report-template.model';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';

@Component({
  selector: 'app-dynamic-filters',
  standalone: true,
  imports: [CommonModule,FormsModule,HttpClientModule ],
  templateUrl: './dynamic-filters.component.html',
  styleUrl: './dynamic-filters.component.css'
})
export class DynamicFiltersComponent {
@Input() filters: FilterDefinition[] = [];
}
