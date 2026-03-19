const appScrollLockOwners = new Set<object>();
const appScrollLockClassName = 'app-scroll-locked';
const appScrollContainerSelector = '.shell .content, .admin-shell .content';
const lockedScrollContainers = new Map<HTMLElement, { overflow: string; overscrollBehavior: string }>();

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
