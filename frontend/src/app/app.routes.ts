import { inject } from '@angular/core';
import { Routes } from '@angular/router';

import { CurrentSquadService } from './core/current-squad.service';
import { AppDetail } from './features/app-detail/app-detail';
import { AppForm } from './features/app-form/app-form';
import { AppList } from './features/app-list/app-list';
import { CheckDetail } from './features/check-detail/check-detail';
import { CheckForm } from './features/check-form/check-form';
import { CheckList } from './features/check-list/check-list';
import { Dependencies } from './features/dependencies/dependencies';
import { Integrations } from './features/integrations/integrations';
import { Repositories } from './features/repositories/repositories';
import { SquadDashboard } from './features/squad-dashboard/squad-dashboard';
import { SquadForm } from './features/squad-form/squad-form';
import { SquadPicker } from './features/squad-picker/squad-picker';

export const routes: Routes = [
  // "/" abre direto a última squad visitada (o Tech Lead cai na squad dele); sem ela, a lista.
  {
    path: '',
    pathMatch: 'full',
    redirectTo: () => {
      const squadId = inject(CurrentSquadService).squadId();
      return squadId ? `/squads/${squadId}` : '/squads';
    },
  },
  { path: 'squads', component: SquadPicker },
  { path: 'squads/new', component: SquadForm },
  { path: 'squads/:squadId/edit', component: SquadForm },
  { path: 'squads/:squadId', component: SquadDashboard },
  { path: 'squads/:squadId/apps', component: AppList },
  { path: 'squads/:squadId/apps/new', component: AppForm },
  // Lista geral de apps (spec 016); `apps/:appId` é outro caminho, sem conflito.
  { path: 'apps', component: AppList },
  { path: 'apps/:appId/edit', component: AppForm },
  { path: 'apps/:appId', component: AppDetail },
  { path: 'repositories', component: Repositories },
  { path: 'packages', component: Dependencies },
  // Nome antigo da tela ("Dependências", antes de virar "Pacotes"): favoritos continuam funcionando.
  { path: 'dependencies', redirectTo: 'packages', pathMatch: 'full' },
  { path: 'integrations', component: Integrations },
  // Carregado sob demanda: o layout do grafo (dagre) não pesa no bundle inicial.
  { path: 'map', loadComponent: () => import('./features/map/map').then((m) => m.AppMapPage) },
  // Nome antigo da tela ("Descobertos"): favoritos continuam funcionando.
  { path: 'discovered', redirectTo: 'repositories', pathMatch: 'full' },
  { path: 'checks', component: CheckList },
  { path: 'checks/new', component: CheckForm },
  { path: 'checks/:checkId/edit', component: CheckForm },
  { path: 'checks/:checkId', component: CheckDetail },
];
