import { NgIf } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [NgIf],
  template: `
    <div class="header">
      <div>
        <h1>{{ title }}</h1>
        <p *ngIf="subtitle">{{ subtitle }}</p>
      </div>
      <div class="actions"><ng-content></ng-content></div>
    </div>
  `,
  styles: `
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1.1rem;
      padding: .2rem .1rem;
      animation: soft-rise .28s ease both;
    }
    h1 {
      margin: 0;
      font-family: var(--font-heading);
      font-size: 1.52rem;
      color: var(--text-main);
      letter-spacing: -0.02em;
    }
    p {
      margin: .24rem 0 0;
      color: var(--text-muted);
      max-width: 56ch;
      line-height: 1.35;
    }
    .actions { display: flex; gap: .6rem; flex-wrap: wrap; }
    @media (max-width: 700px) {
      .header {
        flex-direction: column;
        align-items: flex-start;
      }
      h1 {
        font-size: 1.25rem;
      }
      p {
        max-width: 34ch;
      }
      .actions {
        width: 100%;
        justify-content: flex-start;
      }
    }
  `
})
export class AppPageHeaderComponent {
  @Input() title = '';
  @Input() subtitle = '';
}


