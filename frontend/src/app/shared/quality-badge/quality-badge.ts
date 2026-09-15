import { Component, computed, input } from '@angular/core';

import { QualityLevel } from '../../core/models';

const COLOR_BY_LEVEL: Record<QualityLevel, string> = {
  A: 'var(--quality-a)',
  B: 'var(--quality-b)',
  C: 'var(--quality-c)',
  D: 'var(--quality-d)',
  E: 'var(--quality-e)',
};

@Component({
  selector: 'app-quality-badge',
  imports: [],
  templateUrl: './quality-badge.html',
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 1.75rem;
      height: 1.75rem;
      border-radius: 999px;
      font-weight: 700;
      font-size: 0.85rem;
      color: #fff;
    }
  `,
})
export class QualityBadge {
  readonly level = input.required<QualityLevel | null>();

  protected readonly color = computed(() => (this.level() ? COLOR_BY_LEVEL[this.level()!] : 'var(--text-muted)'));
}
