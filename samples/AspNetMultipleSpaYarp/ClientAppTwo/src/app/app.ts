import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { NavMenu } from './nav-menu/nav-menu';

@Component({
  imports: [RouterOutlet, NavMenu],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {}
