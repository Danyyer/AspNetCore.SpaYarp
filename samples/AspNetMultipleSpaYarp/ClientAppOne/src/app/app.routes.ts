import { Routes } from '@angular/router';

import { Counter } from './counter/counter';
import { FetchData } from './fetch-data/fetch-data';
import { Home } from './home/home';

export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  { path: 'counter', component: Counter },
  { path: 'fetch-data', component: FetchData },
];
