import { DOCUMENT } from '@angular/common';
import { Injectable, NgZone, inject } from '@angular/core';
import { fromEvent, merge, Subscription, timer } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class InactivityService {
  private readonly authService = inject(AuthService);
  private readonly document = inject(DOCUMENT);
  private readonly ngZone = inject(NgZone);

  private activitySub?: Subscription;
  private timeoutSub?: Subscription;
  private readonly inactivityMs = 60 * 60 * 1000;

  start(): void {
    this.stop();

    this.ngZone.runOutsideAngular(() => {
      this.activitySub = merge(
        fromEvent(this.document, 'mousemove'),
        fromEvent(this.document, 'click'),
        fromEvent(this.document, 'keydown'),
        fromEvent(this.document, 'scroll'),
        fromEvent(this.document, 'touchstart')
      ).subscribe(() => this.resetTimer());
    });

    this.resetTimer();
  }

  stop(): void {
    this.activitySub?.unsubscribe();
    this.timeoutSub?.unsubscribe();
    this.activitySub = undefined;
    this.timeoutSub = undefined;
  }

  private resetTimer(): void {
    this.timeoutSub?.unsubscribe();
    this.timeoutSub = timer(this.inactivityMs).subscribe(() => {
      if (this.authService.isAuthenticated()) {
        this.authService.logout('Logged out after 1 hour of inactivity.');
      }
    });
  }
}
