import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}

@Component({
  selector: 'app-fetch-data',
  templateUrl: './fetch-data.html',
})
export class FetchData {
  protected readonly forecasts = signal<WeatherForecast[] | undefined>(undefined);

  constructor() {
    // The request goes to the ASP.NET Core host: the host serves /weatherforecast itself
    // and forwards everything else to this dev server.
    inject(HttpClient)
      .get<WeatherForecast[]>('/weatherforecast')
      .subscribe({
        next: (result) => this.forecasts.set(result),
        error: (error) => console.error(error),
      });
  }
}
