import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';
import { AdventureModule, EditorialOrigin } from './api/adventure-modules.contracts';
import { AdventureModulesClient } from './api/adventure-modules.client';

@Component({ selector: 'dnd-adventure-module-editor', imports: [ReactiveFormsModule, RouterLink], templateUrl: './adventure-module-editor.page.html', changeDetection: ChangeDetectionStrategy.OnPush })
export class AdventureModuleEditorPage implements OnInit {
  private readonly client = inject(AdventureModulesClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly editing = signal<AdventureModule | null>(null);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly file = signal<File | null>(null);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3)] }),
    description: new FormControl('', { nonNullable: true }),
    originKind: new FormControl<EditorialOrigin>('Original', { nonNullable: true }),
    sourceReference: new FormControl('', { nonNullable: true }),
    rightsBasis: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    attribution: new FormControl('', { nonNullable: true }),
    removeCover: new FormControl(false, { nonNullable: true }),
  });
  readonly origins: EditorialOrigin[] = ['Original', 'Licensed', 'Permission', 'PublicDomain', 'FanContentPolicy'];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('moduleId');
    if (!id) return;
    this.client.get(id).subscribe({ next: (item) => { this.editing.set(item); this.form.patchValue({ name: item.name, description: item.description ?? '', originKind: item.textProvenance.originKind, sourceReference: item.textProvenance.sourceReference ?? '', rightsBasis: item.textProvenance.rightsBasis, attribution: item.textProvenance.attribution ?? '' }); }, error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido cargar el módulo.')) });
  }

  chooseFile(event: Event): void { const input = event.target as HTMLInputElement; this.file.set(input.files?.[0] ?? null); }

  save(): void {
    if (this.form.invalid || this.submitting()) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true); this.error.set(null);
    const value = this.form.getRawValue();
    const provenance = { originKind: value.originKind, sourceReference: value.sourceReference, rightsBasis: value.rightsBasis, attribution: value.attribution };
    const current = this.editing();
    const coverProvenance = this.file() ? { originKind: 'Original' as const, rightsBasis: 'Autoría propia' } : null;
    const request = current ? this.client.update(current.id, current.version, value.name, value.description, provenance, this.file(), coverProvenance, value.removeCover) : this.client.create(value.name, value.description, provenance, this.file(), coverProvenance);
    request.pipe(finalize(() => this.submitting.set(false))).subscribe({ next: () => void this.router.navigate(['/admin/adventure-modules']), error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido guardar el módulo.')) });
  }
}
