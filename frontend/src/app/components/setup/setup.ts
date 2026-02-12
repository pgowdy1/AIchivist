import { Component, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';

@Component({
  selector: 'app-setup',
  template: `
    <div class="setup-backdrop">
      <div class="setup-card">
        <div class="setup-header">
          <h1 class="setup-title">Welcome to AIchivist</h1>
          <p class="setup-subtitle">AI-powered search for WSU archival collections</p>
        </div>

        <div class="setup-body">
          <p class="setup-description">
            To get started, enter your Anthropic API key. This key is stored
            locally on your machine and is used to power the AI search and chat
            features.
          </p>

          <a
            class="setup-link"
            href="https://console.anthropic.com/settings/keys"
            target="_blank"
            rel="noopener noreferrer"
          >
            Get an API key from Anthropic &rarr;
          </a>

          <label class="setup-label" for="apiKeyInput">API Key</label>
          <div class="setup-input-wrapper">
            <input
              id="apiKeyInput"
              class="setup-input"
              [type]="showKey() ? 'text' : 'password'"
              [value]="apiKey()"
              (input)="apiKey.set($any($event.target).value)"
              placeholder="sk-ant-..."
              autocomplete="off"
              spellcheck="false"
            />
            <button
              type="button"
              class="setup-toggle"
              (click)="showKey.set(!showKey())"
              [attr.aria-label]="showKey() ? 'Hide API key' : 'Show API key'"
            >
              {{ showKey() ? 'Hide' : 'Show' }}
            </button>
          </div>

          @if (error()) {
            <p class="setup-error">{{ error() }}</p>
          }

          <button
            class="setup-submit"
            [disabled]="saving() || !apiKey().trim()"
            (click)="save()"
          >
            @if (saving()) {
              <span class="setup-spinner"></span>
              Saving...
            } @else {
              Save & Continue
            }
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .setup-backdrop {
      min-height: 100vh;
      background: var(--color-bg);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
    }

    .setup-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg);
      max-width: 480px;
      width: 100%;
      overflow: hidden;
    }

    .setup-header {
      background: var(--color-primary);
      color: white;
      padding: 32px 32px 28px;
      text-align: center;
    }

    .setup-title {
      margin: 0 0 6px;
      font-size: 1.6rem;
      font-weight: 700;
      letter-spacing: -0.02em;
    }

    .setup-subtitle {
      margin: 0;
      font-size: 0.88rem;
      opacity: 0.8;
      font-weight: 400;
    }

    .setup-body {
      padding: 28px 32px 32px;
    }

    .setup-description {
      margin: 0 0 16px;
      font-size: 0.9rem;
      line-height: 1.6;
      color: var(--color-text-secondary);
    }

    .setup-link {
      display: inline-block;
      margin-bottom: 24px;
      font-size: 0.85rem;
      color: var(--color-primary);
      font-weight: 500;
      text-decoration: none;

      &:hover {
        text-decoration: underline;
      }
    }

    .setup-label {
      display: block;
      margin-bottom: 6px;
      font-size: 0.82rem;
      font-weight: 600;
      color: var(--color-text);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .setup-input-wrapper {
      display: flex;
      gap: 0;
      margin-bottom: 20px;
    }

    .setup-input {
      flex: 1;
      padding: 10px 14px;
      border: 1px solid var(--color-border);
      border-right: none;
      border-radius: var(--radius-sm) 0 0 var(--radius-sm);
      font-size: 0.9rem;
      font-family: 'Courier New', Courier, monospace;
      background: var(--color-bg);
      color: var(--color-text);
      outline: none;
      transition: border-color var(--transition-fast);

      &:focus {
        border-color: var(--color-primary);
        box-shadow: 0 0 0 3px var(--color-primary-ring);
      }

      &::placeholder {
        color: var(--color-text-muted);
      }
    }

    .setup-toggle {
      padding: 10px 14px;
      border: 1px solid var(--color-border);
      border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
      background: var(--color-bg);
      color: var(--color-text-secondary);
      font-size: 0.8rem;
      font-weight: 500;
      cursor: pointer;
      transition: background var(--transition-fast), color var(--transition-fast);
      white-space: nowrap;

      &:hover {
        background: var(--color-border-light);
        color: var(--color-text);
      }
    }

    .setup-error {
      margin: 0 0 16px;
      padding: 10px 14px;
      background: var(--color-error-bg);
      color: var(--color-error);
      border-radius: var(--radius-sm);
      font-size: 0.85rem;
      line-height: 1.5;
    }

    .setup-submit {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      width: 100%;
      padding: 12px 24px;
      border: none;
      border-radius: var(--radius-sm);
      background: var(--color-primary);
      color: white;
      font-size: 0.95rem;
      font-weight: 600;
      cursor: pointer;
      transition: background var(--transition-fast);

      &:hover:not(:disabled) {
        background: var(--color-primary-dark);
      }

      &:disabled {
        opacity: 0.55;
        cursor: not-allowed;
      }
    }

    .setup-spinner {
      display: inline-block;
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `,
})
export class Setup {
  apiKey = signal('');
  showKey = signal(false);
  saving = signal(false);
  error = signal('');

  constructor(
    private http: HttpClient,
    private router: Router,
  ) {}

  save(): void {
    const key = this.apiKey().trim();
    if (!key) return;

    this.saving.set(true);
    this.error.set('');

    this.http.post<{ success: boolean }>('/api/setup/save', { apiKey: key }).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigateByUrl('/');
      },
      error: (err) => {
        this.saving.set(false);
        const message =
          err.error?.message ||
          err.error?.error ||
          'Failed to save API key. Make sure the backend is running.';
        this.error.set(message);
      },
    });
  }
}
