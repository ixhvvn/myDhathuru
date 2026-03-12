import { Component, Input } from '@angular/core';
import { NgClass } from '@angular/common';

@Component({
  selector: 'app-status-chip',
  standalone: true,
  imports: [NgClass],
  template: `<span class="chip" [ngClass]="variant"><i></i>{{ label }}</span>`,
  styles: `
    .chip {
      display: inline-flex;
      align-items: center;
      gap: .35rem;
      padding: .28rem .58rem;
      border-radius: 999px;
      font-size: .76rem;
      font-weight: 600;
      text-transform: capitalize;
      border: 1px solid transparent;
    }
    i {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: currentColor;
    }
    .green { color: #2f9872; background: rgba(125, 229, 196, .26); border-color: rgba(80, 190, 156, .28); }
    .red { color: #be4f6f; background: rgba(255, 194, 210, .42); border-color: rgba(234, 132, 162, .3); }
    .amber { color: #a9721c; background: rgba(255, 228, 170, .5); border-color: rgba(236, 180, 84, .32); }
    .blue { color: #4f66b9; background: rgba(201, 215, 255, .55); border-color: rgba(143, 163, 236, .34); }
    .gray { color: #627191; background: rgba(226, 233, 246, .7); border-color: rgba(181, 194, 220, .35); }
  `
})
export class AppStatusChipComponent {
  @Input() label = '';
  @Input() variant: 'green' | 'red' | 'amber' | 'blue' | 'gray' = 'gray';
}


