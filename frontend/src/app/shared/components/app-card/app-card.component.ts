import { Component } from '@angular/core';

@Component({
  selector: 'app-card',
  standalone: true,
  template: `<section class="card"><ng-content></ng-content></section>`,
  styles: `
    :host {
      display: block;
      width: 100%;
      min-width: 0;
    }
    .card {
      position: relative;
      overflow: hidden;
      background: var(--card-bg, linear-gradient(160deg, rgba(255,255,255,.9), rgba(248,251,255,.72)));
      border-radius: var(--card-radius, 20px);
      border: 1px solid var(--card-border, rgba(255,255,255,.85));
      box-shadow: var(--card-shadow, var(--shadow-soft));
      padding: var(--card-padding, 1rem);
      animation: soft-rise .38s ease both;
      transition: transform .24s ease, box-shadow .24s ease, border-color .24s ease, background .24s ease;
    }
    .card::after {
      content: '';
      display: var(--card-shimmer-display, block);
      position: absolute;
      inset: 0;
      background: linear-gradient(110deg, transparent 0%, rgba(255,255,255,.35) 45%, transparent 70%);
      transform: translateX(-120%);
      pointer-events: none;
    }
    .card:hover {
      transform: var(--card-hover-transform, translateY(-2px));
      box-shadow: var(--card-hover-shadow, var(--shadow-hover));
      border-color: var(--card-hover-border, #d7e1fa);
    }
    .card:hover::after {
      animation: shimmer .95s ease;
    }
  `
})
export class AppCardComponent {}


