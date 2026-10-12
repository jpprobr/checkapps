import { inject } from '@angular/core';
import { Routes } from '@angular/router';

import { CurrentSquadService } from './core/current-squad.service';
import { AppDetail } from './features/app-detail/app-detail';
import { AppForm } from './features/app-form/app-form';
import { AppList } from './features/app-list/app-list';
import { CheckDetail } from './features/check-detail/check-detail';
import { CheckForm } from './features/check-form/check-form';
import { CheckList } from './features/check-list/check-list';
import { Discovered } from './features/discovered/discovered';
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
  { path: 'apps/:appId/edit', component: AppForm },
  { path: 'apps/:appId', component: AppDetail },
  { path: 'discovered', component: Discovered },
  { path: 'checks', component: CheckList },
  { path: 'checks/new', component: CheckForm },
  { path: 'checks/:checkId/edit', component: CheckForm },
  { path: 'checks/:checkId', component: CheckDetail },
];
