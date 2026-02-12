import { Component, inject, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);

  ngOnInit(): void {
    this.http.get<{ configured: boolean }>('/api/setup/status').subscribe({
      next: (res) => {
        if (!res.configured) {
          this.router.navigateByUrl('/setup');
        }
      },
      error: () => {
        // If the status endpoint fails (backend not running), stay on current route.
        // The home component will show its own error when search is attempted.
      },
    });
  }
}
