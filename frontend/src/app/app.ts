import { Component, HostListener, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { CurrentSquadService } from './core/current-squad.service';
import { SidebarService } from './core/sidebar.service';

/**
 * Shell: menu lateral (marca, seletor de squad e seções), recolhível para só ícones e
 * gaveta no celular. O seletor de squad é também o indicador permanente de "qual squad
 * estou vendo".
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
  protected readonly sidebar = inject(SidebarService);

  constructor() {
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => this.sidebar.closeDrawer());
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.sidebar.closeDrawer();
  }

  /** Ciente de contexto: na lista de apps, troca para a lista de apps da outra squad; no resto, vai ao dashboard dela. */
  protected switchSquad(squadId: string): void {
    if (!squadId) {
      return;
    }
    const onAppList = /^\/squads\/[^/]+\/apps(\?|$)/.test(this.router.url);
    this.router.navigate(onAppList ? ['/squads', squadId, 'apps'] : ['/squads', squadId]);
  }
}
