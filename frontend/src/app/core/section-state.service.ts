import { Injectable } from '@angular/core';

const STORAGE_KEY = 'checkapps.openSections';

/**
 * Quais seções recolhíveis do detalhe do app estão abertas (spec 016). É preferência de **navegador**, por seção e não
 * por app: abrir "Integrações" num app a deixa aberta nos outros. Tudo fechado por padrão. Mesmo tratamento de
 * armazenamento bloqueado do SidebarService: sem localStorage a seção funciona e só não lembra.
 */
@Injectable({ providedIn: 'root' })
export class SectionStateService {
  isOpen(section: string): boolean {
    return readAll()[section] === true;
  }

  setOpen(section: string, open: boolean): void {
    const all = readAll();
    all[section] = open;
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(all));
    } catch {
      // Armazenamento bloqueado (janela privada, política do navegador): só não lembra.
    }
  }
}

/** Valor ausente, ilegível ou de outra forma que não um mapa conta como "tudo fechado". */
function readAll(): Record<string, boolean> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const parsed: unknown = raw ? JSON.parse(raw) : {};
    return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as Record<string, boolean>) : {};
  } catch {
    return {};
  }
}
