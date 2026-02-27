export interface SearchProgressStep {
  step: string;
  status: 'active' | 'completed' | 'failed';
  message: string;
}

export const STEP_LABELS: Record<string, string> = {
  expanding_query: 'Expanding query',
  searching_database: 'Searching database',
  ranking_results: 'Ranking results',
};
