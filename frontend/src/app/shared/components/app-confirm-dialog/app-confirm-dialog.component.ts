import { NgIf } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { AppButtonComponent } from '../app-button/app-button.component';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [NgIf, AppButtonComponent],
  template: `
    <div class="overlay" *ngIf="open">
      <div class="dialog">
        <h3>{{ title }}</h3>
        <p>{{ message }}</p>
        <div class="actions">
          <app-button variant="secondary" (clicked)="cancel.emit()">Cancel</app-button>
          <app-button variant="danger" (clicked)="confirm.emit()">Confirm</app-button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(38, 48, 79, .36);
      backdrop-filter: blur(4px);
      display: grid;
      place-items: center;
      z-index: 1000;
      padding: 1rem;
    }
    .dialog {
      width: min(420px, 100%);
      background: linear-gradient(160deg, rgba(255,255,255,.95), rgba(245,249,255,.9));
      border-radius: 18px;
      border: 1px solid rgba(255,255,255,.86);
      box-shadow: 0 20px 34px rgba(32, 45, 78, .26);
      padding: 1.05rem;
      animation: soft-rise .24s ease both;
    }
    h3 { margin: 0 0 .35rem; }
    p { margin: 0 0 .9rem; color: var(--text-muted); line-height: 1.45; }
    .actions { display: flex; justify-content: flex-end; gap: .6rem; }
    @media (max-width: 640px) {
      .dialog {
        border-radius: 16px;
        padding: .9rem;
      }
      .actions {
        flex-wrap: wrap;
        justify-content: stretch;
      }
      .actions app-button {
        display: block;
        flex: 1 1 140px;
      }
    }
  `
})
export class AppConfirmDialogComponent {
  @Input() open = false;
  @Input() title = 'Are you sure?';
  @Input() message = 'This action cannot be undone.';

  @Output() cancel = new EventEmitter<void>();
  @Output() confirm = new EventEmitter<void>();
}


