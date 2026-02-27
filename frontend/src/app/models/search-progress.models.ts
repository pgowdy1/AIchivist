export type StepName = 'expanding_query' | 'searching_database' | 'ranking_results';

export interface SearchProgressStep {
  step: StepName;
  status: 'active' | 'completed' | 'failed';
  message: string;
}

export const STEP_LABELS: Record<StepName, string> = {
  expanding_query: 'Expanding query',
  searching_database: 'Searching database',
  ranking_results: 'Ranking results',
};
