import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/app.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  get<T>(path: string, params?: Record<string, unknown>): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(this.buildUrl(path), { params: this.toParams(params) })
      .pipe(map((response) => response.data));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(this.buildUrl(path), body)
      .pipe(map((response) => response.data));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .put<ApiResponse<T>>(this.buildUrl(path), body)
      .pipe(map((response) => response.data));
  }

  delete<T>(path: string): Observable<T> {
    return this.http
      .delete<ApiResponse<T>>(this.buildUrl(path))
      .pipe(map((response) => response.data));
  }

  getFile(path: string, params?: Record<string, unknown>): Observable<Blob> {
    return this.http.get(this.buildUrl(path), {
      params: this.toParams(params),
      responseType: 'blob'
    });
  }

  postFile(path: string, body: unknown): Observable<Blob> {
    return this.http.post(this.buildUrl(path), body, {
      responseType: 'blob'
    });
  }

  private buildUrl(path: string): string {
    return `${environment.apiUrl}/${path.replace(/^\//, '')}`;
  }

  private toParams(params?: Record<string, unknown>): HttpParams {
    let httpParams = new HttpParams();

    if (!params) {
      return httpParams;
    }

    for (const [key, value] of Object.entries(params)) {
      if (value === null || value === undefined || value === '') {
        continue;
      }
      httpParams = httpParams.append(key, String(value));
    }

    return httpParams;
  }
}
