import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: number;
  title: string;
  type: 'success' | 'error' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<ToastMessage[]>([]);
  private counter = 0;

  success(title: string): void {
    this.push(title, 'success');
  }

  error(title: string): void {
    this.push(title, 'error');
  }

  info(title: string): void {
    this.push(title, 'info');
  }

  remove(id: number): void {
    this.toasts.update((messages) => messages.filter((message) => message.id !== id));
  }

  private push(title: string, type: ToastMessage['type']): void {
    const id = ++this.counter;
    this.toasts.update((messages) => [...messages, { id, title, type }]);
    setTimeout(() => this.remove(id), 3500);
  }
}
