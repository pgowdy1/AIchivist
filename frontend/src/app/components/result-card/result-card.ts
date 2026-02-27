import { Component, inject, Input, signal } from '@angular/core';
import { CollectionResult, RelatedCollection } from '../../models/search.models';
import { SearchService } from '../../services/search';

@Component({
  selector: 'app-result-card',
  imports: [],
  templateUrl: './result-card.html',
  styleUrl: './result-card.scss',
})
export class ResultCard {
  @Input() result!: CollectionResult;
  @Input() unranked = false;

  private readonly searchService = inject(SearchService);

  expanded = signal(false);

  relatedCollections = signal<RelatedCollection[]>([]);
  loadingRelated = signal(false);
  relatedLoaded = signal(false);

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
    this.expanded.update(v => !v);
  }

  loadRelated(): void {
    if (this.relatedLoaded()) {
      return;
    }

    this.loadingRelated.set(true);

    this.searchService.getRelated(this.result.collectionUnitId).subscribe({
      next: (collections) => {
        this.relatedCollections.set(collections);
        this.relatedLoaded.set(true);
        this.loadingRelated.set(false);
      },
      error: () => {
        this.loadingRelated.set(false);
      },
    });
  }
}
