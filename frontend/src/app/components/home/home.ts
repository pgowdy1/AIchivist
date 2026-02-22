import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SearchBar } from '../search-bar/search-bar';
import { ResultsPanel } from '../results-panel/results-panel';
import { ChatSidebar } from '../chat-sidebar/chat-sidebar';
import { SettingsDialog } from '../settings-dialog/settings-dialog';
import { SearchService } from '../../services/search';
import { SearchResponse } from '../../models/search.models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-home',
  imports: [SearchBar, ResultsPanel, ChatSidebar, SettingsDialog],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  isMockMode = environment.useMockApi;
  loading = signal(false);
  searchResponse = signal<SearchResponse | null>(null);
  error = signal('');
  showSettings = signal(false);

  constructor(private searchService: SearchService, private router: Router) {}

  onSearch(query: string): void {
    this.loading.set(true);
    this.error.set('');
    this.searchResponse.set(null);

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
