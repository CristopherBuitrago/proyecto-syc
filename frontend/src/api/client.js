const ORDERS_API_URL = import.meta.env.VITE_ORDERS_API_URL || "http://localhost:5000";
const CATALOG_API_URL = import.meta.env.VITE_CATALOG_API_URL || "http://localhost:4000";

// Ambos backends devuelven el mismo shape de error ({ message, code, details }),
// así que un solo cliente/parser alcanza para las dos APIs (ver DECISIONS.md).
export class ApiError extends Error {
  constructor(message, code, details, status) {
    super(message);
    this.code = code;
    this.details = details;
    this.status = status;
  }
}

async function request(baseUrl, path, { method = "GET", body } = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (response.status === 204) return null;

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    throw new ApiError(
      data?.message || "Ocurrió un error inesperado.",
      data?.code,
      data?.details,
      response.status
    );
  }

  return data;
}

export const ordersApi = {
  get: (path) => request(ORDERS_API_URL, path),
  post: (path, body) => request(ORDERS_API_URL, path, { method: "POST", body }),
};

export const catalogApi = {
  get: (path) => request(CATALOG_API_URL, path),
  post: (path, body) => request(CATALOG_API_URL, path, { method: "POST", body }),
};
