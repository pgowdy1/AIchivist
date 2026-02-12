import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SearchRequest, SearchResponse, RelatedCollection } from '../models/search.models';

@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly apiUrl = '/api';

  constructor(private http: HttpClient) {}

  search(query: string): Observable<SearchResponse> {
    const request: SearchRequest = { query };
    return this.http.post<SearchResponse>(`${this.apiUrl}/search`, request);
  }

  getRelated(unitId: string): Observable<RelatedCollection[]> {
    return this.http.get<RelatedCollection[]>(`${this.apiUrl}/collections/${unitId}/related`);
  }
}
