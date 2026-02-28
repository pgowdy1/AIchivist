export interface SearchRequest {
  query: string;
}

export interface CollectionResult {
  rank: number;
  relevanceScore: number;
  relevanceExplanation: string;
  isAiRanked?: boolean;
  collectionUnitId: string;
  title: string;
  repository: string | null;
  dateRange: string | null;
  extent: string | null;
  abstract: string | null;
  scopeContent: string | null;
  biogHist: string | null;
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
  additionalResults?: CollectionResult[];
  cached: boolean;
}

export interface RelatedCollection {
  collectionUnitId: string;
  title: string;
  repository: string | null;
  dateRange: string | null;
  abstract: string | null;
  overlapScore: number;
  sharedSubjects: string[];
  sharedPersons: string[];
  sharedOrganizations: string[];
  sharedPlaces: string[];
  overlapSummary: string;
}

export interface SavedCollection {
  collectionUnitId: string;
  title: string;
  repository: string | null;
  dateRange: string | null;
}

export interface CollectionRequestPayload {
  requesterName: string;
  requesterEmail: string;
  requesterPhone: string | null;
  affiliation: string | null;
  notes: string | null;
  collections: SavedCollection[];
}

export interface CollectionRequestResponse {
  success: boolean;
  emailSent: boolean;
  message: string | null;
}
