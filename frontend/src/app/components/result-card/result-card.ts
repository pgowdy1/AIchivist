import { Component, Input } from '@angular/core';
import { CollectionResult } from '../../models/search.models';

@Component({
  selector: 'app-result-card',
  imports: [],
  templateUrl: './result-card.html',
  styleUrl: './result-card.scss',
})
export class ResultCard {
  @Input() result!: CollectionResult;

  expanded = false;

  get scoreWidth(): string {
    return `${(this.result.relevanceScore / 10) * 100}%`;
  }

  get scoreColor(): string {
    const score = this.result.relevanceScore;
    if (score >= 8) return '#388e3c';
    if (score >= 5) return '#f57c00';
    return '#d32f2f';
  }

  toggleExpanded(): void {
    this.expanded = !this.expanded;
  }
}
