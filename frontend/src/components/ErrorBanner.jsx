import { AlertTriangle } from "lucide-react";

// Único componente de error para las dos APIs, porque ambas ya devuelven el mismo
// shape { message, code, details } (ver DECISIONS.md). Caso especial: STOCK_UNAVAILABLE
// trae details.failedItems con qué línea(s) fallaron y cuánto stock hay realmente —
// es un requisito explícito del enunciado mostrarlo, no solo el mensaje genérico.
export default function ErrorBanner({ error }) {
  if (!error) return null;

  const failedItems = error.code === "STOCK_UNAVAILABLE" ? error.details?.failedItems : null;

  return (
    <div className="error-banner">
      <div className="error-banner-header">
        <AlertTriangle size={18} />
        <p>{error.message}</p>
      </div>
      {failedItems && (
        <ul>
          {failedItems.map((f) => (
            <li key={f.productId}>
              Producto #{f.productId}: pediste {f.requested}, solo hay {f.available} disponibles.
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
