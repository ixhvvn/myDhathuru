import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../app-button/app-button.component';
import { AppCardComponent } from '../app-card/app-card.component';
import { AppStatusChipComponent } from '../app-status-chip/app-status-chip.component';
import { DocumentEmailRequest, DocumentEmailStatus } from '../../../core/models/app.models';

@Component({
  selector: 'app-document-email-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppStatusChipComponent],
  template: `
    <div class="drawer" *ngIf="open">
      <app-card>
        <div class="head">
          <div>
            <h3>{{ title }}</h3>
            <p>PDF will be attached automatically when the email is sent.</p>
          </div>
          <div class="head-meta">
            <app-status-chip [label]="statusLabel()" [variant]="statusVariant()"></app-status-chip>
            <small *ngIf="lastEmailedAt">Last sent: {{ formatDateTime(lastEmailedAt) }}</small>
          </div>
        </div>

        <form [formGroup]="form" class="form-grid" (ngSubmit)="submit()">
          <label>
            To
            <input type="email" [value]="toEmail" readonly>
          </label>

          <label>
            CC (optional)
            <input type="text" formControlName="ccEmail" placeholder="Enter CC email">
          </label>

          <label>
            Email Body
            <textarea rows="9" formControlName="body"></textarea>
          </label>

          <div class="attachment">
            <strong>Attachment</strong>
            <span>{{ attachmentName }}</span>
          </div>

          <div class="actions">
            <app-button variant="secondary" (clicked)="cancel.emit()">Cancel</app-button>
            <app-button type="submit" [loading]="loading" [disabled]="!toEmail">Send Email</app-button>
          </div>
        </form>
      </app-card>
    </div>
  `,
  styles: `
    .drawer {
      position: fixed;
      inset: 0;
      z-index: 1250;
      background: rgba(43, 54, 87, .34);
      backdrop-filter: blur(4px);
      display: grid;
      place-items: center;
      padding: 1rem;
    }
    .drawer app-card {
      width: min(680px, 100%);
      max-height: 92vh;
      overflow: auto;
      --card-bg: linear-gradient(160deg, rgba(255,255,255,.97), rgba(245,248,255,.92));
    }
    .head {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: .9rem;
      align-items: flex-start;
    }
    .head h3 {
      margin: 0;
    }
    .head p {
      margin: .22rem 0 0;
      color: var(--text-muted);
      font-size: .82rem;
      line-height: 1.5;
    }
    .head-meta {
      display: grid;
      gap: .3rem;
      justify-items: end;
    }
    .head-meta small {
      color: var(--text-muted);
      font-size: .74rem;
    }
    .form-grid {
      display: grid;
      gap: .78rem;
    }
    label {
      display: grid;
      gap: .24rem;
      font-size: .82rem;
      color: var(--text-muted);
    }
    input,
    textarea {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .58rem .66rem;
      background: rgba(255,255,255,.94);
      resize: vertical;
      font: inherit;
      color: var(--text-main);
    }
    input[readonly] {
      color: var(--text-main);
      background: linear-gradient(145deg, rgba(245,248,255,.92), rgba(237,243,255,.88));
    }
    .attachment {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .72rem .8rem;
      background: rgba(246, 250, 255, .62);
      display: grid;
      gap: .18rem;
    }
    .attachment strong {
      font-size: .83rem;
      color: var(--text-main);
      font-family: var(--font-heading);
    }
    .attachment span {
      color: var(--text-muted);
      font-size: .78rem;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: .5rem;
    }
    @media (max-width: 700px) {
      .drawer {
        padding: .65rem;
      }
      .drawer app-card {
        max-height: 95dvh;
      }
      .head {
        grid-template-columns: 1fr;
        display: grid;
      }
      .head-meta {
        justify-items: start;
      }
      .actions {
        flex-wrap: wrap;
      }
      .actions app-button {
        width: 100%;
      }
    }
  `
})
export class AppDocumentEmailDialogComponent implements OnChanges {
  @Input() open = false;
  @Input() title = 'Send Email';
  @Input() toEmail = '';
  @Input() body = '';
  @Input() attachmentName = 'document.pdf';
  @Input() loading = false;
  @Input() emailStatus: DocumentEmailStatus = 'Pending';
  @Input() lastEmailedAt?: string | null;

  @Output() cancel = new EventEmitter<void>();
  @Output() send = new EventEmitter<DocumentEmailRequest>();

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    ccEmail: ['', [Validators.maxLength(500)]],
    body: ['', [Validators.required, Validators.maxLength(4000)]]
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue || changes['body'] || changes['toEmail']) {
      this.form.reset({
        ccEmail: '',
        body: this.body ?? ''
      });
    }
  }

  submit(): void {
    if (!this.toEmail) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.send.emit({
      ccEmail: value.ccEmail.trim() || undefined,
      body: value.body.trim()
    });
  }

  statusLabel(): string {
    return this.emailStatus === 'Emailed' ? 'Emailed' : 'Email Pending';
  }

  statusVariant(): 'green' | 'amber' {
    return this.emailStatus === 'Emailed' ? 'green' : 'amber';
  }

  formatDateTime(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return `${date.toLocaleDateString()} ${date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
  }
}
