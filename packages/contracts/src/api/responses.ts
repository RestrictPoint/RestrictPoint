/** Standard success envelope (docs/16-API-Specification.md). */
export interface ApiSuccessResponse<TData> {
  data: TData;
  correlationId: string;
  timestamp: string;
}

/** Standard error envelope (docs/16-API-Specification.md). */
export interface ApiErrorResponse {
  error: {
    code: string;
    message: string;
    details?: Record<string, unknown>;
  };
  correlationId: string;
}
