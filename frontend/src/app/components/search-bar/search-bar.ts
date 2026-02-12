import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-search-bar',
  imports: [FormsModule],
  templateUrl: './search-bar.html',
  styleUrl: './search-bar.scss',
})
export class SearchBar {
  @Input() loading = false;
  @Output() searchTriggered = new EventEmitter<string>();

  query = '';

  readonly exampleQueries = [
    'Find collections related to World War II at Washington State',
    'What materials exist about student protests in the 1970s?',
    'Find information about agricultural research in the 1950s',
    'Search for letters or diaries from early faculty from 1850–1870',
  ];

  onSubmit(): void {
    const trimmed = this.query.trim();
    if (trimmed.length >= 3 && !this.loading) {
      this.searchTriggered.emit(trimmed);
    }
  }

  useExample(example: string): void {
    this.query = example;
    this.onSubmit();
  }
}
