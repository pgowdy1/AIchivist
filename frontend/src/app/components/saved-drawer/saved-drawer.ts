import { Component, inject, input, output, signal, HostListener } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FavoritesService } from '../../services/favorites';
import { CollectionRequestPayload, CollectionRequestResponse } from '../../models/search.models';

@Component({
  selector: 'app-saved-drawer',
  imports: [],
  templateUrl: './saved-drawer.html',
  styleUrl: './saved-drawer.scss',
})
export class SavedDrawer {
  open = input(false);
  closed = output<void>();

  readonly favoritesService = inject(FavoritesService);
  private readonly http = inject(HttpClient);

  requesterName = signal('');
  requesterEmail = signal('');
  requesterPhone = signal('');
  affiliation = signal('');
  notes = signal('');

  submitting = signal(false);
  submitSuccess = signal<{ emailSent: boolean } | null>(null);
  submitError = signal('');

  get isFormValid(): boolean {
    return (
      this.requesterName().trim().length > 0 &&
      this.isValidEmail(this.requesterEmail()) &&
      this.favoritesService.count() > 0
    );
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.open()) {
      this.closed.emit();
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('drawer-backdrop')) {
      this.closed.emit();
    }
  }

  removeCollection(unitId: string): void {
    this.favoritesService.remove(unitId);
  }

  submit(): void {
    if (!this.isFormValid || this.submitting()) return;

    this.submitting.set(true);
    this.submitSuccess.set(null);
    this.submitError.set('');

    const payload: CollectionRequestPayload = {
      requesterName: this.requesterName().trim(),
      requesterEmail: this.requesterEmail().trim(),
      requesterPhone: this.requesterPhone().trim() || null,
      affiliation: this.affiliation().trim() || null,
      notes: this.notes().trim() || null,
      collections: this.favoritesService.savedCollections(),
    };

    this.http.post<CollectionRequestResponse>('/api/requests', payload).subscribe({
      next: (res) => {
        this.submitting.set(false);
        if (res.success) {
          this.submitSuccess.set({ emailSent: res.emailSent });
          this.resetForm();
        } else {
          this.submitError.set(res.message ?? 'Request failed. Please try again.');
        }
      },
      error: () => {
        this.submitting.set(false);
        this.submitError.set('Failed to submit request. Please try again.');
      },
    });
  }

  private resetForm(): void {
    this.requesterName.set('');
    this.requesterEmail.set('');
    this.requesterPhone.set('');
    this.affiliation.set('');
    this.notes.set('');
  }

  private isValidEmail(email: string): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
  }
}
