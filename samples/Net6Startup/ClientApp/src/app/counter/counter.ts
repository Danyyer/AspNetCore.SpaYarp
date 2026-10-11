import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-counter',
  templateUrl: './counter.html',
})
export class Counter {
  protected readonly currentCount = signal(0);

  protected incrementCounter() {
    this.currentCount.update((count) => count + 1);
  }
}
