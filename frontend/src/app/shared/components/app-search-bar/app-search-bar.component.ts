import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-search-bar',
  standalone: true,
  template: `
    <div class="search-wrap">
      <span class="icon" aria-hidden="true"></span>
      <input
        class="search"
        type="text"
        [value]="value"
        [placeholder]="placeholder"
        (input)="onInput($event)" />
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .search-wrap {
      position: relative;
      display: flex;
      align-items: center;
      width: 100%;
    }
    .icon {
      position: absolute;
      left: .72rem;
      top: 50%;
      width: 14px;
      height: 14px;
      border: 2px solid #a6b4d7;
      border-radius: 999px;
      transform: translateY(-56%);
      pointer-events: none;
    }
    .icon::after {
      content: '';
      position: absolute;
      right: -3px;
      bottom: -5px;
      width: 7px;
      height: 2px;
      background: #a6b4d7;
      transform: rotate(42deg);
      border-radius: 999px;
    }
    .search {
      width: 100%;
      border-radius: 13px;
      border: 1px solid #d8e1f6;
      padding: .64rem .82rem .64rem 2.2rem;
      background: linear-gradient(145deg, rgba(255,255,255,.92), rgba(245,249,255,.84));
      box-shadow: inset 0 1px 0 rgba(255,255,255,.7);
      transition: border-color .2s ease, box-shadow .2s ease, transform .2s ease;
    }
    .search:focus {
      border-color: #bdc8f8;
      box-shadow: 0 0 0 4px rgba(111,126,247,.14);
      transform: translateY(-1px);
      outline: none;
    }
    @media (max-width: 700px) {
      .icon {
        left: .6rem;
        width: 13px;
        height: 13px;
      }
      .search {
        padding: .58rem .7rem .58rem 1.95rem;
      }
    }
  `
})
export class AppSearchBarComponent {
  @Input() value = '';
  @Input() placeholder = 'Search...';
  @Output() searchChange = new EventEmitter<string>();

  onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchChange.emit(value);
  }
}


