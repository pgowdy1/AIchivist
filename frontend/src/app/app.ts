import { Component, signal } from '@angular/core';
import { SearchBar } from './components/search-bar/search-bar';
import { ResultsPanel } from './components/results-panel/results-panel';
import { ChatSidebar } from './components/chat-sidebar/chat-sidebar';
import { SearchService } from './services/search';
import { SearchResponse } from './models/search.models';

@Component({
  selector: 'app-root',
  imports: [SearchBar, ResultsPanel, ChatSidebar],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  loading = signal(false);
  searchResponse = signal<SearchResponse | null>(null);
  error = signal('');

  constructor(private searchService: SearchService) {}

  onSearch(query: string): void {
    this.loading.set(true);
    this.error.set('');
    this.searchResponse.set(null);

    this.searchService.search(query).subscribe({
      next: (res) => {
        this.searchResponse.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Search failed. Make sure the backend is running.');
        this.loading.set(false);
      }
    });
  }
}
