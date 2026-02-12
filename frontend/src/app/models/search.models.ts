export interface SearchRequest {
  query: string;
}

export interface CollectionResult {
  rank: number;
  relevanceScore: number;
  relevanceExplanation: string;
  collectionUnitId: string;
  title: string;
  repository: string | null;
  dateRange: string | null;
  extent: string | null;
  abstract: string | null;
  scopeContent: string | null;
  subjects: string[];
  persnames: string[];
  geognames: string[];
  genres: string[];
  seriesTitles: string[];
}

export interface SearchResponse {
  query: string;
  contextId: string;
  results: CollectionResult[];
  cached: boolean;
}
