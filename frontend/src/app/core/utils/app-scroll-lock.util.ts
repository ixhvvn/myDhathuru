const appScrollLockOwners = new Set<object>();
const appScrollLockClassName = 'app-scroll-locked';
const appScrollContainerSelector = '.shell .content, .admin-shell .content';
const lockedScrollContainers = new Map<HTMLElement, { overflow: string; overscrollBehavior: string }>();
const overlayRootSelector = 'body div.drawer, body aside.drawer, body .bug-modal, body .dialog, body section.modal, body .payroll-modal';
let listenersAttached = false;
let touchStartY = 0;

export function setAppScrollLock(owner: object, locked: boolean): void {
  if (typeof document === 'undefined') {
    return;
  }

  if (locked) {
    appScrollLockOwners.add(owner);
  } else {
    appScrollLockOwners.delete(owner);
  }

  const hasActiveLock = appScrollLockOwners.size > 0;
  document.documentElement.classList.toggle(appScrollLockClassName, hasActiveLock);
  document.body.classList.toggle(appScrollLockClassName, hasActiveLock);
  syncScrollInterception(hasActiveLock);

  if (hasActiveLock) {
    document.querySelectorAll<HTMLElement>(appScrollContainerSelector).forEach((container) => {
      if (!lockedScrollContainers.has(container)) {
        lockedScrollContainers.set(container, {
          overflow: container.style.overflow,
          overscrollBehavior: container.style.overscrollBehavior
        });
      }

      container.style.overflow = 'hidden';
      container.style.overscrollBehavior = 'none';
    });
    return;
  }

  lockedScrollContainers.forEach((state, container) => {
    container.style.overflow = state.overflow;
    container.style.overscrollBehavior = state.overscrollBehavior;
  });
  lockedScrollContainers.clear();
}

function syncScrollInterception(active: boolean): void {
  if (typeof document === 'undefined' || listenersAttached === active) {
    return;
  }

  if (active) {
    document.addEventListener('wheel', handleWheel, { passive: false, capture: true });
    document.addEventListener('touchstart', handleTouchStart, { passive: true, capture: true });
    document.addEventListener('touchmove', handleTouchMove, { passive: false, capture: true });
    listenersAttached = true;
    return;
  }

  document.removeEventListener('wheel', handleWheel, { capture: true });
  document.removeEventListener('touchstart', handleTouchStart, { capture: true });
  document.removeEventListener('touchmove', handleTouchMove, { capture: true });
  listenersAttached = false;
}

function handleWheel(event: WheelEvent): void {
  if (!shouldAllowOverlayScroll(event.target, event.deltaY)) {
    event.preventDefault();
  }
}

function handleTouchStart(event: TouchEvent): void {
  touchStartY = event.touches[0]?.clientY ?? 0;
}

function handleTouchMove(event: TouchEvent): void {
  const currentY = event.touches[0]?.clientY ?? touchStartY;
  const deltaY = touchStartY - currentY;

  if (!shouldAllowOverlayScroll(event.target, deltaY)) {
    event.preventDefault();
    return;
  }

  touchStartY = currentY;
}

function shouldAllowOverlayScroll(target: EventTarget | null, deltaY: number): boolean {
  const element = target instanceof Element ? target : null;
  if (!element) {
    return false;
  }

  const overlayRoot = element.closest(overlayRootSelector);
  if (!(overlayRoot instanceof HTMLElement)) {
    return false;
  }

  return findScrollableAncestor(element, overlayRoot, deltaY) !== null;
}

function findScrollableAncestor(element: Element, overlayRoot: HTMLElement, deltaY: number): HTMLElement | null {
  let current: Element | null = element;

  while (current instanceof HTMLElement) {
    if (isScrollable(current) && canScroll(current, deltaY)) {
      return current;
    }

    if (current === overlayRoot) {
      break;
    }

    current = current.parentElement;
  }

  return null;
}

function isScrollable(element: HTMLElement): boolean {
  if (element.scrollHeight <= element.clientHeight + 1) {
    return false;
  }

  const overflowY = window.getComputedStyle(element).overflowY;
  return overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay';
}

function canScroll(element: HTMLElement, deltaY: number): boolean {
  if (deltaY > 0) {
    return element.scrollTop + element.clientHeight < element.scrollHeight - 1;
  }

  if (deltaY < 0) {
    return element.scrollTop > 0;
  }

  return true;
}
