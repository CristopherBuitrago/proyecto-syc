// Misma estructura de error que el servicio .NET (sección 8: "estructura de respuesta
// de error uniforme en ambos backends").
class ApiError extends Error {
  constructor(statusCode, message, code, details) {
    super(message);
    this.statusCode = statusCode;
    this.code = code;
    this.details = details;
  }

  toJSON() {
    return { message: this.message, code: this.code, details: this.details };
  }
}

function errorHandler(err, req, res, next) {
  if (err instanceof ApiError) {
    console.error(`[${req.method} ${req.path}] ${err.code}: ${err.message}`);
    return res.status(err.statusCode).json(err.toJSON());
  }
  console.error(`[${req.method} ${req.path}] Error no controlado:`, err);
  return res.status(500).json({ message: "Error interno del servidor.", code: "INTERNAL_ERROR" });
}

module.exports = { ApiError, errorHandler };
