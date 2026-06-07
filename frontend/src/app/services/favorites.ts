import { Injectable, signal, computed } from '@angular/core';
import { SavedCollection } from '../models/search.models';

@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private static readonly STORAGE_KEY = 'aichivist-favorites';

  readonly savedCollections = signal<SavedCollection[]>([]);
  readonly count = computed(() => this.savedCollections().length);

  constructor() {
    this.loadFromStorage();
  }

  add(collection: SavedCollection): void {
    const current = this.savedCollections();
    if (current.some(c => c.collectionUnitId === collection.collectionUnitId)) {
      return;
    }
    this.savedCollections.set([...current, collection]);
    this.saveToStorage();
  }

  remove(unitId: string): void {
    this.savedCollections.update(
      collections => collections.filter(c => c.collectionUnitId !== unitId)
    );
    this.saveToStorage();
  }

  toggle(collection: SavedCollection): void {
    if (this.isSaved(collection.collectionUnitId)) {
      this.remove(collection.collectionUnitId);
    } else {
      this.add(collection);
    }
  }

  isSaved(unitId: string): boolean {
    return this.savedCollections().some(c => c.collectionUnitId === unitId);
  }

  clear(): void {
    this.savedCollections.set([]);
    this.saveToStorage();
  }

  private loadFromStorage(): void {
    try {
      const stored = localStorage.getItem(FavoritesService.STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed)) {
          this.savedCollections.set(parsed);
        }
      }
    } catch {
      // localStorage unavailable or corrupt data — fall back to empty array
    }
  }

  private saveToStorage(): void {
    try {
      localStorage.setItem(
        FavoritesService.STORAGE_KEY,
        JSON.stringify(this.savedCollections())
      );
    } catch {
      // localStorage unavailable — in-memory only
    }
  }
}
