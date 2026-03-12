import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-public-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="public-layout">
      <router-outlet></router-outlet>
    </div>
  `,
  styles: `
    .public-layout {
      min-height: 100vh;
    }
  `
})
export class PublicLayoutComponent {}
