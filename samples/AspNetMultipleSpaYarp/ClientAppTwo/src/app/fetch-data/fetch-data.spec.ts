import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { FetchData } from './fetch-data';

describe('FetchData', () => {
  it('should render the forecasts returned by the server', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(FetchData);
    const http = TestBed.inject(HttpTestingController);

    http.expectOne('/weatherforecast').flush([
      { date: '2030-01-01', temperatureC: 0, temperatureF: 32, summary: 'Freezing' },
    ]);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Freezing');
  });
});
