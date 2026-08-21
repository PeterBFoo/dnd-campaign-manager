import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';

import { PlatformClient } from '../api/platform.client';
import { PlatformStatusStore } from './platform-status.store';

@Component({
  selector: 'dnd-platform-status',
  imports: [DatePipe],
  providers: [PlatformClient, PlatformStatusStore],
  templateUrl: './platform-status.component.html',
  styleUrl: './platform-status.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlatformStatusComponent implements OnInit {
  readonly store = inject(PlatformStatusStore);

  ngOnInit(): void {
    this.store.load();
  }
}
