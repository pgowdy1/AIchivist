import { Component, computed, input } from '@angular/core';
import { SearchProgressStep, STEP_LABELS } from '../../models/search-progress.models';

const ALL_STEPS = ['expanding_query', 'searching_database', 'ranking_results'];

@Component({
  selector: 'app-search-progress',
  template: `
    <div class="stepper">
      @for (step of stepStates(); track step.key) {
        <div class="step" [class]="'step--' + step.status">
          <div class="step-icon">
            @switch (step.status) {
              @case ('completed') {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"
                     stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              }
              @case ('active') {
                <div class="spinner"></div>
              }
              @case ('failed') {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"
                     stroke-linecap="round" stroke-linejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </svg>
              }
              @default {
                <div class="dot"></div>
              }
            }
          </div>
          <div class="step-content">
            <span class="step-label">{{ step.label }}</span>
            @if (step.message) {
              <span class="step-message">{{ step.message }}</span>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .stepper {
      display: flex;
      flex-direction: column;
      gap: 16px;
      min-width: 300px;
    }

    .step {
      display: flex;
      align-items: flex-start;
      gap: 12px;
    }

    .step-icon {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      border: 2px solid var(--color-border, #ddd);
      background: white;
      transition: all 0.3s ease;
    }

    .step-icon svg {
      width: 14px;
      height: 14px;
    }

    .step--completed .step-icon {
      background: #16a34a;
      border-color: #16a34a;
      color: white;
    }

    .step--active .step-icon {
      background: #2563eb;
      border-color: #2563eb;
      color: white;
    }

    .step--failed .step-icon {
      background: #dc2626;
      border-color: #dc2626;
      color: white;
    }

    .step--pending .step-icon {
      background: white;
      border-color: var(--color-border, #d1d5db);
    }

    .dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--color-border, #d1d5db);
    }

    .spinner {
      width: 14px;
      height: 14px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .step-content {
      display: flex;
      flex-direction: column;
      padding-top: 3px;
    }

    .step-label {
      font-size: 0.95rem;
      font-weight: 600;
      color: var(--color-text, #1f2937);
    }

    .step--pending .step-label {
      color: var(--color-text-secondary, #9ca3af);
    }

    .step-message {
      font-size: 0.82rem;
      color: var(--color-text-secondary, #6b7280);
      margin-top: 2px;
    }
  `,
})
export class SearchProgress {
  steps = input<SearchProgressStep[]>([]);

  stepStates = computed(() => {
    const current = this.steps();
    return ALL_STEPS.map((key) => {
      const found = current.find((s) => s.step === key);
      return {
        key,
        label: STEP_LABELS[key] ?? key,
        status: found?.status ?? 'pending',
        message: found?.message ?? '',
      };
    });
  });
}
