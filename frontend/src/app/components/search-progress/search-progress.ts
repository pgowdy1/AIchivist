import { Component, computed, input } from '@angular/core';
import { SearchProgressStep, STEP_LABELS } from '../../models/search-progress.models';

const ALL_STEPS = ['expanding_query', 'searching_database', 'ranking_results'] as const;

@Component({
  selector: 'app-search-progress',
  templateUrl: './search-progress.html',
  styleUrl: './search-progress.scss',
})
export class SearchProgress {
  steps = input<SearchProgressStep[]>([]);

  stepStates = computed(() => {
    const current = this.steps();
    return ALL_STEPS.map((key) => {
      const found = current.find((s) => s.step === key);
      return {
        key,
        label: STEP_LABELS[key] ?? key,
        status: found?.status ?? 'pending',
        message: found?.message ?? '',
      };
    });
  });
}
