import { Routes } from '@angular/router';

import { AppDetail } from './features/app-detail/app-detail';
import { AppList } from './features/app-list/app-list';
import { SquadDashboard } from './features/squad-dashboard/squad-dashboard';
import { SquadPicker } from './features/squad-picker/squad-picker';

export const routes: Routes = [
  { path: '', component: SquadPicker },
  { path: 'squads/:squadId', component: SquadDashboard },
  { path: 'squads/:squadId/apps', component: AppList },
  { path: 'apps/:appId', component: AppDetail },
];
