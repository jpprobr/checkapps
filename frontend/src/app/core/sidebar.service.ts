import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'checkapps.sidebarCollapsed';

/**
 * Estado do menu lateral: recolhido (só ícones, lembrado no navegador) e gaveta aberta
 * (celular, volátil). Mesmo tratamento de armazenamento bloqueado do CurrentSquadService.
 */
@Injectable({ providedIn: 'root' })
export class SidebarService {
  readonly collapsed = signal<boolean>(readStored());
  readonly drawerOpen = signal(false);

  toggleCollapsed(): void {
    this.setCollapsed(!this.collapsed());
  }

  setCollapsed(value: boolean): void {
    this.collapsed.set(value);
    try {
      localStorage.setItem(STORAGE_KEY, value ? '1' : '0');
    } catch {
      // Armazenamento bloqueado: só não lembra.
    }
  }

  openDrawer(): void {
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }
}

function readStored(): boolean {
  try {
    return localStorage.getItem(STORAGE_KEY) === '1';
  } catch {
    return false;
  }
}
