import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { ordersApi } from "../api/client";

// Sesión de cliente simulada: el usuario elige "quién es" una sola vez, no en cada
// pedido. No es autenticación real (eso es un extra opcional del enunciado) — es una
// decisión de UX para que la app se sienta como "el cliente pide su propio pedido" y
// no como un panel de administrador. Ver DECISIONS.md, punto 8.
const CustomerSessionContext = createContext(null);

const STORAGE_KEY = "syc.currentCustomerId";

export function CustomerSessionProvider({ children }) {
  const [customers, setCustomers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [currentCustomerId, setCurrentCustomerIdState] = useState(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored ? Number(stored) : null;
  });

  useEffect(() => {
    ordersApi
      .get("/api/customers")
      .then((data) => {
        setCustomers(data);
        setCurrentCustomerIdState((current) => {
          if (current && data.some((c) => c.id === current)) return current;
          return data[0]?.id ?? null;
        });
      })
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  function setCurrentCustomerId(id) {
    setCurrentCustomerIdState(id);
    localStorage.setItem(STORAGE_KEY, String(id));
  }

  const currentCustomer = useMemo(
    () => customers.find((c) => c.id === currentCustomerId) ?? null,
    [customers, currentCustomerId]
  );

  return (
    <CustomerSessionContext.Provider
      value={{ customers, loading, error, currentCustomerId, currentCustomer, setCurrentCustomerId }}
    >
      {children}
    </CustomerSessionContext.Provider>
  );
}

export function useCustomerSession() {
  const ctx = useContext(CustomerSessionContext);
  if (!ctx) throw new Error("useCustomerSession debe usarse dentro de CustomerSessionProvider");
  return ctx;
}
