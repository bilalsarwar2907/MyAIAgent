// Typed accessors for axios-style errors thrown by the shared `api` client, so
// stores don't have to cast the caught value to `any`.

interface ApiErrorLike {
  response?: {
    status?: number;
    data?: string | { message?: string };
  };
}

/** HTTP status of the failed response, if the server answered at all. */
export function errorStatus(err: unknown): number | undefined {
  return (err as ApiErrorLike | undefined)?.response?.status;
}

/** Message from the error response body (plain string or `{ message }`), else `fallback`. */
export function errorMessage(err: unknown, fallback: string): string {
  const data = (err as ApiErrorLike | undefined)?.response?.data;
  return (typeof data === 'string' ? data : data?.message) || fallback;
}
