import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ApiKeyForm } from '../api-key-form/api-key-form';

@Component({
  selector: 'app-setup',
  imports: [ApiKeyForm],
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

          <app-api-key-form
            submitLabel="Save & Continue"
            successMessage="API key saved. Loading..."
            (saved)="onSaved()"
          />
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

  `,
})
export class Setup {
  private router = inject(Router);

  onSaved(): void {
    setTimeout(() => this.router.navigateByUrl('/'), 1000);
  }
}
