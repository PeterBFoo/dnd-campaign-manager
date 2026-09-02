import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { SessionStore } from '@modules/access';

@Component({
  selector: 'dnd-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly currentUrl = signal(this.router.url);

  readonly campaignId = computed(() => this.currentUrl().match(/^\/campaigns\/([^/]+)/)?.[1] ?? null);
  readonly userInitial = computed(() => this.session.user()?.displayName.trim().charAt(0).toUpperCase() || '?');

  constructor() {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(),
    ).subscribe((event) => this.currentUrl.set(event.urlAfterRedirects));
  }

  logout(): void {
    this.session.logout().subscribe({
      next: () => void this.router.navigate(['/']),
      error: () => void this.router.navigate(['/']),
    });
  }
}
