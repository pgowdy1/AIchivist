import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-api-key-form',
  templateUrl: './api-key-form.html',
  styleUrl: './api-key-form.scss',
})
export class ApiKeyForm {
  private http = inject(HttpClient);
  private destroyRef = inject(DestroyRef);

  submitLabel = input('Save API Key');
  successMessage = input('API key saved successfully!');
  saved = output<void>();

  apiKey = signal('');
  showKey = signal(false);
  saving = signal(false);
  success = signal(false);
  error = signal('');

  save(): void {
    const key = this.apiKey().trim();
    if (!key) return;

    this.saving.set(true);
    this.error.set('');
    this.success.set(false);

    this.http.post<{ success: boolean }>('/api/setup/save', { apiKey: key })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set(true);
          this.saved.emit();
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
