import { Component, EventEmitter, Output, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-settings-dialog',
  template: `
    <div class="settings-backdrop" (click)="onBackdropClick($event)">
      <div class="settings-card">
        <div class="settings-header">
          <h2 class="settings-title">Settings</h2>
          <button
            type="button"
            class="settings-close"
            (click)="close()"
            aria-label="Close settings"
          >
            &times;
          </button>
        </div>

        <div class="settings-body">
          <h3 class="settings-section-title">Anthropic API Key</h3>
          <p class="settings-description">
            Your API key is stored locally and used to power AI search and chat features.
          </p>

          <a
            class="settings-link"
            href="https://console.anthropic.com/settings/keys"
            target="_blank"
            rel="noopener noreferrer"
          >
            Get an API key from Anthropic &rarr;
          </a>

          <label class="settings-label" for="settingsApiKeyInput">API Key</label>
          <div class="settings-input-wrapper">
            <input
              id="settingsApiKeyInput"
              class="settings-input"
              [type]="showKey() ? 'text' : 'password'"
              [value]="apiKey()"
              (input)="apiKey.set($any($event.target).value)"
              placeholder="sk-ant-..."
              autocomplete="off"
              spellcheck="false"
            />
            <button
              type="button"
              class="settings-toggle"
              (click)="showKey.set(!showKey())"
              [attr.aria-label]="showKey() ? 'Hide API key' : 'Show API key'"
            >
              {{ showKey() ? 'Hide' : 'Show' }}
            </button>
          </div>

          @if (error()) {
            <p class="settings-error">{{ error() }}</p>
          }

          @if (success()) {
            <p class="settings-success">API key updated successfully!</p>
          }

          <button
            class="settings-submit"
            [disabled]="saving() || success() || !apiKey().trim()"
            (click)="save()"
          >
            @if (saving()) {
              <span class="settings-spinner"></span>
              Saving...
            } @else {
              Save API Key
            }
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .settings-backdrop {
      position: fixed;
      inset: 0;
      z-index: 1000;
      background: rgba(0, 0, 0, 0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
      animation: settings-fade-in 0.2s ease;
    }

    @keyframes settings-fade-in {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes settings-slide-up {
      from {
        opacity: 0;
        transform: translateY(12px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .settings-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg);
      max-width: 480px;
      width: 100%;
      overflow: hidden;
      animation: settings-slide-up 0.25s ease;
    }

    .settings-header {
      background: var(--color-primary);
      color: white;
      padding: 24px 32px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .settings-title {
      margin: 0;
      font-size: 1.3rem;
      font-weight: 700;
      letter-spacing: -0.02em;
    }

    .settings-close {
      background: none;
      border: none;
      color: white;
      font-size: 1.6rem;
      line-height: 1;
      cursor: pointer;
      padding: 4px 8px;
      border-radius: var(--radius-sm);
      opacity: 0.8;
      transition: opacity var(--transition-fast), background var(--transition-fast);

      &:hover {
        opacity: 1;
        background: rgba(255, 255, 255, 0.15);
      }
    }

    .settings-body {
      padding: 28px 32px 32px;
    }

    .settings-section-title {
      margin: 0 0 8px;
      font-size: 1rem;
      font-weight: 600;
      color: var(--color-text);
    }

    .settings-description {
      margin: 0 0 16px;
      font-size: 0.9rem;
      line-height: 1.6;
      color: var(--color-text-secondary);
    }

    .settings-link {
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

    .settings-label {
      display: block;
      margin-bottom: 6px;
      font-size: 0.82rem;
      font-weight: 600;
      color: var(--color-text);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .settings-input-wrapper {
      display: flex;
      gap: 0;
      margin-bottom: 20px;
    }

    .settings-input {
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

    .settings-toggle {
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

    .settings-error {
      margin: 0 0 16px;
      padding: 10px 14px;
      background: var(--color-error-bg);
      color: var(--color-error);
      border-radius: var(--radius-sm);
      font-size: 0.85rem;
      line-height: 1.5;
    }

    .settings-success {
      margin: 0 0 16px;
      padding: 10px 14px;
      background: var(--color-success-bg);
      color: var(--color-success);
      border-radius: var(--radius-sm);
      font-size: 0.85rem;
      line-height: 1.5;
      font-weight: 500;
    }

    .settings-submit {
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

    .settings-spinner {
      display: inline-block;
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: settings-spin 0.6s linear infinite;
    }

    @keyframes settings-spin {
      to { transform: rotate(360deg); }
    }
  `,
})
export class SettingsDialog {
  @Output() closed = new EventEmitter<void>();

  private http = inject(HttpClient);

  apiKey = signal('');
  showKey = signal(false);
  saving = signal(false);
  success = signal(false);
  error = signal('');

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('settings-backdrop')) {
      this.close();
    }
  }

  close(): void {
    this.closed.emit();
  }

  save(): void {
    const key = this.apiKey().trim();
    if (!key) return;

    this.saving.set(true);
    this.error.set('');

    this.http.post<{ success: boolean }>('/api/setup/save', { apiKey: key }).subscribe({
      next: () => {
        this.saving.set(false);
        this.success.set(true);
        setTimeout(() => this.closed.emit(), 1500);
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
