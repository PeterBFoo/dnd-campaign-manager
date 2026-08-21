import { HttpErrorResponse } from '@angular/common/http';

import { apiErrorMessage } from './problem-details';

describe('ProblemDetails messages', () => {
  it('prefers validation, detail and title in that order', () => {
    expect(apiErrorMessage(new HttpErrorResponse({ error: { errors: { email: ['Correo inválido.'] } } }), 'Fallback'))
      .toBe('Correo inválido.');
    expect(apiErrorMessage(new HttpErrorResponse({ error: { detail: 'Detalle.', title: 'Título.' } }), 'Fallback'))
      .toBe('Detalle.');
    expect(apiErrorMessage(new HttpErrorResponse({ error: { title: 'Título.' } }), 'Fallback'))
      .toBe('Título.');
  });

  it('uses the fallback for non-HTTP errors', () => {
    expect(apiErrorMessage(new Error('private'), 'Mensaje público.')).toBe('Mensaje público.');
  });
});
