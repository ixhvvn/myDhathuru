import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgClass, NgIf } from '@angular/common';

@Component({
  selector: 'app-button',
  standalone: true,
  imports: [NgClass, NgIf],
  template: `
    <button
      [type]="type"
      [disabled]="disabled || loading"
      [ngClass]="['app-btn', variant, size, fullWidth ? 'full' : '']"
      (click)="clicked.emit($event)">
      <span class="spinner" *ngIf="loading"></span>
      <ng-content></ng-content>
    </button>
  `,
  styles: `
    .app-btn {
      border: 1px solid transparent;
      border-radius: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: transform .2s ease, box-shadow .2s ease, border-color .2s ease, background .2s ease;
      display: inline-flex;
      align-items: center;
      gap: .5rem;
      justify-content: center;
      position: relative;
      overflow: hidden;
      letter-spacing: .01em;
    }
    .app-btn::after {
      content: '';
      position: absolute;
      inset: 0;
      background: linear-gradient(110deg, transparent 0%, rgba(255,255,255,.32) 50%, transparent 100%);
      transform: translateX(-130%);
      transition: transform .5s ease;
      pointer-events: none;
    }
    .app-btn:disabled {
      opacity: .6;
      cursor: not-allowed;
    }
    .app-btn:not(:disabled):hover {
      transform: translateY(-2px);
      box-shadow: 0 12px 24px rgba(79, 100, 172, 0.22);
    }
    .app-btn:not(:disabled):hover::after {
      transform: translateX(130%);
    }
    .md { min-height: 41px; padding: .62rem 1rem; font-size: .9rem; }
    .sm { min-height: 34px; padding: .46rem .76rem; font-size: .78rem; }
    .full { width: 100%; }
    .primary {
      background: linear-gradient(130deg, var(--primary), var(--primary-strong) 65%, #6d99f5);
      color: #fff;
      box-shadow: 0 11px 20px rgba(102, 117, 225, .34);
    }
    .primary:not(:disabled):hover {
      box-shadow: 0 16px 24px rgba(93, 109, 221, .42);
    }
    .secondary {
      background: linear-gradient(145deg, rgba(255,255,255,.9), #edf3ff);
      color: #56658d;
      border-color: #d8e0f6;
    }
    .danger {
      background: linear-gradient(135deg, #e993a5, #de7d95);
      color: #fff;
      box-shadow: 0 10px 18px rgba(210, 88, 117, .25);
    }
    .success {
      background: linear-gradient(135deg, #55b48b, #429f78);
      color: #fff;
      box-shadow: 0 10px 18px rgba(66, 155, 119, .28);
    }
    .warning {
      background: linear-gradient(135deg, #f4b05f, #e4912e);
      color: #fff;
      box-shadow: 0 10px 18px rgba(212, 140, 53, .28);
    }
    .spinner {
      width: 14px;
      height: 14px;
      border-radius: 50%;
      border: 2px solid rgba(255,255,255,.5);
      border-top-color: #fff;
      animation: spin .9s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `
})
export class AppButtonComponent {
  @Input() type: 'button' | 'submit' = 'button';
  @Input() variant: 'primary' | 'secondary' | 'danger' | 'success' | 'warning' = 'primary';
  @Input() size: 'sm' | 'md' = 'md';
  @Input() fullWidth = false;
  @Input() loading = false;
  @Input() disabled = false;
  @Output() clicked = new EventEmitter<MouseEvent>();
}


