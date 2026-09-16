import { Routes } from '@angular/router';

import { AppDetail } from './features/app-detail/app-detail';
import { AppForm } from './features/app-form/app-form';
import { AppList } from './features/app-list/app-list';
import { SquadDashboard } from './features/squad-dashboard/squad-dashboard';
import { SquadForm } from './features/squad-form/squad-form';
import { SquadPicker } from './features/squad-picker/squad-picker';

export const routes: Routes = [
  { path: '', component: SquadPicker },
  { path: 'squads/new', component: SquadForm },
  { path: 'squads/:squadId/edit', component: SquadForm },
  { path: 'squads/:squadId', component: SquadDashboard },
  { path: 'squads/:squadId/apps', component: AppList },
  { path: 'squads/:squadId/apps/new', component: AppForm },
  { path: 'apps/:appId/edit', component: AppForm },
  { path: 'apps/:appId', component: AppDetail },
];
