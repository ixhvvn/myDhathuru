import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  template: `
    <div class="empty">
      <div class="pulse"></div>
      <h3>{{ title }}</h3>
      <p>{{ description }}</p>
      <ng-content></ng-content>
    </div>
  `,
  styles: `
    .empty {
      position: relative;
      overflow: hidden;
      border: 1px dashed var(--border-strong);
      border-radius: 16px;
      padding: 1.6rem 1.2rem;
      text-align: center;
      color: var(--text-muted);
      background: linear-gradient(180deg, rgba(255,255,255,.9), rgba(245,248,253,.9));
    }
    .pulse {
      width: 42px;
      height: 42px;
      margin: 0 auto .5rem;
      border-radius: 14px;
      background: linear-gradient(140deg, rgba(120, 138, 255, .24), rgba(121, 212, 239, .24));
      border: 1px solid rgba(148, 166, 245, .36);
      box-shadow: inset 0 1px 0 rgba(255,255,255,.8);
      animation: soft-rise .45s ease both;
    }
    h3 { margin: 0 0 .3rem; color: var(--text-main); }
    p { margin: 0 0 .7rem; }
  `
})
export class AppEmptyStateComponent {
  @Input() title = 'No records';
  @Input() description = 'Nothing found for your current filter.';
}


