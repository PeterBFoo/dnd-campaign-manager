import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdventureModule } from './api/adventure-modules.contracts';
import { AdventureModulesClient } from './api/adventure-modules.client';

@Component({ selector: 'dnd-adventure-module-detail', imports: [DatePipe, RouterLink], templateUrl: './adventure-module-detail.page.html', changeDetection: ChangeDetectionStrategy.OnPush })
export class AdventureModuleDetailPage implements OnInit {
  private readonly client = inject(AdventureModulesClient);
  private readonly route = inject(ActivatedRoute);
  readonly module = signal<AdventureModule | null>(null);
  readonly coverSource = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  ngOnInit(): void { const id = this.route.snapshot.paramMap.get('moduleId'); if (id) this.client.get(id).subscribe({ next: (item) => { this.module.set(item); if (item.coverUrl) this.client.cover(item.id).subscribe({ next: (blob) => this.coverSource.set(URL.createObjectURL(blob)) }); }, error: () => this.error.set('No se ha podido cargar el módulo.') }); }
}
