import { NgClass, NgFor } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [NgFor, NgClass],
  template: `
    <div class="toast-stack">
      <div class="toast" *ngFor="let toast of toastService.toasts()" [ngClass]="toast.type">
        <span class="dot"></span>
        {{ toast.title }}
      </div>
    </div>
  `,
  styles: `
    .toast-stack {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      display: grid;
      gap: .5rem;
      z-index: 2000;
    }
    .toast {
      display: inline-flex;
      align-items: center;
      gap: .45rem;
      padding: .68rem .84rem;
      border-radius: 12px;
      color: #243154;
      border: 1px solid #dce5f8;
      box-shadow: 0 12px 24px rgba(45, 63, 111, .18);
      min-width: 240px;
      font-size: .84rem;
      background: linear-gradient(145deg, rgba(255,255,255,.95), rgba(246,250,255,.86));
      animation: soft-rise .24s ease both;
      backdrop-filter: blur(6px);
    }
    .dot {
      width: 10px;
      height: 10px;
      border-radius: 999px;
      background: currentColor;
    }
    .success {
      color: #2e9a73;
      border-color: rgba(90, 194, 160, .38);
      background: linear-gradient(145deg, rgba(238, 254, 248, .95), rgba(227, 250, 241, .82));
    }
    .error {
      color: #c05473;
      border-color: rgba(232, 142, 168, .43);
      background: linear-gradient(145deg, rgba(255, 241, 245, .95), rgba(255, 231, 237, .86));
    }
    .info {
      color: #4d66ba;
      border-color: rgba(151, 171, 241, .44);
      background: linear-gradient(145deg, rgba(242, 246, 255, .95), rgba(234, 242, 255, .84));
    }
    @media (max-width: 700px) {
      .toast-stack {
        right: .7rem;
        left: .7rem;
      }
      .toast {
        min-width: 0;
      }
    }
  `
})
export class ToastComponent {
  readonly toastService = inject(ToastService);
}


