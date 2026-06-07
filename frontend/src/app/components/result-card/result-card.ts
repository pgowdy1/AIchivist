import { Component, computed, inject, Input, signal } from '@angular/core';
import { CollectionResult, RelatedCollection } from '../../models/search.models';
import { SearchService } from '../../services/search';
import { FavoritesService } from '../../services/favorites';

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
  private readonly favoritesService = inject(FavoritesService);

  expanded = signal(false);

  relatedCollections = signal<RelatedCollection[]>([]);
  loadingRelated = signal(false);
  relatedLoaded = signal(false);

  isSaved = computed(() => this.favoritesService.isSaved(this.result.collectionUnitId));

  get scoreWidth(): string {
    return `${(this.result.relevanceScore / 10) * 100}%`;
  }

  get scoreColor(): string {
    const score = this.result.relevanceScore;
    if (score >= 8) return '#388e3c';
    if (score >= 5) return '#f57c00';
    return '#d32f2f';
  }

  toggleFavorite(): void {
    this.favoritesService.toggle({
      collectionUnitId: this.result.collectionUnitId,
      title: this.result.title,
      repository: this.result.repository,
      dateRange: this.result.dateRange,
    });
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
