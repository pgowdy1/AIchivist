import { Component, computed, effect, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SearchBar } from '../search-bar/search-bar';
import { ResultsPanel } from '../results-panel/results-panel';
import { ChatSidebar } from '../chat-sidebar/chat-sidebar';
import { SettingsDialog } from '../settings-dialog/settings-dialog';
import { SearchProgress } from '../search-progress/search-progress';
import { SearchService } from '../../services/search';
import { SearchHubService } from '../../services/search-hub';
import { SearchResponse } from '../../models/search.models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-home',
  imports: [SearchBar, ResultsPanel, ChatSidebar, SettingsDialog, SearchProgress],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  isMockMode = environment.useMockApi;
  loading = signal(false);
  searchResponse = signal<SearchResponse | null>(null);
  error = signal('');
  showSettings = signal(false);
  searchProgress = computed(() => this.hubService.progress());

  private usingSignalR = false;

  constructor(
    private searchService: SearchService,
    private hubService: SearchHubService,
    private router: Router,
  ) {
    effect(() => {
      const result = this.hubService.searchResult();
      const err = this.hubService.searchError();
      if (!this.usingSignalR) return;

      if (result) {
        this.searchResponse.set(result);
        this.loading.set(false);
      } else if (err) {
        this.error.set(err);
        this.loading.set(false);
      }
    });
  }

  async onSearch(query: string): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    this.searchResponse.set(null);
    this.usingSignalR = false;

    const ok = await this.hubService.search(query);
    if (ok) {
      this.usingSignalR = true;
      return;
    }

    // Fallback to HTTP POST
    this.searchService.search(query).subscribe({
      next: (res) => {
        this.searchResponse.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        if (err.status === 503) {
          this.router.navigateByUrl('/setup');
          return;
        }
        this.error.set('Search failed. Make sure the backend is running.');
        this.loading.set(false);
      },
    });
  }
}
