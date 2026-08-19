import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function apiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const problem = error.error as ProblemDetails | null;
  const validation = problem?.errors ? Object.values(problem.errors).flat()[0] : null;
  return validation ?? problem?.detail ?? problem?.title ?? fallback;
}
