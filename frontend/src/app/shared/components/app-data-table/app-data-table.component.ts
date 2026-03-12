import { Component, Input } from '@angular/core';
import { NgIf } from '@angular/common';
import { AppEmptyStateComponent } from '../app-empty-state/app-empty-state.component';

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [NgIf, AppEmptyStateComponent],
  template: `
    <div class="table-wrap">
      <div class="table-scroll" *ngIf="hasData; else emptyTpl">
        <table>
          <ng-content></ng-content>
        </table>
      </div>
      <ng-template #emptyTpl>
        <app-empty-state [title]="emptyTitle" [description]="emptyDescription"></app-empty-state>
      </ng-template>
    </div>
  `,
  styles: `
    .table-wrap {
      border: 1px solid #dee5f5;
      border-radius: 16px;
      background: rgba(255,255,255,.86);
      overflow: hidden;
      box-shadow: 0 10px 24px rgba(76, 96, 156, .08);
      animation: soft-rise .3s ease both;
    }
    .table-scroll { overflow: auto; }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: .86rem;
    }
    :host ::ng-deep th,
    :host ::ng-deep td {
      padding: .74rem .8rem;
      border-bottom: 1px solid #e9eef9;
      text-align: left;
      white-space: nowrap;
    }
    :host ::ng-deep th {
      background: linear-gradient(180deg, #f6f8ff, #f0f4ff);
      color: #5a6e9b;
      font-size: .77rem;
      text-transform: uppercase;
      letter-spacing: .04em;
    }
    :host ::ng-deep tbody tr:hover {
      background: linear-gradient(120deg, rgba(233, 238, 255, .6), rgba(232, 246, 255, .52));
    }
    :host ::ng-deep tbody tr:last-child td {
      border-bottom: 0;
    }
    @media (max-width: 700px) {
      .table-wrap {
        border-radius: 14px;
      }
      :host ::ng-deep th,
      :host ::ng-deep td {
        padding: .6rem .62rem;
      }
      :host ::ng-deep th {
        font-size: .7rem;
      }
      table {
        font-size: .8rem;
      }
    }
  `
})
export class AppDataTableComponent {
  @Input() hasData = false;
  @Input() emptyTitle = 'No data yet';
  @Input() emptyDescription = 'Create your first record to populate this list.';
}


