import { DatePipe } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-date-badge',
  standalone: true,
  imports: [DatePipe],
  template: `<span class="date">{{ value | date: format }}</span>`,
  styles: `
    .date {
      display: inline-block;
      padding: .28rem .56rem;
      border-radius: 999px;
      background: linear-gradient(130deg, rgba(201, 222, 255, .75), rgba(216, 241, 255, .72));
      color: #4866ab;
      border: 1px solid rgba(158, 181, 235, .45);
      font-size: .76rem;
      font-weight: 600;
    }
  `
})
export class AppDateBadgeComponent {
  @Input() value: string | Date = '';
  @Input() format = 'yyyy-MM-dd';
}


