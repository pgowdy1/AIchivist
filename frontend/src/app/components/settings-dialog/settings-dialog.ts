import { Component, EventEmitter, Output, inject, HostListener, DestroyRef } from '@angular/core';
import { ApiKeyForm } from '../api-key-form/api-key-form';

@Component({
  selector: 'app-settings-dialog',
  imports: [ApiKeyForm],
  template: `
    <div class="settings-backdrop" (click)="onBackdropClick($event)">
      <div class="settings-card" role="dialog" aria-modal="true" aria-labelledby="settings-dialog-title">
        <div class="settings-header">
          <h2 class="settings-title" id="settings-dialog-title">Settings</h2>
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

          <app-api-key-form
            successMessage="API key updated successfully!"
            (saved)="onSaved()"
          />
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

  `,
})
export class SettingsDialog {
  @Output() closed = new EventEmitter<void>();

  private destroyRef = inject(DestroyRef);
  private closeTimerId: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => {
      if (this.closeTimerId) clearTimeout(this.closeTimerId);
    });
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    this.close();
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('settings-backdrop')) {
      this.close();
    }
  }

  close(): void {
    if (this.closeTimerId) {
      clearTimeout(this.closeTimerId);
      this.closeTimerId = null;
    }
    this.closed.emit();
  }

  onSaved(): void {
    this.closeTimerId = setTimeout(() => this.closed.emit(), 1500);
  }
}
