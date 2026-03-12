import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loader',
  standalone: true,
  template: `
    <div class="loader-wrap" [class.overlay]="overlay">
      <div class="loader">
        <span></span>
      </div>
      <span>{{ label }}</span>
    </div>
  `,
  styles: `
    .loader-wrap {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: .6rem;
      color: var(--text-muted);
      min-height: 80px;
    }
    .overlay {
      position: absolute;
      inset: 0;
      background: rgba(255,255,255,.72);
      border-radius: inherit;
      backdrop-filter: blur(2px);
    }
    .loader {
      width: 28px;
      height: 28px;
      border: 2px solid #d4dcf0;
      border-radius: 50%;
      border-top-color: #8a98ff;
      animation: spin 1s linear infinite;
      display: grid;
      place-items: center;
    }
    .loader span {
      width: 11px;
      height: 11px;
      border-radius: 50%;
      background: linear-gradient(130deg, #95a2ff, #84d5f1);
      opacity: .9;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `
})
export class AppLoaderComponent {
  @Input() label = 'Loading...';
  @Input() overlay = false;
}


