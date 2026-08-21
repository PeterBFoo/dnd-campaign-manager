import { ChangeDetectionStrategy, Component } from '@angular/core';

import { AccessEntryComponent } from '@modules/access/entry';
import { PlatformStatusComponent } from '@modules/platform';

@Component({
  selector: 'dnd-home-page',
  imports: [AccessEntryComponent, PlatformStatusComponent],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {}
