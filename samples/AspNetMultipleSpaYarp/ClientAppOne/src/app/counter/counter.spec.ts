import { TestBed } from '@angular/core/testing';

import { Counter } from './counter';

describe('Counter', () => {
  it('should display the current count and increment it', async () => {
    const fixture = TestBed.createComponent(Counter);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('strong')?.textContent).toBe('0');

    (compiled.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(compiled.querySelector('strong')?.textContent).toBe('1');
  });
});
