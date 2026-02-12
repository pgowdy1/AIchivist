import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ChatMessage, ChatRequest, ChatResponse } from '../models/chat.models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly apiUrl = 'http://localhost:5265/api';

  constructor(private http: HttpClient) {}

  chat(messages: ChatMessage[], contextId: string): Observable<ChatResponse> {
    const request: ChatRequest = { messages, contextId };
    return this.http.post<ChatResponse>(`${this.apiUrl}/chat`, request);
  }
}
