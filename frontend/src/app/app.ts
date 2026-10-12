import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { CurrentSquadService } from './core/current-squad.service';

/**
 * Shell fino: produto → home, seções de topo e o seletor de squad, que também é o
 * indicador permanente de "qual squad estou vendo".
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly router = inject(Router);
  protected readonly currentSquad = inject(CurrentSquadService);

  /** Ciente de contexto: na lista de apps, troca para a lista de apps da outra squad; no resto, vai ao dashboard dela. */
  protected switchSquad(squadId: string): void {
    if (!squadId) {
      return;
    }
    const onAppList = /^\/squads\/[^/]+\/apps(\?|$)/.test(this.router.url);
    this.router.navigate(onAppList ? ['/squads', squadId, 'apps'] : ['/squads', squadId]);
  }
}
