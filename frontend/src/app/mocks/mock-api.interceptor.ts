import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { delay, of } from 'rxjs';
import { mockSearch, mockGetRelated, mockChat } from './mock-search';

export const mockApiInterceptor: HttpInterceptorFn = (req, next) => {
  const url = req.url;

  // POST /api/search
  if (req.method === 'POST' && url.endsWith('/api/search')) {
    const body = req.body as { query: string };
    return of(new HttpResponse({ status: 200, body: mockSearch(body.query) })).pipe(delay(800));
  }

  // GET /api/collections/{unitId}/related
  const relatedMatch = url.match(/\/api\/collections\/(.+)\/related$/);
  if (req.method === 'GET' && relatedMatch) {
    const unitId = decodeURIComponent(relatedMatch[1]);
    return of(new HttpResponse({ status: 200, body: mockGetRelated(unitId) })).pipe(delay(500));
  }

  // POST /api/chat
  if (req.method === 'POST' && url.endsWith('/api/chat')) {
    const body = req.body as { messages: { role: string; content: string }[]; contextId: string };
    const lastUserMsg = [...body.messages].reverse().find((m) => m.role === 'user');
    return of(
      new HttpResponse({ status: 200, body: { response: mockChat(lastUserMsg?.content ?? '') } }),
    ).pipe(delay(1200));
  }

  return next(req);
};
