import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';

import { PlatformStatusService } from './platform-status.service';

@Component({
  selector: 'dnd-root',
  imports: [DatePipe],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  readonly platform = inject(PlatformStatusService);

  ngOnInit(): void {
    this.platform.load();
  }
}
