import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';
import { AdventureModule } from './api/adventure-modules.contracts';
import { AdventureModulesClient } from './api/adventure-modules.client';

@Component({
  selector: 'dnd-adventure-modules',
  imports: [RouterLink, DatePipe],
  templateUrl: './adventure-modules.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdventureModulesPage implements OnInit {
  private readonly client = inject(AdventureModulesClient);
  readonly modules = signal<AdventureModule[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly coverUrls = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.client.list().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (items) => { this.modules.set(items); for (const item of items.filter((candidate) => candidate.coverUrl)) this.client.cover(item.id).subscribe({ next: (blob) => this.coverUrls.update((urls) => ({ ...urls, [item.id]: URL.createObjectURL(blob) })) }); },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar los módulos.')),
    });
  }

  remove(item: AdventureModule): void {
    if (!confirm(`¿Eliminar «${item.name}»?`)) return;
    this.client.delete(item.id, item.version).subscribe({
      next: () => this.modules.update((items) => items.filter((candidate) => candidate.id !== item.id)),
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido eliminar el módulo.')),
    });
  }
}
