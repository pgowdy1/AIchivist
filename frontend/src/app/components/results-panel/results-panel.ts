import { Component, Input, signal } from '@angular/core';
import { CollectionResult } from '../../models/search.models';
import { ResultCard } from '../result-card/result-card';

@Component({
  selector: 'app-results-panel',
  imports: [ResultCard],
  templateUrl: './results-panel.html',
  styleUrl: './results-panel.scss',
})
export class ResultsPanel {
  @Input() results: CollectionResult[] = [];
  @Input() additionalResults: CollectionResult[] = [];
  @Input() query = '';
  @Input() cached = false;

  showAdditional = signal(false);

  toggleAdditional(): void {
    this.showAdditional.update((v) => !v);
  }
}
