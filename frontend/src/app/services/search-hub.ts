import { Injectable, signal } from '@angular/core';
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
} from '@microsoft/signalr';
import { SearchProgressStep } from '../models/search-progress.models';
import { SearchResponse } from '../models/search.models';

@Injectable({ providedIn: 'root' })
export class SearchHubService {
  private connection: HubConnection | null = null;

  readonly progress = signal<SearchProgressStep[]>([]);
  readonly searchResult = signal<SearchResponse | null>(null);
  readonly searchError = signal<string | null>(null);
  readonly connected = signal(false);

  async search(query: string): Promise<boolean> {
    this.progress.set([]);
    this.searchResult.set(null);
    this.searchError.set(null);

    try {
      if (!this.connection) {
        this.connection = new HubConnectionBuilder()
          .withUrl('/hubs/search')
          .withAutomaticReconnect()
          .build();
      }

      this.connection.off('SearchProgress');
      this.connection.off('SearchCompleted');
      this.connection.off('SearchFailed');

      this.connection.on('SearchProgress', (step: SearchProgressStep) => {
        this.progress.update(steps => {
          const index = steps.findIndex(s => s.step === step.step);
          if (index >= 0) {
            const updated = [...steps];
            updated[index] = step;
            return updated;
          }
          return [...steps, step];
        });
      });

      this.connection.on('SearchCompleted', (result: SearchResponse) => {
        this.searchResult.set(result);
      });

      this.connection.on('SearchFailed', (error: { error: string; failedStep: string }) => {
        this.searchError.set(error.error);
      });

      if (this.connection.state === HubConnectionState.Disconnected) {
        await this.connection.start();
      }

      this.connected.set(true);
      await this.connection.invoke('StartSearch', query);
      return true;
    } catch {
      this.connected.set(false);
      return false;
    }
  }

  disconnect(): void {
    if (this.connection) {
      this.connection.stop();
      this.connection = null;
      this.connected.set(false);
    }
  }
}
