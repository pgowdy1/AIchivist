import { SearchResponse, RelatedCollection } from '../models/search.models';
import { MOCK_COLLECTIONS, MOCK_CHAT_RESPONSES, DEFAULT_CHAT_RESPONSE } from './mock-data';

export function mockSearch(query: string): SearchResponse {
  const queryTerms = query
    .toLowerCase()
    .split(/\s+/)
    .filter((t) => t.length > 2);

  const scored = MOCK_COLLECTIONS.map((c) => {
    const searchableText = [
      c.title,
      c.abstract,
      c.scopeContent,
      c.biogHist,
      ...c.subjects,
      ...c.persnames,
      ...c.geognames,
      ...c.genres,
      ...c.seriesTitles,
    ]
      .filter(Boolean)
      .join(' ')
      .toLowerCase();

    let matchCount = 0;
    for (const term of queryTerms) {
      if (searchableText.includes(term)) matchCount++;
    }
    return { collection: c, matchCount };
  });

  const results = scored
    .filter((s) => s.matchCount > 0)
    .sort((a, b) => b.matchCount - a.matchCount)
    .slice(0, 10)
    .map((s, i) => ({
      ...s.collection,
      rank: i + 1,
      relevanceScore: Math.round((10 - i * 0.7) * 10) / 10,
      relevanceExplanation: buildExplanation(s.collection.title, query, s.matchCount),
    }));

  // If nothing matched, return a random sample so the UI isn't empty
  const finalResults =
    results.length > 0
      ? results
      : MOCK_COLLECTIONS.slice(0, 5).map((c, i) => ({
          ...c,
          rank: i + 1,
          relevanceScore: 5,
          relevanceExplanation: `Potentially related to "${query}" — this collection may contain tangentially relevant materials.`,
        }));

  return {
    query,
    contextId: 'mock-ctx-' + Date.now(),
    results: finalResults,
    cached: false,
  };
}

export function mockGetRelated(unitId: string): RelatedCollection[] {
  const source = MOCK_COLLECTIONS.find((c) => c.collectionUnitId === unitId);
  if (!source) return [];

  return MOCK_COLLECTIONS.filter((c) => c.collectionUnitId !== unitId)
    .map((c) => {
      const sharedSubjects = c.subjects.filter((s) => source.subjects.includes(s));
      const sharedPersons = c.persnames.filter((p) => source.persnames.includes(p));
      const sharedPlaces = c.geognames.filter((g) => source.geognames.includes(g));
      const overlapScore = sharedSubjects.length + sharedPersons.length + sharedPlaces.length;
      return {
        collectionUnitId: c.collectionUnitId,
        title: c.title,
        repository: c.repository,
        dateRange: c.dateRange,
        abstract: c.abstract,
        overlapScore,
        sharedSubjects,
        sharedPersons,
        sharedOrganizations: [] as string[],
        sharedPlaces,
        overlapSummary:
          overlapScore > 0
            ? `Shares ${sharedSubjects.length} subject(s), ${sharedPersons.length} person(s), and ${sharedPlaces.length} place(s)`
            : 'Tangentially related collection',
      } satisfies RelatedCollection;
    })
    .filter((r) => r.overlapScore > 0)
    .sort((a, b) => b.overlapScore - a.overlapScore)
    .slice(0, 5);
}

export function mockChat(userMessage: string): string {
  const lower = userMessage.toLowerCase();

  for (const entry of MOCK_CHAT_RESPONSES) {
    if (entry.keywords.some((kw) => lower.includes(kw))) {
      return entry.response;
    }
  }

  return DEFAULT_CHAT_RESPONSE;
}

function buildExplanation(title: string, query: string, matchCount: number): string {
  if (matchCount >= 4) {
    return `Highly relevant — "${title}" directly addresses multiple aspects of your query about ${query}.`;
  }
  if (matchCount >= 2) {
    return `Relevant — "${title}" contains materials related to your search for ${query}.`;
  }
  return `Potentially relevant — "${title}" may contain materials related to your query.`;
}
