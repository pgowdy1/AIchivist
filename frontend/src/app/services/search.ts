import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SearchRequest, SearchResponse } from '../models/search.models';

@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly apiUrl = 'http://localhost:5265/api';

  constructor(private http: HttpClient) {}

  search(query: string): Observable<SearchResponse> {
    const request: SearchRequest = { query };
    return this.http.post<SearchResponse>(`${this.apiUrl}/search`, request);
  }
}
