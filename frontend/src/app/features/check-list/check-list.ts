import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { formatPercent } from '../../core/check-labels';
import { ChecksApiService } from '../../core/checks-api.service';
import { CheckSummary } from '../../core/models';

@Component({
  selector: 'app-check-list',
  imports: [RouterLink, DatePipe],
  templateUrl: './check-list.html',
  styleUrl: './check-list.css',
})
export class CheckList {
  private readonly checksApi = inject(ChecksApiService);

  protected readonly checks = signal<CheckSummary[]>([]);
  protected readonly showClosed = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly formatPercent = formatPercent;

  constructor() {
    this.reload();
  }

  protected toggleClosed(): void {
    this.showClosed.set(!this.showClosed());
    this.reload();
  }

  private reload(): void {
    this.loading.set(true);
    this.checksApi.list(this.showClosed()).subscribe({
      next: (checks) => {
        this.checks.set(checks);
        this.error.set(false);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }
}
