import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  PortalAdminEmailAudienceMode,
  PortalAdminEmailBusinessOption,
  PortalAdminEmailCampaign,
  PortalAdminEmailCampaignSendResult
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-email-service-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Email Service</h1>
      <p>Send notices, announcements, and platform updates to all registered businesses or only selected companies, with each company receiving its own hidden recipient list.</p>
    </section>

    <section class="summary-grid" *ngIf="!loadingBusinesses()">
      <app-card class="summary-card summary-indigo">
        <span>Registered Companies</span>
        <strong>{{ businesses().length }}</strong>
        <small>Businesses available for portal-admin mailings.</small>
      </app-card>
      <app-card class="summary-card summary-green">
        <span>Active Companies</span>
        <strong>{{ activeBusinessCount() }}</strong>
        <small>Enabled businesses currently reachable in the live portal.</small>
      </app-card>
      <app-card class="summary-card summary-cyan">
        <span>Admin CC Contacts</span>
        <strong>{{ totalAdminContacts() }}</strong>
        <small>Active admin users that can be copied per company.</small>
      </app-card>
      <app-card class="summary-card summary-peach">
        <span>Latest Campaigns</span>
        <strong>{{ campaigns().length }}</strong>
        <small>Recent send history loaded on this page.</small>
      </app-card>
    </section>

    <app-loader *ngIf="loadingBusinesses() || loadingCampaigns()"></app-loader>

    <ng-container *ngIf="!loadingBusinesses() && !loadingCampaigns()">
      <app-card class="privacy-card">
        <div class="privacy-copy">
          <strong>Recipient privacy is enforced.</strong>
          <p>myDhathuru sends one email per company, so one business never sees the email address of another business. When admin CC is enabled, only that same company and its own admins are included together.</p>
        </div>
      </app-card>

      <app-card class="audience-card">
        <div class="audience-switch">
          <button type="button" [class.active]="audienceMode() === 'AllBusinesses'" (click)="setAudienceMode('AllBusinesses')">All Companies</button>
          <button type="button" [class.active]="audienceMode() === 'SelectedBusinesses'" (click)="setAudienceMode('SelectedBusinesses')">Selected Companies</button>
        </div>
        <div class="audience-meta" *ngIf="audienceMode() === 'SelectedBusinesses'">
          <span>{{ selectedBusinesses().length }} {{ companyLabel(selectedBusinesses().length) }} selected</span>
          <app-button size="sm" type="button" variant="secondary" (clicked)="openCompanyPicker()">Choose Companies</app-button>
        </div>
      </app-card>

      <app-card class="compose-card">
        <div class="section-head">
          <div>
            <h2>Compose Announcement</h2>
            <p>Use <code>[company name]</code> anywhere in the message body to personalize each email automatically.</p>
          </div>
          <div class="target-summary">
            <span>{{ targetBusinesses().length }} {{ companyLabel(targetBusinesses().length) }}</span>
            <small>{{ targetAdminCcCount() }} {{ adminRecipientLabel(targetAdminCcCount()) }}</small>
            <app-button *ngIf="audienceMode() === 'SelectedBusinesses'" size="sm" type="button" variant="secondary" (clicked)="openCompanyPicker()">
              Manage Selection
            </app-button>
          </div>
        </div>

        <form [formGroup]="composeForm" class="compose-grid">
          <label class="full">
            Subject
            <input type="text" formControlName="subject" maxlength="250" placeholder="Service notice, platform announcement, billing reminder">
          </label>

          <label class="full">
            Email Body
            <textarea
              rows="8"
              formControlName="body"
              maxlength="5000"
              placeholder="Dear Sir/Madam,&#10;&#10;This is a notice from myDhathuru for [company name].&#10;&#10;Thanks and regards,&#10;myDhathuru"></textarea>
          </label>

          <label class="toggle">
            <input type="checkbox" formControlName="ccAdminUsers">
            <span>CC each company's active admin users</span>
          </label>

          <label class="toggle">
            <input type="checkbox" formControlName="includeDisabledBusinesses">
            <span>Include disabled businesses in the target audience</span>
          </label>
        </form>

        <div class="hint-row" *ngIf="audienceMode() === 'SelectedBusinesses' && selectedDisabledExcludedCount() > 0">
          <span>{{ selectedDisabledExcludedCount() }} disabled {{ selectionLabel(selectedDisabledExcludedCount()) }} will stay excluded until "Include disabled businesses" is enabled.</span>
        </div>

        <div class="actions">
          <app-button type="button" [loading]="sending()" (clicked)="sendCampaign()">Send Email Campaign</app-button>
        </div>
      </app-card>

      <app-card class="result-card" *ngIf="lastResult() as result">
        <div class="section-head">
          <div>
            <h2>Latest Send Result</h2>
            <p>{{ result.sentCompanyCount }} sent and {{ result.failedCompanyCount }} failed from {{ result.requestedCompanyCount }} targeted companies.</p>
          </div>
        </div>

        <div class="table-wrap" *ngIf="result.results.length > 0">
          <table>
            <thead>
              <tr>
                <th>Company</th>
                <th>To</th>
                <th>Admin CC</th>
                <th>Status</th>
                <th>Error</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of result.results">
                <td>{{ row.companyName }}</td>
                <td>{{ row.toEmail }}</td>
                <td>{{ row.ccAdminCount }}</td>
                <td><span class="status" [attr.data-send-status]="row.status">{{ row.status }}</span></td>
                <td>{{ row.errorMessage || '-' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </app-card>

      <app-card class="history-card">
        <div class="section-head">
          <div>
            <h2>Campaign History</h2>
            <p>Database-backed send history for portal-admin notices and announcements.</p>
          </div>
        </div>

        <app-empty-state
          *ngIf="campaigns().length === 0"
          title="No campaigns sent yet"
          description="Your email campaign history will appear here after the first send.">
        </app-empty-state>

        <div class="table-wrap" *ngIf="campaigns().length > 0">
          <table>
            <thead>
              <tr>
                <th>Subject</th>
                <th>Audience</th>
                <th>Companies</th>
                <th>Sent</th>
                <th>Failed</th>
                <th>Admin CC</th>
                <th>Sent By</th>
                <th>Sent At</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let campaign of campaigns()">
                <td class="subject-cell">{{ campaign.subject }}</td>
                <td>{{ campaign.audienceMode === 'AllBusinesses' ? 'All companies' : 'Selected companies' }}</td>
                <td>{{ campaign.requestedCompanyCount }}</td>
                <td>{{ campaign.sentCompanyCount }}</td>
                <td>{{ campaign.failedCompanyCount }}</td>
                <td>{{ campaign.ccAdminUsers ? 'Enabled' : 'Off' }}</td>
                <td>{{ campaign.sentByName || '-' }}</td>
                <td>{{ campaign.sentAt | date:'yyyy-MM-dd HH:mm' }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="results-footer" *ngIf="campaigns().length > 0">
          <div class="results-meta">Total {{ totalCampaignCount() }} campaign{{ totalCampaignCount() === 1 ? '' : 's' }}</div>
          <div class="pagination">
            <span>Page {{ historyPage() }} of {{ historyTotalPages() }}</span>
            <div class="pagination-actions">
              <app-button size="sm" type="button" variant="secondary" [disabled]="historyPage() <= 1" (clicked)="changeHistoryPage(historyPage() - 1)">
                Previous
              </app-button>
              <app-button size="sm" type="button" variant="secondary" [disabled]="historyPage() >= historyTotalPages()" (clicked)="changeHistoryPage(historyPage() + 1)">
                Next
              </app-button>
            </div>
          </div>
        </div>
      </app-card>

      <div class="company-picker-overlay" *ngIf="companyPickerOpen()" (click)="closeCompanyPicker()">
        <app-card class="company-picker-dialog" (click)="$event.stopPropagation()">
          <div class="company-picker-head">
            <div>
              <h2>Select Companies</h2>
              <p>Choose the registered companies that should receive this email. Delivery stays private for each company.</p>
            </div>
            <app-button size="sm" type="button" variant="secondary" (clicked)="closeCompanyPicker()">Close</app-button>
          </div>

          <div class="picker-toolbar picker-toolbar-modal">
            <label class="search-box">
              Search Companies
              <input type="text" [value]="companySearch()" (input)="companySearch.set(($any($event.target).value || '').trimStart())" placeholder="Search by company or email">
            </label>
            <div class="picker-meta">
              <span>{{ selectedBusinesses().length }} selected</span>
              <small>{{ visibleBusinesses().length }} visible</small>
            </div>
            <div class="picker-actions">
              <app-button size="sm" type="button" variant="secondary" (clicked)="selectAllVisible()">Select Visible</app-button>
              <app-button size="sm" type="button" variant="secondary" (clicked)="clearSelections()">Clear</app-button>
            </div>
          </div>

          <app-empty-state
            *ngIf="visibleBusinesses().length === 0"
            title="No businesses match the filter"
            description="Adjust the search or audience settings to continue.">
          </app-empty-state>

          <div class="company-picker-body" *ngIf="visibleBusinesses().length > 0">
            <div class="business-grid">
              <label class="business-option" *ngFor="let business of visibleBusinesses()" [class.selected]="isSelected(business.tenantId)">
                <input type="checkbox" [checked]="isSelected(business.tenantId)" (change)="toggleBusiness(business.tenantId)">
                <div class="option-copy">
                  <strong>{{ business.companyName }}</strong>
                  <small>{{ business.companyEmail }}</small>
                  <div class="option-meta">
                    <span class="status" [attr.data-status]="business.status">{{ business.status }}</span>
                    <span>{{ business.activeAdminCount }} admin{{ business.activeAdminCount === 1 ? '' : 's' }}</span>
                    <span *ngIf="business.primaryAdminEmail">{{ business.primaryAdminEmail }}</span>
                  </div>
                </div>
              </label>
            </div>
          </div>

          <div class="company-picker-footer">
            <span>{{ selectedBusinesses().length }} {{ companyLabel(selectedBusinesses().length) }} ready</span>
            <div class="picker-actions">
              <app-button size="sm" type="button" variant="secondary" (clicked)="clearSelections()">Clear</app-button>
              <app-button size="sm" type="button" (clicked)="closeCompanyPicker()">Done</app-button>
            </div>
          </div>
        </app-card>
      </div>
    </ng-container>
  `,
  styles: `
    .page-head h1 { margin: 0; color: #2f4269; font-family: var(--font-heading); font-size: 1.5rem; font-weight: 600; }
    .page-head p { margin: .32rem 0 0; color: #63759d; max-width: 920px; }
    .summary-grid {
      margin-top: .78rem;
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: .68rem;
    }
    .summary-card {
      --card-padding: .78rem .86rem;
      --card-shadow: none;
      --card-hover-shadow: none;
      --card-hover-transform: none;
      --card-shimmer-display: none;
      display: grid;
      gap: .18rem;
    }
    .summary-card span {
      color: #5f73a0;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-size: .74rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .summary-card strong {
      color: #2b3f68;
      font-size: 1.34rem;
      line-height: 1.2;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .summary-card small {
      color: #697ca5;
      font-size: .75rem;
      line-height: 1.3;
    }
    .summary-indigo { --card-bg: linear-gradient(145deg, rgba(236,241,255,.94), rgba(223,232,255,.9)); }
    .summary-green { --card-bg: linear-gradient(145deg, rgba(229,249,238,.94), rgba(213,242,225,.9)); }
    .summary-cyan { --card-bg: linear-gradient(145deg, rgba(227,248,255,.94), rgba(214,241,255,.9)); }
    .summary-peach { --card-bg: linear-gradient(145deg, rgba(255,242,228,.94), rgba(252,232,213,.9)); }
    .privacy-card,
    .audience-card,
    .compose-card,
    .business-card,
    .result-card,
    .history-card {
      margin-top: .78rem;
    }
    .privacy-card {
      --card-padding: .9rem;
      background: linear-gradient(145deg, rgba(241,246,255,.95), rgba(233,243,255,.88));
    }
    .privacy-copy strong {
      display: block;
      color: #32486f;
      font-family: var(--font-heading);
      font-size: .96rem;
      font-weight: 600;
    }
    .privacy-copy p {
      margin: .3rem 0 0;
      color: #60739d;
      line-height: 1.55;
    }
    .audience-card {
      --card-padding: .5rem;
    }
    .audience-switch {
      display: flex;
      gap: .42rem;
      flex-wrap: wrap;
    }
    .audience-meta {
      margin-top: .55rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .6rem;
      flex-wrap: wrap;
      color: #50658e;
      font-size: .82rem;
      font-weight: 600;
    }
    .audience-switch button {
      border: 1px solid #d5e0f8;
      border-radius: 12px;
      background: linear-gradient(145deg, rgba(255,255,255,.92), rgba(237,244,255,.88));
      color: #5a6d97;
      font-family: var(--font-heading);
      font-size: .84rem;
      font-weight: 600;
      padding: .52rem .82rem;
      cursor: pointer;
    }
    .audience-switch button.active {
      border-color: transparent;
      color: #fff;
      background: linear-gradient(135deg, #7383f8, #67a8ef);
      box-shadow: 0 12px 22px rgba(96, 112, 199, .25);
    }
    .section-head {
      display: flex;
      align-items: start;
      justify-content: space-between;
      gap: .8rem;
      flex-wrap: wrap;
      margin-bottom: .62rem;
    }
    .section-head h2 {
      margin: 0;
      color: #33486f;
      font-family: var(--font-heading);
      font-size: 1.05rem;
      font-weight: 600;
    }
    .section-head p {
      margin: .24rem 0 0;
      color: #60739d;
      font-size: .84rem;
      max-width: 760px;
      line-height: 1.5;
    }
    .section-head code {
      background: rgba(116, 134, 245, .12);
      color: #4f65a0;
      border-radius: 999px;
      padding: .14rem .38rem;
      font-size: .76rem;
    }
    .target-summary {
      display: grid;
      gap: .12rem;
      text-align: right;
      color: #4a5f8b;
    }
    .target-summary span {
      font-family: var(--font-heading);
      font-size: .98rem;
      font-weight: 600;
    }
    .target-summary small {
      color: #6a7ea7;
      font-size: .76rem;
    }
    .target-summary app-button {
      justify-self: end;
    }
    .compose-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .6rem .68rem;
    }
    .compose-grid > label {
      display: grid;
      gap: .22rem;
      color: #5f739d;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
      min-width: 0;
    }
    .compose-grid > label.full {
      grid-column: 1 / -1;
    }
    input[type='text'],
    textarea {
      border: 1px solid #ccdaf5;
      border-radius: 11px;
      background: #fff;
      color: #34496e;
      padding: .58rem .66rem;
      font-size: .86rem;
      min-height: 42px;
      font-family: inherit;
      resize: vertical;
    }
    input[type='text']:focus,
    textarea:focus {
      outline: none;
      border-color: #7f8df5;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .toggle {
      display: flex !important;
      align-items: center;
      gap: .48rem;
      border: 1px solid #d8e2f9;
      border-radius: 11px;
      background: rgba(255,255,255,.86);
      min-height: 44px;
      padding: .5rem .6rem;
      color: #4b6088;
      font-size: .82rem;
    }
    .toggle input[type='checkbox'] {
      width: 18px;
      height: 18px;
      margin: 0;
      accent-color: #6f83f4;
      flex: 0 0 auto;
    }
    .hint-row {
      margin-top: .56rem;
      border: 1px solid rgba(239, 201, 129, .5);
      border-radius: 12px;
      background: rgba(255, 247, 231, .88);
      color: #8b6a2e;
      padding: .56rem .68rem;
      font-size: .8rem;
      line-height: 1.45;
    }
    .actions {
      margin-top: .72rem;
      display: flex;
      justify-content: flex-end;
      gap: .44rem;
      flex-wrap: wrap;
    }
    .picker-actions {
      display: flex;
      gap: .44rem;
      flex-wrap: wrap;
    }
    .company-picker-overlay {
      position: fixed;
      inset: 0;
      z-index: 80;
      display: grid;
      place-items: center;
      padding: 1rem;
      background: rgba(236, 242, 255, .56);
      backdrop-filter: blur(10px);
    }
    .company-picker-dialog {
      width: min(980px, 100%);
      max-height: min(86vh, 900px);
      --card-padding: .9rem;
      --card-overflow: hidden;
      --card-shadow: 0 24px 60px rgba(84, 103, 163, .22);
      --card-hover-shadow: 0 24px 60px rgba(84, 103, 163, .22);
      --card-hover-transform: none;
    }
    .company-picker-head {
      display: flex;
      align-items: start;
      justify-content: space-between;
      gap: .8rem;
      flex-wrap: wrap;
      margin-bottom: .75rem;
    }
    .company-picker-head h2 {
      margin: 0;
      color: #33486f;
      font-family: var(--font-heading);
      font-size: 1.06rem;
      font-weight: 600;
    }
    .company-picker-head p {
      margin: .24rem 0 0;
      color: #60739d;
      font-size: .84rem;
      line-height: 1.5;
      max-width: 720px;
    }
    .picker-toolbar-modal {
      margin-bottom: .72rem;
    }
    .company-picker-body {
      max-height: min(54vh, 540px);
      overflow: auto;
      padding-right: .15rem;
    }
    .company-picker-footer {
      margin-top: .78rem;
      padding-top: .78rem;
      border-top: 1px solid #dce6fa;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .7rem;
      flex-wrap: wrap;
      color: #51668f;
      font-size: .84rem;
      font-weight: 600;
    }
    .picker-toolbar {
      display: flex;
      align-items: end;
      justify-content: space-between;
      gap: .68rem;
      flex-wrap: wrap;
      margin-bottom: .62rem;
    }
    .search-box {
      display: grid;
      gap: .22rem;
      color: #5f739d;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
      flex: 1 1 280px;
      min-width: min(280px, 100%);
    }
    .picker-meta {
      display: grid;
      gap: .08rem;
      text-align: right;
      color: #50658e;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .picker-meta small {
      color: #6d80a8;
      font-size: .76rem;
      font-weight: 500;
    }
    .business-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .58rem;
    }
    .business-option {
      display: flex;
      gap: .56rem;
      align-items: start;
      border: 1px solid #d8e2f9;
      border-radius: 14px;
      background: linear-gradient(145deg, rgba(255,255,255,.95), rgba(243,248,255,.88));
      padding: .7rem .74rem;
      cursor: pointer;
      transition: transform .18s ease, box-shadow .18s ease, border-color .18s ease;
    }
    .business-option:hover {
      transform: translateY(-1px);
      box-shadow: 0 14px 28px rgba(91, 111, 170, .12);
      border-color: rgba(136, 156, 210, .6);
    }
    .business-option.selected {
      border-color: rgba(120, 141, 244, .7);
      box-shadow: 0 16px 30px rgba(102, 124, 206, .16);
      background: linear-gradient(145deg, rgba(236,242,255,.96), rgba(235,247,255,.9));
    }
    .business-option input[type='checkbox'] {
      width: 18px;
      height: 18px;
      margin-top: .1rem;
      accent-color: #6f83f4;
      flex: 0 0 auto;
    }
    .option-copy {
      min-width: 0;
      display: grid;
      gap: .18rem;
    }
    .option-copy strong {
      color: #2f4369;
      font-family: var(--font-heading);
      font-size: .9rem;
      font-weight: 600;
      line-height: 1.28;
    }
    .option-copy small {
      color: #63779f;
      font-size: .78rem;
      overflow-wrap: anywhere;
    }
    .option-meta {
      display: flex;
      flex-wrap: wrap;
      gap: .38rem;
      margin-top: .14rem;
      color: #6478a2;
      font-size: .74rem;
    }
    .status {
      display: inline-flex;
      align-items: center;
      border-radius: 999px;
      padding: .22rem .48rem;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .72rem;
      border: 1px solid transparent;
      width: fit-content;
    }
    .status[data-status='Active'],
    .status[data-send-status='Sent'] {
      color: #2f9870;
      border-color: rgba(124, 215, 180, .5);
      background: rgba(208, 245, 225, .74);
    }
    .status[data-status='Disabled'],
    .status[data-send-status='Failed'] {
      color: #b44c6b;
      border-color: rgba(231, 154, 178, .5);
      background: rgba(255, 219, 230, .74);
    }
    .table-wrap {
      border: 1px solid #d9e3fa;
      border-radius: 14px;
      overflow: auto;
      margin-top: .2rem;
    }
    table {
      width: 100%;
      min-width: 860px;
      border-collapse: collapse;
      font-size: .82rem;
    }
    th, td {
      padding: .56rem .62rem;
      border-bottom: 1px solid #e0e8fb;
      color: #415680;
      vertical-align: middle;
      text-align: left;
    }
    th {
      background: #f3f7ff;
      color: #5e74a1;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-family: var(--font-heading);
      font-size: .74rem;
      font-weight: 600;
      white-space: nowrap;
    }
    .subject-cell {
      max-width: 280px;
      min-width: 220px;
      white-space: normal;
    }
    .results-footer {
      margin-top: .72rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .8rem;
      flex-wrap: wrap;
    }
    .results-meta {
      color: #5f739d;
      font-size: .83rem;
      font-weight: 600;
    }
    .pagination {
      display: flex;
      align-items: center;
      gap: .65rem;
      color: #5f739d;
      font-size: .84rem;
      font-weight: 600;
      flex-wrap: wrap;
    }
    .pagination-actions {
      display: inline-flex;
      gap: .42rem;
      align-items: center;
      flex-wrap: wrap;
    }
    @media (max-width: 1320px) {
      .summary-grid,
      .business-grid,
      .compose-grid {
        grid-template-columns: 1fr 1fr;
      }
    }
    @media (max-width: 980px) {
      .summary-grid,
      .business-grid,
      .compose-grid {
        grid-template-columns: 1fr;
      }
      .actions {
        justify-content: flex-start;
      }
      .target-summary,
      .picker-meta {
        text-align: left;
      }
      .target-summary app-button {
        justify-self: start;
      }
      .audience-meta,
      .company-picker-head,
      .company-picker-footer {
        align-items: stretch;
      }
      .company-picker-overlay {
        padding: .75rem;
      }
      .company-picker-dialog {
        width: 100%;
        max-height: calc(100vh - 1.5rem);
      }
      .company-picker-body {
        max-height: calc(100vh - 19rem);
      }
    }
  `
})
export class PortalAdminEmailServicePageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly audienceMode = signal<PortalAdminEmailAudienceMode>('AllBusinesses');
  readonly loadingBusinesses = signal(true);
  readonly loadingCampaigns = signal(true);
  readonly sending = signal(false);
  readonly businesses = signal<PortalAdminEmailBusinessOption[]>([]);
  readonly campaigns = signal<PortalAdminEmailCampaign[]>([]);
  readonly selectedTenantIds = signal<string[]>([]);
  readonly companyPickerOpen = signal(false);
  readonly companySearch = signal('');
  readonly historyPage = signal(1);
  readonly historyTotalPages = signal(1);
  readonly totalCampaignCount = signal(0);
  readonly lastResult = signal<PortalAdminEmailCampaignSendResult | null>(null);

  readonly composeForm = this.fb.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(250)]],
    body: ['', [Validators.required, Validators.maxLength(5000)]],
    ccAdminUsers: [true],
    includeDisabledBusinesses: [false]
  });

  readonly activeBusinessCount = computed(() => this.businesses().filter((business) => business.status === 'Active').length);
  readonly totalAdminContacts = computed(() => this.businesses().reduce((total, business) => total + business.activeAdminCount, 0));

  readonly eligibleBusinesses = computed(() =>
    this.businesses().filter((business) => this.composeForm.controls.includeDisabledBusinesses.value || business.status === 'Active'));

  readonly visibleBusinesses = computed(() =>
  {
    const search = this.companySearch().trim().toLowerCase();
    return this.businesses().filter((business) =>
    {
      if (!search) {
        return true;
      }

      return business.companyName.toLowerCase().includes(search)
        || business.companyEmail.toLowerCase().includes(search)
        || (business.primaryAdminEmail || '').toLowerCase().includes(search);
    });
  });

  readonly selectedBusinesses = computed(() =>
  {
    const selected = new Set(this.selectedTenantIds());
    const eligible = new Set(this.eligibleBusinesses().map((business) => business.tenantId));
    return this.businesses().filter((business) => selected.has(business.tenantId) && eligible.has(business.tenantId));
  });

  readonly selectedDisabledExcludedCount = computed(() =>
  {
    if (this.composeForm.controls.includeDisabledBusinesses.value) {
      return 0;
    }

    const selected = new Set(this.selectedTenantIds());
    return this.businesses().filter((business) => selected.has(business.tenantId) && business.status !== 'Active').length;
  });

  readonly targetBusinesses = computed(() =>
    this.audienceMode() === 'AllBusinesses'
      ? this.eligibleBusinesses()
      : this.selectedBusinesses());

  readonly targetAdminCcCount = computed(() =>
  {
    if (!this.composeForm.controls.ccAdminUsers.value) {
      return 0;
    }

    return this.targetBusinesses().reduce((total, business) => total + business.activeAdminCount, 0);
  });

  constructor() {
    this.loadBusinessOptions();
    this.loadCampaigns();
  }

  setAudienceMode(mode: PortalAdminEmailAudienceMode): void {
    this.audienceMode.set(mode);

    if (mode === 'SelectedBusinesses') {
      this.openCompanyPicker();
      return;
    }

    this.closeCompanyPicker();
  }

  openCompanyPicker(): void {
    this.audienceMode.set('SelectedBusinesses');
    this.companyPickerOpen.set(true);
  }

  closeCompanyPicker(): void {
    this.companyPickerOpen.set(false);
    this.companySearch.set('');
  }

  isSelected(tenantId: string): boolean {
    return this.selectedTenantIds().includes(tenantId);
  }

  toggleBusiness(tenantId: string): void {
    this.selectedTenantIds.update((selected) =>
      selected.includes(tenantId)
        ? selected.filter((value) => value !== tenantId)
        : [...selected, tenantId]);
  }

  selectAllVisible(): void {
    const currentSelection = new Set(this.selectedTenantIds());
    const selectableIds = this.visibleBusinesses()
      .filter((business) => this.composeForm.controls.includeDisabledBusinesses.value || business.status === 'Active')
      .map((business) => business.tenantId);

    selectableIds.forEach((tenantId) => currentSelection.add(tenantId));
    this.selectedTenantIds.set(Array.from(currentSelection));
  }

  clearSelections(): void {
    this.selectedTenantIds.set([]);
  }

  changeHistoryPage(pageNumber: number): void {
    if (pageNumber < 1 || pageNumber > this.historyTotalPages()) {
      return;
    }

    this.historyPage.set(pageNumber);
    this.loadCampaigns();
  }

  sendCampaign(): void {
    if (this.composeForm.invalid) {
      this.composeForm.markAllAsTouched();
      this.toast.error('Subject and email body are required.');
      return;
    }

    if (this.audienceMode() === 'SelectedBusinesses' && this.targetBusinesses().length === 0) {
      this.toast.error('Select at least one eligible company before sending.');
      return;
    }

    this.sending.set(true);
    this.api.sendEmailServiceCampaign({
      audienceMode: this.audienceMode(),
      tenantIds: this.selectedTenantIds(),
      subject: this.composeForm.controls.subject.value.trim(),
      body: this.composeForm.controls.body.value.trim(),
      ccAdminUsers: this.composeForm.controls.ccAdminUsers.value,
      includeDisabledBusinesses: this.composeForm.controls.includeDisabledBusinesses.value
    })
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: (result) => {
          this.lastResult.set(result);
          this.loadCampaigns();
          this.toast.success(`Email campaign processed for ${result.requestedCompanyCount} compan${result.requestedCompanyCount === 1 ? 'y' : 'ies'}.`);
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to send the portal-admin email campaign.'))
      });
  }

  companyLabel(count: number): string {
    return count === 1 ? 'company' : 'companies';
  }

  adminRecipientLabel(count: number): string {
    return count === 1 ? 'admin CC recipient' : 'admin CC recipients';
  }

  selectionLabel(count: number): string {
    return count === 1 ? 'selection' : 'selections';
  }

  private loadBusinessOptions(): void {
    this.loadingBusinesses.set(true);
    this.api.getEmailServiceBusinessOptions()
      .pipe(finalize(() => this.loadingBusinesses.set(false)))
      .subscribe({
        next: (result) => this.businesses.set(result),
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load business email recipients.'))
      });
  }

  private loadCampaigns(): void {
    this.loadingCampaigns.set(true);
    this.api.getEmailServiceCampaigns({
      pageNumber: this.historyPage(),
      pageSize: 8
    })
      .pipe(finalize(() => this.loadingCampaigns.set(false)))
      .subscribe({
        next: (result) => {
          this.campaigns.set(result.items);
          this.totalCampaignCount.set(result.totalCount);
          this.historyTotalPages.set(Math.max(result.totalPages || 1, 1));
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load email campaign history.'))
      });
  }
}
